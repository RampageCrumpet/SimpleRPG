using System;

namespace SimpleRPG.Gameplay
{
    /// <summary>
    /// Identifies a networked entity. Zero means no entity. For networked objects this is the NetworkObjectId.
    /// </summary>
    public readonly struct EntityId : IEquatable<EntityId>
    {
        public EntityId(ulong value)
        {
            Value = value;
        }

        public ulong Value { get; }

        public bool IsValid => Value != 0;

        public bool Equals(EntityId other) => Value == other.Value;

        public override bool Equals(object obj) => obj is EntityId other && Equals(other);

        public override int GetHashCode() => Value.GetHashCode();

        public override string ToString() => "Entity#" + Value;

        public static bool operator ==(EntityId a, EntityId b) => a.Equals(b);

        public static bool operator !=(EntityId a, EntityId b) => !a.Equals(b);
    }

    /// <summary>
    /// Identifies one ability execution (and everything it causes) for the length of a session. Zero means none.
    /// </summary>
    public readonly struct ExecutionId : IEquatable<ExecutionId>
    {
        public ExecutionId(uint value)
        {
            Value = value;
        }

        public uint Value { get; }

        public bool IsValid => Value != 0;

        public bool Equals(ExecutionId other) => Value == other.Value;

        public override bool Equals(object obj) => obj is ExecutionId other && Equals(other);

        public override int GetHashCode() => Value.GetHashCode();

        public override string ToString() => "Execution#" + Value;

        public static bool operator ==(ExecutionId a, ExecutionId b) => a.Equals(b);

        public static bool operator !=(ExecutionId a, ExecutionId b) => !a.Equals(b);
    }

    /// <summary>
    /// Identifies one applied status instance (or stack) for the length of a session. Zero means none.
    /// </summary>
    public readonly struct StatusInstanceId : IEquatable<StatusInstanceId>
    {
        public StatusInstanceId(uint value)
        {
            Value = value;
        }

        public uint Value { get; }

        public bool IsValid => Value != 0;

        public bool Equals(StatusInstanceId other) => Value == other.Value;

        public override bool Equals(object obj) => obj is StatusInstanceId other && Equals(other);

        public override int GetHashCode() => Value.GetHashCode();

        public override string ToString() => "Status#" + Value;

        public static bool operator ==(StatusInstanceId a, StatusInstanceId b) => a.Equals(b);

        public static bool operator !=(StatusInstanceId a, StatusInstanceId b) => !a.Equals(b);
    }

    /// <summary>
    /// Identifies one item instance (one charged staff, one equipped sword) as opposed to its item definition. Zero means none.
    /// </summary>
    public readonly struct ItemInstanceId : IEquatable<ItemInstanceId>
    {
        public ItemInstanceId(ulong value)
        {
            Value = value;
        }

        public ulong Value { get; }

        public bool IsValid => Value != 0;

        public bool Equals(ItemInstanceId other) => Value == other.Value;

        public override bool Equals(object obj) => obj is ItemInstanceId other && Equals(other);

        public override int GetHashCode() => Value.GetHashCode();

        public override string ToString() => "Item#" + Value;

        public static bool operator ==(ItemInstanceId a, ItemInstanceId b) => a.Equals(b);

        public static bool operator !=(ItemInstanceId a, ItemInstanceId b) => !a.Equals(b);
    }

    /// <summary>
    /// Identifies one death: the entity and which of its lives ended. A revived entity starts a new life, so it can die again with a new ID.
    /// </summary>
    public readonly struct DeathTransitionId : IEquatable<DeathTransitionId>
    {
        public DeathTransitionId(EntityId entity, int lifeId)
        {
            Entity = entity;
            LifeId = lifeId;
        }

        public EntityId Entity { get; }

        public int LifeId { get; }

        public bool Equals(DeathTransitionId other) => Entity == other.Entity && LifeId == other.LifeId;

        public override bool Equals(object obj) => obj is DeathTransitionId other && Equals(other);

        public override int GetHashCode() => HashCode.Combine(Entity, LifeId);

        public override string ToString() => Entity + "/Life" + LifeId;

        public static bool operator ==(DeathTransitionId a, DeathTransitionId b) => a.Equals(b);

        public static bool operator !=(DeathTransitionId a, DeathTransitionId b) => !a.Equals(b);
    }

    /// <summary>
    /// Hands out increasing session-scoped IDs. Zero is never returned because it means "none". Not thread safe; the server simulation owns one.
    /// </summary>
    public sealed class IdCounter
    {
        private ulong last;

        /// <summary>
        /// Gets the next ID.
        /// </summary>
        public ulong Next() => ++last;
    }
}
