using SimpleRPG.Gameplay;

namespace SimpleRPG.Abilities
{
    /// <summary>
    /// What a trigger carried: the facts conditions look at and the ability is given. Built by the engine from the event, never by content.
    /// </summary>
    public readonly struct TriggerContext
    {
        /// <summary>The body location of a trigger that struck no particular part.</summary>
        public const int NoBodyLocation = DamageRequest.NoBodyLocation;

        public TriggerContext(EffectTrigger trigger, EntityId owner, EntityId other, SourceHandle source, int bodyLocation, int amount, bool fatal, Position3 position)
        {
            Trigger = trigger;
            Owner = owner;
            Other = other;
            Source = source;
            BodyLocation = bodyLocation;
            Amount = amount;
            Fatal = fatal;
            Position = position;
        }

        public EffectTrigger Trigger { get; }

        /// <summary>The entity the effect belongs to.</summary>
        public EntityId Owner { get; }

        /// <summary>The other party, or none.</summary>
        public EntityId Other { get; }

        /// <summary>What did it: the item or ability behind the damage, if any. An effect with <see cref="EffectScope.Host"/> reacts only to its own host.</summary>
        public SourceHandle Source { get; }

        /// <summary>The body location struck, as the integer value of the game's own enum, or <see cref="NoBodyLocation"/>.</summary>
        public int BodyLocation { get; }

        /// <summary>The health the blow actually took, after defences. Zero for a trigger that is not damage.</summary>
        public int Amount { get; }

        /// <summary>Whether this blow killed.</summary>
        public bool Fatal { get; }

        /// <summary>Where it happened (a death's position).</summary>
        public Position3 Position { get; }
    }
}
