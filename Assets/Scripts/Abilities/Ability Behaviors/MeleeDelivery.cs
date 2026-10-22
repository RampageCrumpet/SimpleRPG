using SimpleRPG;
using SimpleRPG.Abilities;
using SimpleRPG.Gameplay;
using System.Linq;
using UnityEngine;

/// <summary>
/// Melee mechanics for conventional enemies: each swing is one of a set of independently selected attacks, and its hit window comes from
/// the attack clip's animation events. Players use <c>ComboMeleeDelivery</c> instead, whose hit windows come from combo data.
/// </summary>
public sealed class MeleeDelivery : MeleeDeliveryBase
{
    [System.Serializable]
    public class IndependentAttack
    {
        public string Trigger;
        [Min(0.1f)] public float Duration = 3f;
        [Min(0)] public float DamageScalar = 1f;
    }

    /// <summary>
    /// Seconds past a swing's expected length after which a swing whose animation never closed its hit window is released.
    /// </summary>
    private const float SwingTimeoutSlack = 2f;

    [Tooltip("The attacks this character chooses between. Hit windows come from clip events.")]
    [SerializeField] private IndependentAttack[] independentAttacks = System.Array.Empty<IndependentAttack>();

    private float recoveryUntil;

    /// <summary>
    /// True if a new attack may start: the last one's recovery is over and none is still in progress.
    /// </summary>
    public bool CanStartFreshChain => Time.time >= recoveryUntil && Swings.Count == 0;

    public override AttackAnimation AnimationFor(int attackNumber)
    {
        return AttackAnimation.FromTrigger(independentAttacks[Mathf.Clamp(attackNumber - 1, 0, independentAttacks.Length - 1)].Trigger);
    }

    protected override float DamageScalar(int attackNumber)
    {
        return independentAttacks[attackNumber - 1].DamageScalar;
    }

    private void Update()
    {
        if (!IsServer || Swings.Count == 0)
        {
            return;
        }

        // A swing whose animation never closed its hit window (it was interrupted) must not hold its ability open forever.
        foreach (Swing swing in Swings.Values.Where(s => Time.time - s.StartedAt > independentAttacks[s.AttackNumber - 1].Duration + SwingTimeoutSlack).ToList())
        {
            Finish(swing);
        }
    }

    /// <inheritdoc/>
    public override MeleePressOutcome Press(long pressTick, bool freshAllowed)
    {
        bool mayStart = IsServer && Character.IsAlive && freshAllowed && independentAttacks.Length > 0 && CanStartFreshChain;
        return mayStart ? MeleePressOutcome.Fresh : MeleePressOutcome.Rejected;
    }

    /// <inheritdoc/>
    public override void CancelPress()
    {
        // Nothing is reserved by a press; the attack is only chosen when the swing begins.
    }

    /// <inheritdoc/>
    /// <remarks>
    /// Chooses one of the independent attacks and plays it. The swing's hits are reported through <paramref name="events"/> while its hit
    /// window is open, and it completes when the window closes.
    /// </remarks>
    public override bool Begin(in MeleeSwingLaunch launch, IMeleeEvents events)
    {
        if (!IsServer || !Character.IsAlive)
        {
            return false;
        }

        if (independentAttacks.Length == 0)
        {
            Debug.LogError(name + " has no independent attacks, so it cannot swing.", this);
            return false;
        }

        if (!CanStartFreshChain)
        {
            return false;
        }

        int chosen = Mathf.Min(independentAttacks.Length - 1, (int)(GameplayServices.Current.Random.NextDouble() * independentAttacks.Length));
        int attackNumber = chosen + 1;
        recoveryUntil = Time.time + independentAttacks[chosen].Duration;

        StartSwing(attackNumber, launch, events);
        return true;
    }

    /// <summary>
    /// Opens the weapon's hit window. Called by the attack clip's animation event on every machine.
    /// On the server it also selects the swing the hits belong to.
    /// </summary>
    /// <param name="attackNumber">Independent clips share the event number, so it is ignored: the server already selected the only live swing.</param>
    public void ActivateWeapon(int attackNumber)
    {
        if (!Character.IsAlive || WeaponComponent == null) return;

        if (IsServer)
        {
            ActiveSwing = OldestSwing();
            if (ActiveSwing == null) return;
        }

        WeaponComponent.ActivateHitbox();
    }

    /// <summary>
    /// Closes the weapon's hit window, which also ends the swing it belonged to. Called by the attack clip's animation event.
    /// </summary>
    public void DeactivateWeaponH()
    {
        WeaponComponent?.DeactivateHitbox();

        if (IsServer && ActiveSwing != null)
        {
            Finish(ActiveSwing);
            ActiveSwing = null;
        }
    }

    // A clip with no attack number belongs to the oldest swing in progress.
    private Swing OldestSwing()
    {
        Swing oldest = null;
        foreach (Swing swing in Swings.Values)
        {
            if (oldest == null || swing.AttackNumber < oldest.AttackNumber) oldest = swing;
        }

        return oldest;
    }
}
