using SimpleRPG.Gameplay;
using UnityEngine;

namespace SimpleRPG.Abilities
{
    /// <summary>
    /// A request to spawn an entity, made by the SpawnEntity operation.
    /// </summary>
    public readonly struct SpawnRequest
    {
        public SpawnRequest(GameObject prefab, Position3 position, SourceHandle source, SimpleRPG.Gameplay.EntityId instigator, ExecutionId execution)
        {
            Prefab = prefab;
            Position = position;
            Source = source;
            Instigator = instigator;
            Execution = execution;
        }

        public GameObject Prefab { get; }

        public Position3 Position { get; }

        public SourceHandle Source { get; }

        public SimpleRPG.Gameplay.EntityId Instigator { get; }

        public ExecutionId Execution { get; }
    }

    /// <summary>
    /// Spawns entities into the world. The game integration implements it with the game's own spawning, so a spawned creature is an ordinary one
    /// with its own AI, health and lifetime.
    /// </summary>
    public interface ISpawnService
    {
        /// <summary>
        /// Spawns the entity.
        /// </summary>
        /// <returns>False if it could not be spawned.</returns>
        bool Spawn(in SpawnRequest request);
    }
}
