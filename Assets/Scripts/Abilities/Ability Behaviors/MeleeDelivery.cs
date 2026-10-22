using HitDetection;
using SimpleRPG;
using SimpleRPG.Abilities;
using SimpleRPG.Gameplay;
using System.Collections.Generic;
using System.Linq;
using Unity.Netcode;
using UnityEngine;
using EntityId = SimpleRPG.Gameplay.EntityId;

/// <summary>
/// A character's melee mechanics: the combo chain, the animation-driven hit window and the hits it produces.
/// A melee ability asks for a swing and is told what the swing hits; this component owns everything in between, so abilities never
/// handle animation, hitboxes or combo timing themselves. The server decides which attack plays and what it hits; clients only play the animation.
/// </summary>
[RequireComponent(typeof(Character))]
[RequireComponent(typeof(Animator))]
public class MeleeDelivery : NetworkBehaviour, IMeleeSwingHost
{
    /// <summary>
    /// Seconds past a swing's expected length after which a swing whose animation never closed its hit window is released.
    /// </summary>
    private const float SwingTimeoutSlack = 2f;

    /// <summary>
    /// A swing the server has started and whose hits it is waiting to report.
    /// </summary>
    private sealed class Swing
    {
        public ContinuationToken Token;
        public IMeleeEvents Events;
        public ExecutionId Execution;
        public int AttackNumber;
        public float StartedAt;
    }

    /// <summary>
    /// The character who owns this <see cref="MeleeDelivery"/>.
    /// </summary>
    private Character character;

    private Weapon weapon;

    /// <summary>
    /// Server-side combo chain. Decides which attack number is playing and whether a queued press is accepted.
    /// </summary>
    private MeleeComboTracker combo;

    /// <summary>
    /// Animators on this character and its children. Used to tell whether the player is mid-swing or mid-combo.
    /// </summary>
    private Animator[] animators;

    /// <summary>
    /// Earliest client time at which another queue request may be sent. Holding the button sends at most one request per interval.
    /// </summary>
    private float nextQueueSendTime;

    /// <summary>
    /// Minimum seconds between queue requests from the client. The server accepts only the first press in each attack window,
    /// so this only limits how often a held button floods the network.
    /// </summary>
    private const float QueueResendInterval = 0.1f;

    /// <summary>
    /// Swings the server is tracking, by attack number. A queued swing exists before the one playing now has finished.
    /// </summary>
    private readonly Dictionary<int, Swing> swings = new Dictionary<int, Swing>();

    /// <summary>
    /// The swing whose hit window is open, or null. Hits outside any window are ignored.
    /// </summary>
    private Swing activeSwing;

    /// <summary>
    /// The attack number a queued press was accepted for, waiting for its ability execution to ask for the swing. Zero if none.
    /// </summary>
    private int pendingQueuedAttack;

    /// <summary>
    /// Length in seconds of each attack clip in the longsword chain, in order. Its length is the number of attacks in the chain.
    /// Must match the clip lengths; the EditMode tests check this.
    /// </summary>
    [Tooltip("Length in seconds of each longsword attack clip, in order. Must match the clips.")]
    [SerializeField]
    private float[] attackLengths = { 70f / 60f, 55f / 60f, 59f / 60f };

    /// <summary>
    /// Length in seconds of the combo continuation after each attack clip. The last entry is unused because the chain ends with attack 3.
    /// </summary>
    [Tooltip("Length in seconds of the combo continuation after each longsword attack. The last entry is unused.")]
    [SerializeField]
    private float[] comboLengths = { 17f / 60f, 3f / 60f, 0f };

    /// <summary>
    /// Damage multiplier for each attack in the longsword chain, indexed by attack number minus one.
    /// Based on each attack clip's length relative to Attack 1 (Attack 1 = 1.0). Tune these for balance.
    /// </summary>
    [Tooltip("Damage multiplier for each longsword attack. Index 0 is Attack 1. Based on clip length relative to Attack 1.")]
    [SerializeField]
    private float[] comboDamageScalars = { 1.00f, 0.79f, 0.84f };

    /// <summary>
    /// Raised on every client when the server starts an attack. The int is the attack number in the chain (1, 2 or 3).
    /// </summary>
    public delegate void NotifyMeleeAttackStarted(int attackNumber);

    public event NotifyMeleeAttackStarted MeleeAttackStarted;

    private Weapon WeaponComponent => weapon != null ? weapon : (weapon = GetComponentInChildren<Weapon>());

    private void Awake()
    {
        character = GetComponent<Character>();
        combo = new MeleeComboTracker(attackLengths, comboLengths);
    }

    public override void OnNetworkSpawn()
    {
        if (!IsServer)
        {
            return;
        }

        // A server with no screen has nothing to draw, and an animator that is culled never plays the clips whose events open the hit window.
        foreach (Animator animator in GetComponentsInChildren<Animator>(true))
        {
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
        }

        Weapon equipped = WeaponComponent;
        if (equipped != null)
        {
            equipped.HitConfirmed += HandleWeaponHit;
        }

        character.OnDeath += ReleaseAllSwings;
    }

    public override void OnNetworkDespawn()
    {
        if (weapon != null)
        {
            weapon.HitConfirmed -= HandleWeaponHit;
        }

        if (character != null)
        {
            character.OnDeath -= ReleaseAllSwings;
        }

        if (IsServer)
        {
            ReleaseAllSwings();
        }
    }

    private void Update()
    {
        if (!IsServer || swings.Count == 0)
        {
            return;
        }

        // A swing whose animation never closed its hit window (it was interrupted) must not hold its ability open forever.
        foreach (Swing swing in swings.Values.Where(s => Time.time - s.StartedAt > SwingLength(s.AttackNumber) + SwingTimeoutSlack).ToList())
        {
            Finish(swing);
        }
    }

    /// <inheritdoc/>
    public bool CanStartFreshChain => combo.CanStartAttack(Time.time);

    /// <inheritdoc/>
    /// <remarks>
    /// Starts a fresh chain at attack 1, or plays the attack a queued press was accepted for. The swing's hits are reported through
    /// <paramref name="events"/> while its hit window is open, and it completes when the window closes.
    /// </remarks>
    public bool Begin(in MeleeSwingLaunch launch, IMeleeEvents events)
    {
        if (!IsServer || !character.IsAlive)
        {
            return false;
        }

        int attackNumber;
        if (pendingQueuedAttack > 0)
        {
            attackNumber = pendingQueuedAttack;
            pendingQueuedAttack = 0;
        }
        else
        {
            // A fresh chain can't start while a queued chain is still running.
            if (!combo.CanStartAttack(Time.time))
            {
                return false;
            }

            combo.StartAttack(Time.time);
            attackNumber = 1;
        }

        if (swings.TryGetValue(attackNumber, out Swing stale))
        {
            Finish(stale);
        }

        swings[attackNumber] = new Swing
        {
            Token = launch.Token,
            Events = events,
            Execution = launch.Execution,
            AttackNumber = attackNumber,
            StartedAt = Time.time,
        };

        MeleeAttackClientRpc(attackNumber);
        return true;
    }

    /// <inheritdoc/>
    /// <remarks>
    /// While an attack is playing, a press queues the next swing and bypasses the cooldown. During a combo continuation the press is consumed and does nothing.
    /// Only when neither is playing does the press fall through to a fresh attack under the cooldown.
    /// </remarks>
    public bool TryQueue(AbilityDefinition ability)
    {
        animators ??= GetComponentsInChildren<Animator>(true);

        bool inAttack = false;
        bool inCombo = false;
        foreach (Animator animator in animators)
        {
            // Layered animators (e.g. a skeleton's weapon-attack layer) play their attack state above the base layer, so check every layer.
            for (int layer = 0; layer < animator.layerCount; layer++)
            {
                AnimatorStateInfo state = animator.GetCurrentAnimatorStateInfo(layer);
                inAttack |= state.IsTag("Attack");
                inCombo |= state.IsTag("Combo");
            }
        }

        if (!inAttack && !inCombo)
        {
            return false;
        }

        if (inAttack && Time.time >= nextQueueSendTime)
        {
            nextQueueSendTime = Time.time + QueueResendInterval;
            RequestQueue(ability);
        }

        return true;
    }

    private void RequestQueue(AbilityDefinition ability)
    {
        if (IsServer)
        {
            QueueOnServer(ability);
        }
        else if (DefinitionRegistry.Instance != null && DefinitionRegistry.Instance.TryGetShortId(ability.Id, out uint shortId))
        {
            QueueAttackServerRpc(shortId);
        }
    }

    /// <summary>
    /// Asks the server to queue the next swing of the combo. The server rejects it unless it lands inside the current attack's window.
    /// </summary>
    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
    private void QueueAttackServerRpc(uint abilityShortId)
    {
        if (DefinitionRegistry.Instance != null && DefinitionRegistry.Instance.TryGetByShortId(abilityShortId, out GameplayDefinition definition) && definition is AbilityDefinition ability)
        {
            QueueOnServer(ability);
        }
    }

    /// <summary>
    /// Server only. Accepts a queued press if the character has the ability and the press lands inside the current attack's window,
    /// then runs the ability for the next attack without charging its cooldown again.
    /// </summary>
    private void QueueOnServer(AbilityDefinition ability)
    {
        if (!IsServer || !character.IsAlive || ability == null || !ability.HasNode(NodeTypes.MeleeSwing) || !character.GraphAbilities.Contains(ability))
        {
            return;
        }

        if (!combo.TryQueueNext(Time.time, out int nextAttack))
        {
            return;
        }

        pendingQueuedAttack = nextAttack;
        IAbilityActivator activator = GetComponent<IAbilityActivator>();
        if (activator == null || !activator.ExecuteQueued(ability))
        {
            pendingQueuedAttack = 0;
        }
    }

    /// <summary>
    /// Triggers a melee attack on all clients.
    /// </summary>
    /// <param name="attackNumber">The attack in the longsword chain (1, 2 or 3) the server has started. Every client plays the same branch.</param>
    [Rpc(SendTo.Everyone)]
    public void MeleeAttackClientRpc(int attackNumber)
    {
        MeleeAttackStarted?.Invoke(attackNumber);
    }

    /// <summary>
    /// Opens the weapon's hit window. Called by the attack clip's animation event on every machine; the event's int parameter is the attack number.
    /// On the server it also selects which tracked swing the hits belong to.
    /// </summary>
    /// <param name="attackNumber">The attack in the longsword chain (1, 2 or 3). Clips with no attack number pass 0, meaning the only swing.</param>
    public void ActivateWeapon(int attackNumber)
    {
        WeaponComponent.ActivateHitbox();

        if (IsServer)
        {
            activeSwing = FindSwing(attackNumber);
        }
    }

    /// <summary>
    /// Closes the weapon's hit window, which also ends the swing it belonged to. Called by the attack clip's animation event.
    /// </summary>
    public void DeactivateWeaponH()
    {
        WeaponComponent.DeactivateHitbox();

        if (IsServer && activeSwing != null)
        {
            Finish(activeSwing);
            activeSwing = null;
        }
    }

    private Swing FindSwing(int attackNumber)
    {
        if (attackNumber > 0)
        {
            return swings.TryGetValue(attackNumber, out Swing swing) ? swing : null;
        }

        // A clip with no attack number belongs to the oldest swing in progress.
        Swing oldest = null;
        foreach (Swing swing in swings.Values)
        {
            if (oldest == null || swing.AttackNumber < oldest.AttackNumber) oldest = swing;
        }

        return oldest;
    }

    /// <summary>
    /// Server only. The weapon touched a target for the first time this swing. Reports the hit to the ability that started the swing.
    /// </summary>
    private void HandleWeaponHit(Character victim, BodyLocation location)
    {
        Swing swing = activeSwing;
        if (swing == null || victim == null)
        {
            return;
        }

        WeaponData weaponData = character.WeaponData;
        float baseDamage = weaponData != null && weaponData.damage != null ? weaponData.damage.Damage : 0f;
        bool victimWasAlive = victim.IsAlive;

        MeleeHit hit = new MeleeHit(victim.Id, (int)location, swing.AttackNumber, GetComboDamageScalar(swing.AttackNumber), baseDamage);
        swing.Events.Hit(swing.Token, hit);

        // A blow that lands on a living target is a confirmed weapon hit; the damage above has already been applied, so rules reacting to it see the result.
        if (victimWasAlive)
        {
            GameplayEventDispatcher events = GameplayServices.Current.Events;
            events.Enqueue(new WeaponHitConfirmedEvent(character.Id, victim.Id, (int)location, swing.AttackNumber, swing.Execution));
            if (!events.IsDraining)
            {
                events.Drain();
            }
        }
    }

    /// <summary>
    /// Ends a swing: the ability that started it is told, and it is no longer tracked.
    /// </summary>
    private void Finish(Swing swing)
    {
        if (swings.TryGetValue(swing.AttackNumber, out Swing tracked) && tracked == swing)
        {
            swings.Remove(swing.AttackNumber);
        }

        if (activeSwing == swing)
        {
            activeSwing = null;
        }

        swing.Events.Completed(swing.Token);
    }

    private void ReleaseAllSwings()
    {
        pendingQueuedAttack = 0;
        foreach (Swing swing in swings.Values.ToList())
        {
            Finish(swing);
        }

        activeSwing = null;
    }

    private float SwingLength(int attackNumber)
    {
        int index = attackNumber - 1;
        float attack = index >= 0 && index < attackLengths.Length ? attackLengths[index] : 1f;
        float after = index >= 0 && index < comboLengths.Length ? comboLengths[index] : 0f;
        return attack + after;
    }

    /// <summary>
    /// Gets the damage multiplier for an attack, defaulting to 1 if none is set.
    /// </summary>
    private float GetComboDamageScalar(int attackNumber)
    {
        int index = attackNumber - 1;
        return index >= 0 && index < comboDamageScalars.Length ? comboDamageScalars[index] : 1f;
    }
}
