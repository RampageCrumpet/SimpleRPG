using System;

namespace SimpleRPG.Gameplay
{
    /// <summary>
    /// Where a damage request came from, so rules can tell a direct weapon blow from a spell, a proc or a damage-over-time tick.
    /// An on-hit proc only reacts to <see cref="DirectWeaponHit"/>, so the explosion it causes never triggers it again.
    /// </summary>
    [Flags]
    public enum DamageProvenance
    {
        None = 0,

        /// <summary>A blow from a weapon swing.</summary>
        DirectWeaponHit = 1,

        /// <summary>Damage from a spell or other ability effect.</summary>
        Spell = 2,

        /// <summary>Damage caused by a triggered rule (an item proc) rather than the player's own action.</summary>
        Proc = 4,

        /// <summary>A periodic tick from a status or hazard.</summary>
        Periodic = 8,

        /// <summary>Damage from the environment, with no attacker.</summary>
        Environmental = 16,
    }

    /// <summary>
    /// A request to damage one entity. All damage goes through <see cref="CombatResolver"/> on the server.
    /// </summary>
    public readonly struct DamageRequest
    {
        /// <summary>The body location of damage that struck no particular part of the body: a spell effect, a damage-over-time tick, a hazard.</summary>
        public const int NoBodyLocation = -1;

        public DamageRequest(
            EntityId target,
            int amount,
            EntityId instigator = default,
            SourceHandle source = default,
            int bodyLocation = NoBodyLocation,
            DamageProvenance provenance = DamageProvenance.None,
            ExecutionId execution = default)
        {
            Target = target;
            Amount = amount;
            Instigator = instigator;
            Source = source;
            BodyLocation = bodyLocation;
            Provenance = provenance;
            Execution = execution;
        }

        /// <summary>The entity being damaged.</summary>
        public EntityId Target { get; }

        /// <summary>The damage to deal. Zero or less deals none.</summary>
        public int Amount { get; }

        /// <summary>The entity credited with the damage, if any. A hazard has none.</summary>
        public EntityId Instigator { get; }

        /// <summary>What the damage came from: a weapon, a spell's item, a trap.</summary>
        public SourceHandle Source { get; }

        /// <summary>
        /// The body location hit, as the integer value of the game's own body location enum, or <see cref="NoBodyLocation"/>. Core only passes it
        /// to a target's <see cref="IRegionDamageModifier"/>; it does not interpret it.
        /// </summary>
        public int BodyLocation { get; }

        public DamageProvenance Provenance { get; }

        /// <summary>The ability execution this damage belongs to, if any.</summary>
        public ExecutionId Execution { get; }

        /// <summary>The same request with a different amount, which keeps its attribution, source, location and provenance.</summary>
        public DamageRequest WithAmount(int amount) => new DamageRequest(Target, amount, Instigator, Source, BodyLocation, Provenance, Execution);
    }
}
