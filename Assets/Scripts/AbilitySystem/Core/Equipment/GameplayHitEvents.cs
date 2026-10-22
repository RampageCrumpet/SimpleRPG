namespace SimpleRPG.Gameplay
{
    /// <summary>
    /// A weapon blow landed on a target. Raised by melee delivery after the blow's own effects have been applied, so a rule reacting to it
    /// (an on-hit proc) sees the result. Effects caused by those rules, such as a spell, are not weapon hits and never raise this event again.
    /// </summary>
    public sealed class WeaponHitConfirmedEvent : GameplayEvent
    {
        public WeaponHitConfirmedEvent(EntityId attacker, EntityId victim, int bodyLocation, int attackNumber, ExecutionId execution)
        {
            Attacker = attacker;
            Victim = victim;
            BodyLocation = bodyLocation;
            AttackNumber = attackNumber;
            Execution = execution;
        }

        /// <summary>The entity that swung.</summary>
        public EntityId Attacker { get; }

        public EntityId Victim { get; }

        /// <summary>The body location hit, as the integer value of the game's body location enum.</summary>
        public int BodyLocation { get; }

        /// <summary>The attack in the combo chain that landed the blow.</summary>
        public int AttackNumber { get; }

        /// <summary>The ability execution the swing belongs to.</summary>
        public ExecutionId Execution { get; }

        /// <summary>This is always a direct weapon hit. Rules filter on this to avoid reacting to their own effects.</summary>
        public DamageProvenance Provenance => DamageProvenance.DirectWeaponHit;
    }
}
