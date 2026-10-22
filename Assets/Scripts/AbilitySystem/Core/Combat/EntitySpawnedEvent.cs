namespace SimpleRPG.Gameplay
{
    /// <summary>
    /// An ability spawned a new entity into the world: a summoned creature, a demon from a curse.
    /// </summary>
    public sealed class EntitySpawnedEvent : GameplayEvent
    {
        public EntitySpawnedEvent(EntityId entity, EntityId instigator, SourceHandle source, ExecutionId execution, Position3 position)
        {
            Entity = entity;
            Instigator = instigator;
            Source = source;
            Execution = execution;
            Position = position;
        }

        /// <summary>The new entity.</summary>
        public EntityId Entity { get; }

        /// <summary>The entity credited with spawning it, if any. The spawned entity does not depend on it staying alive.</summary>
        public EntityId Instigator { get; }

        /// <summary>What it came from: the item whose rule spawned it, for example.</summary>
        public SourceHandle Source { get; }

        public ExecutionId Execution { get; }

        public Position3 Position { get; }
    }
}
