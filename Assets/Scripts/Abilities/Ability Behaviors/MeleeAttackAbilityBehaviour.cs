using HitDetection;
using SimpleRPG;
using SimpleRPG.Abilities;
using System.Linq;
using Unity.Netcode;
using UnityEngine;
using static SimpleRPG.Character;

[RequireComponent(typeof(Character))]
[RequireComponent(typeof(Animator))]
public class MeleeAttackAbilityBehaviour : NetworkBehaviour, IInvokeableAbilityBehaviour, IQueueableAbilityBehaviour
{
    /// <summary>
    /// The character who owns this <see cref="FireProjectileAbillityBehaviour"/> 
    /// </summary>
    private Character character;

    private Weapon weapon;

    private WeaponData weaponData;

    /// <summary>
    /// Server-side cooldown record used to validate incoming attack requests.
    /// </summary>
    private readonly ServerCooldownTracker cooldowns = new ServerCooldownTracker();

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
    /// Raised on every client when the server starts an attack. The int is the attack number in the chain (1, 2 or 3).
    /// </summary>
    public delegate void NotifyMeleeAttackStarted(int attackNumber);

    public event NotifyMeleeAttackStarted MeleeAttackStarted;

    public void Start()
    {
        character = this.GetComponent<Character>();

        //TODO: This needs to be dynamic. A player could have many weapons.
        weapon = this.GetComponentInChildren<Weapon>();

        //TODO: This should be coming from the players inventory or equipment.
        this.weaponData = character.WeaponData;

        combo = new MeleeComboTracker(attackLengths, comboLengths);
    }

    /// <inheritdoc/>
    void IInvokeableAbilityBehaviour.Invoke(AbilityInstance abilityInstace)
    {
        MeleeAttackServerRpc(abilityInstace.Ability.AbillityName);
    }

    /// <inheritdoc/>
    /// <remarks>
    /// While an attack is playing, a press queues the next swing and bypasses the cooldown. During a combo continuation the press is consumed and does nothing.
    /// Only when neither is playing does the press fall through to a fresh attack under the cooldown.
    /// </remarks>
    bool IQueueableAbilityBehaviour.TryQueue()
    {
        animators ??= GetComponentsInChildren<Animator>(true);

        bool inAttack = false;
        bool inCombo = false;
        foreach (Animator animator in animators)
        {
            AnimatorStateInfo state = animator.GetCurrentAnimatorStateInfo(0);
            inAttack |= state.IsTag("Attack");
            inCombo |= state.IsTag("Combo");
        }

        if (!inAttack && !inCombo)
        {
            return false;
        }

        if (inAttack && Time.time >= nextQueueSendTime)
        {
            nextQueueSendTime = Time.time + QueueResendInterval;
            QueueAttackServerRpc();
        }

        return true;
    }

    /// <summary>
    /// Tell the player to swing their weapon across the network.
    /// </summary>
    /// <param name="abilityName"> The Ability name of the <see cref="MeleeAttackAbility"/> we want to trigger.</param>
    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
    void MeleeAttackServerRpc(string abilityName)
    {
        MeleeAttackAbility abilityInstance = this.character.PersonalAbilities.Select(x => x.Ability).OfType<MeleeAttackAbility>().FirstOrDefault(x => x.AbillityName == abilityName);

        // If we weren't able to find an ability return and log an error.
        if (abilityInstance == null)
        {
            Debug.LogError("Unable to find an ability with the name " + abilityName + " on character " + this.gameObject.name + ".");
            return;
        }

        // A fresh chain can't start while a queued chain is still running.
        if (!combo.CanStartAttack(Time.time))
        {
            return;
        }

        // The server is the authority on cooldowns, the client's own cooldown check is only a prediction.
        if (!cooldowns.TryActivate(abilityName, abilityInstance.CooldownTime, Time.time))
        {
            return;
        }

        combo.StartAttack(Time.time);
        MeleeAttackClientRpc(1);
    }

    /// <summary>
    /// Asks the server to queue the next swing of the combo. The server rejects it unless it lands inside the current attack's window.
    /// </summary>
    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
    void QueueAttackServerRpc()
    {
        if (combo.TryQueueNext(Time.time, out int nextAttack))
        {
            MeleeAttackClientRpc(nextAttack);
        }
    }

        /// <summary>
    /// Triggers a melee attack on all clients.
    /// </summary>
    /// <param name="attackNumber">The attack in the longsword chain (1, 2 or 3) the server has started. Every client plays the same branch.</param>
    [Rpc(SendTo.Everyone)]
    public void MeleeAttackClientRpc(int attackNumber)
    {
        weapon.StartSwinging(weaponData);
        MeleeAttackStarted?.Invoke(attackNumber);
    }

    /// <summary>
    /// Damage multiplier for each attack in the longsword chain, indexed by attack number minus one.
    /// Based on each attack clip's length relative to Attack 1 (Attack 1 = 1.0). Tune these for balance.
    /// </summary>
    [Tooltip("Damage multiplier for each longsword attack. Index 0 is Attack 1. Based on clip length relative to Attack 1.")]
    [SerializeField]
    private float[] comboDamageScalars = { 1.00f, 0.79f, 0.84f };

    /// <summary>
    /// Activates the weapons hitbox. Called by the attack clip's animation event; the event's int parameter is the attack number.
    /// </summary>
    /// <param name="attackNumber">The attack in the longsword chain (1, 2 or 3).</param>
    public void ActivateWeapon(int attackNumber)
    {
        weapon.ActivateHitbox(GetComboDamageScalar(attackNumber));
    }

    /// <summary>
    /// Gets the damage multiplier for an attack, defaulting to 1 if none is set.
    /// </summary>
    private float GetComboDamageScalar(int attackNumber)
    {
        int index = attackNumber - 1;
        return index >= 0 && index < comboDamageScalars.Length ? comboDamageScalars[index] : 1f;
    }

    /// <summary>
    /// Deactivates the weapons hitbox.
    /// </summary>
    public void DeactivateWeaponH()
    {
        weapon.DeactivateHitbox();
    }
}
