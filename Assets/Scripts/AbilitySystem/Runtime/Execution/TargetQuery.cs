using SimpleRPG.Gameplay;
using System.Collections.Generic;

namespace SimpleRPG.Abilities
{
    /// <summary>
    /// Finds entities near a point. The game integration or the standard <see cref="CombatantTargetQuery"/> implements it.
    /// </summary>
    public interface ITargetQuery
    {
        /// <summary>
        /// Adds every entity within <paramref name="radius"/> of <paramref name="center"/> to <paramref name="results"/>, in order of entity ID so the
        /// result never depends on the order things were found in.
        /// </summary>
        /// <param name="aliveOnly">True to leave out entities that are dead.</param>
        void EntitiesInRadius(Position3 center, float radius, bool aliveOnly, List<EntityId> results);
    }

    /// <summary>
    /// Selects combatants by their position, measured from each entity's own position (for a character, where it stands).
    /// </summary>
    public sealed class CombatantTargetQuery : ITargetQuery
    {
        private readonly CombatantRegistry combatants;

        public CombatantTargetQuery(CombatantRegistry combatants)
        {
            this.combatants = combatants;
        }

        /// <inheritdoc/>
        public void EntitiesInRadius(Position3 center, float radius, bool aliveOnly, List<EntityId> results)
        {
            float radiusSquared = radius * radius;
            int start = results.Count;

            foreach (ICombatant combatant in combatants.All)
            {
                if (aliveOnly && !combatant.IsAlive)
                {
                    continue;
                }

                Position3 position = combatant.Position;
                float dx = position.X - center.X;
                float dy = position.Y - center.Y;
                float dz = position.Z - center.Z;
                if (dx * dx + dy * dy + dz * dz <= radiusSquared)
                {
                    results.Add(combatant.Id);
                }
            }

            results.Sort(start, results.Count - start, EntityIdComparer.Instance);
        }

        private sealed class EntityIdComparer : IComparer<EntityId>
        {
            public static readonly EntityIdComparer Instance = new EntityIdComparer();

            public int Compare(EntityId a, EntityId b) => a.Value.CompareTo(b.Value);
        }
    }
}
