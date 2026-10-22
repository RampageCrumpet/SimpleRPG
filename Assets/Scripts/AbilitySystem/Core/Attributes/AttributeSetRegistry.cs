using System.Collections.Generic;

namespace SimpleRPG.Gameplay
{
    /// <summary>
    /// Finds the <see cref="AttributeSet"/> of each source: an entity's own stats, an item's fixed stats, a world object's constants.
    /// A source with no registered set has no attributes, and bindings that read it use their fallback.
    /// </summary>
    public sealed class AttributeSetRegistry : IAttributeLookup
    {
        private readonly Dictionary<SourceHandle, AttributeSet> sets = new Dictionary<SourceHandle, AttributeSet>();

        /// <summary>
        /// Gives a source its attributes, replacing any it had.
        /// </summary>
        public void Register(SourceHandle source, AttributeSet attributes)
        {
            sets[source] = attributes;
        }

        /// <summary>
        /// Gets a source's attributes, giving it an empty set if it had none.
        /// </summary>
        public AttributeSet GetOrCreate(SourceHandle source)
        {
            if (!sets.TryGetValue(source, out AttributeSet set))
            {
                set = new AttributeSet();
                sets.Add(source, set);
            }

            return set;
        }

        /// <summary>
        /// Gets a source's attributes, if it has any.
        /// </summary>
        public bool TryGetSet(SourceHandle source, out AttributeSet set) => sets.TryGetValue(source, out set);

        /// <summary>
        /// Removes a source's attributes, for example when an entity despawns.
        /// </summary>
        public bool Unregister(SourceHandle source) => sets.Remove(source);

        /// <inheritdoc/>
        public bool TryGetAttributes(SourceHandle source, out IAttributeReader attributes)
        {
            bool found = sets.TryGetValue(source, out AttributeSet set);
            attributes = set;
            return found;
        }

        public int Count => sets.Count;
    }

    /// <summary>
    /// What a projectile hit, reported by the projectile's delivery to the ability that launched it.
    /// </summary>
    public readonly struct ProjectileImpact
    {
        public ProjectileImpact(EntityId victim, int bodyLocation, Position3 position, bool hitWorld)
        {
            Victim = victim;
            BodyLocation = bodyLocation;
            Position = position;
            HitWorld = hitWorld;
        }

        /// <summary>The entity that was hit, or none if the projectile hit the world.</summary>
        public EntityId Victim { get; }

        /// <summary>The body location hit, as the integer value of the game's body location enum.</summary>
        public int BodyLocation { get; }

        /// <summary>Where the projectile stopped.</summary>
        public Position3 Position { get; }

        /// <summary>True if the projectile hit level geometry rather than an entity.</summary>
        public bool HitWorld { get; }
    }
}
