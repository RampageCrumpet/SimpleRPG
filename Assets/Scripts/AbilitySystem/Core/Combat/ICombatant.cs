using System.Collections.Generic;

namespace SimpleRPG.Gameplay
{
    /// <summary>
    /// A position in the world, so Core does not depend on the engine's vector type.
    /// </summary>
    public readonly struct Position3
    {
        public Position3(float x, float y, float z)
        {
            X = x;
            Y = y;
            Z = z;
        }

        public float X { get; }

        public float Y { get; }

        public float Z { get; }

        public override string ToString() => "(" + X + ", " + Y + ", " + Z + ")";
    }

    /// <summary>
    /// Something that can be damaged and die. The game's character implements this; <see cref="CombatResolver"/> decides what happens and
    /// the combatant only stores the result. The Commit methods run on the server.
    /// </summary>
    public interface ICombatant
    {
        EntityId Id { get; }

        int Health { get; }

        bool IsAlive { get; }

        /// <summary>
        /// Which life this is, starting at 1. Reviving starts the next life, so each death has a distinct <see cref="DeathTransitionId"/>.
        /// </summary>
        int LifeId { get; }

        Position3 Position { get; }

        /// <summary>Stores the new health.</summary>
        void CommitHealth(int health);

        /// <summary>Marks the combatant dead. Called exactly once per life.</summary>
        void CommitDeath();

        /// <summary>Starts the next life alive with the given health.</summary>
        void CommitRevive(int health);
    }

    /// <summary>
    /// Finds combatants by entity ID.
    /// </summary>
    public interface ICombatantRegistry
    {
        bool TryGet(EntityId id, out ICombatant combatant);
    }

    /// <summary>
    /// The standard in-memory registry of combatants.
    /// </summary>
    public sealed class CombatantRegistry : ICombatantRegistry
    {
        private readonly Dictionary<EntityId, ICombatant> combatants = new Dictionary<EntityId, ICombatant>();

        public void Register(ICombatant combatant)
        {
            combatants[combatant.Id] = combatant;
        }

        /// <summary>
        /// Removes a combatant, but only if the registered one is this one, so a stale unregister can't remove a newer holder of the same ID.
        /// </summary>
        public void Unregister(ICombatant combatant)
        {
            Unregister(combatant.Id, combatant);
        }

        /// <summary>
        /// Removes a combatant by the ID it was registered under, for a combatant whose own ID is already gone (a character that has despawned).
        /// </summary>
        public void Unregister(EntityId id, ICombatant combatant)
        {
            if (combatants.TryGetValue(id, out ICombatant registered) && ReferenceEquals(registered, combatant))
            {
                combatants.Remove(id);
            }
        }

        public bool TryGet(EntityId id, out ICombatant combatant) => combatants.TryGetValue(id, out combatant);

        public int Count => combatants.Count;

        /// <summary>
        /// Every registered combatant, in no particular order.
        /// </summary>
        public IEnumerable<ICombatant> All => combatants.Values;
    }
}
