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
/// What every character's melee mechanics share: the swings the server is tracking, the weapon's hits and how they are reported to the
/// ability that started the swing, and telling every machine which attack to play. A melee ability asks for a swing and is told what the
/// swing hits; subclasses decide which attack plays and when its hit window is open, so abilities never handle animation, hitboxes or combo
/// timing themselves. The server decides which attack plays and what it hits; clients only play the animation.
/// </summary>
[RequireComponent(typeof(Character))]
[RequireComponent(typeof(Animator))]
public abstract class MeleeDeliveryBase : NetworkBehaviour, IMeleeSwingHost
{
    /// <summary>
    /// A swing the server has started and whose hits it is waiting to report.
    /// </summary>
    protected sealed class Swing
    {
        public ContinuationToken Token;
        public IMeleeEvents Events;
        public ExecutionId Execution;
        public int AttackNumber;
        public float StartedAt;
    }

    /// <summary>
    /// Swings the server is tracking, by attack number. A chained swing can exist before the one playing now has finished.
    /// </summary>
    protected readonly Dictionary<int, Swing> Swings = new Dictionary<int, Swing>();

    private Weapon weapon;

    /// <summary>
    /// The character who owns this melee mechanics component.
    /// </summary>
    protected Character Character { get; private set; }

    /// <summary>
    /// The swing whose hit window is open, or null. Hits outside any window are ignored.
    /// </summary>
    protected Swing ActiveSwing { get; set; }

    protected Weapon WeaponComponent => weapon != null ? weapon : (weapon = GetComponentInChildren<Weapon>());

    /// <summary>
    /// Raised on every client when the server starts an attack. The int is the attack number (1 for the first attack of a chain).
    /// </summary>
    public delegate void NotifyMeleeAttackStarted(int attackNumber);

    public event NotifyMeleeAttackStarted MeleeAttackStarted;

    /// <summary>
    /// How an attack is shown on every machine.
    /// </summary>
    public abstract AttackAnimation AnimationFor(int attackNumber);

    /// <summary>
    /// The damage multiplier of an attack.
    /// </summary>
    protected abstract float DamageScalar(int attackNumber);

    /// <inheritdoc/>
    public abstract MeleePressOutcome Press(long pressTick, bool freshAllowed);

    /// <inheritdoc/>
    public abstract void CancelPress();

    /// <inheritdoc/>
    public abstract bool Begin(in MeleeSwingLaunch launch, IMeleeEvents events);

    protected virtual void Awake()
    {
        Character = GetComponent<Character>();
    }

    public override void OnNetworkSpawn()
    {
        if (!IsServer)
        {
            return;
        }

        // A server with no screen has nothing to draw, and an animator that is culled never moves the weapon its hitbox rides on.
        foreach (Animator animator in GetComponentsInChildren<Animator>(true))
        {
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
        }

        Weapon equipped = WeaponComponent;
        if (equipped != null)
        {
            equipped.HitConfirmed += HandleWeaponHit;
        }

        Character.OnDeath += ReleaseAllSwings;
    }

    public override void OnNetworkDespawn()
    {
        if (weapon != null)
        {
            weapon.HitConfirmed -= HandleWeaponHit;
        }

        if (Character != null)
        {
            Character.OnDeath -= ReleaseAllSwings;
        }

        if (IsServer)
        {
            ReleaseAllSwings();
        }
    }

    /// <summary>
    /// Starts tracking a swing and tells every machine which attack to play. Server only.
    /// </summary>
    protected void StartSwing(int attackNumber, in MeleeSwingLaunch launch, IMeleeEvents events)
    {
        if (Swings.TryGetValue(attackNumber, out Swing stale))
        {
            Finish(stale);
        }

        Swings[attackNumber] = new Swing
        {
            Token = launch.Token,
            Events = events,
            Execution = launch.Execution,
            AttackNumber = attackNumber,
            StartedAt = Time.time,
        };

        MeleeAttackClientRpc(attackNumber);
    }

    /// <summary>
    /// Triggers a melee attack on all clients.
    /// </summary>
    /// <param name="attackNumber">The attack the server has started. Every client plays the same one.</param>
    [Rpc(SendTo.Everyone)]
    public void MeleeAttackClientRpc(int attackNumber)
    {
        MeleeAttackStarted?.Invoke(attackNumber);
    }

    /// <summary>
    /// Ends a swing: the ability that started it is told, and it is no longer tracked.
    /// </summary>
    protected void Finish(Swing swing)
    {
        if (Swings.TryGetValue(swing.AttackNumber, out Swing tracked) && tracked == swing)
        {
            Swings.Remove(swing.AttackNumber);
        }

        if (ActiveSwing == swing)
        {
            ActiveSwing = null;
        }

        swing.Events.Completed(swing.Token);
    }

    /// <summary>
    /// Closes the weapon's hit window and ends every swing in progress, telling their abilities. Server only.
    /// </summary>
    protected virtual void ReleaseAllSwings()
    {
        WeaponComponent?.DeactivateHitbox();
        foreach (Swing swing in Swings.Values.ToList())
        {
            Finish(swing);
        }

        ActiveSwing = null;
    }

    /// <summary>
    /// Server only. The weapon touched a target for the first time this swing. Reports the hit to the ability that started the swing.
    /// </summary>
    private void HandleWeaponHit(Character victim, BodyLocation location)
    {
        Swing swing = ActiveSwing;
        if (swing == null || victim == null || !Character.IsAlive)
        {
            return;
        }

        WeaponData weaponData = Character.WeaponData;
        float baseDamage = weaponData != null && weaponData.damage != null ? weaponData.damage.Damage : 0f;
        bool victimWasAlive = victim.IsAlive;

        MeleeHit hit = new MeleeHit(victim.Id, (int)location, swing.AttackNumber, DamageScalar(swing.AttackNumber), baseDamage);
        swing.Events.Hit(swing.Token, hit);

        // A blow that lands on a living target is a confirmed weapon hit; the damage above has already been applied, so rules reacting to it see the result.
        if (victimWasAlive)
        {
            GameplayEventDispatcher events = GameplayServices.Current.Events;
            events.Enqueue(new WeaponHitConfirmedEvent(Character.Id, victim.Id, (int)location, swing.AttackNumber, swing.Execution));
            if (!events.IsDraining)
            {
                events.Drain();
            }
        }
    }
}
