using System;

namespace SimpleRPG.Gameplay
{
    /// <summary>
    /// What kind of thing a <see cref="SourceHandle"/> points at.
    /// </summary>
    public enum SourceKind : byte
    {
        /// <summary>No source.</summary>
        None = 0,

        /// <summary>A character or other entity.</summary>
        Entity = 1,

        /// <summary>An item instance, such as an equipped sword or a charged staff.</summary>
        Item = 2,

        /// <summary>A world object that is not a character: a trap, a brazier, a hazard.</summary>
        WorldObject = 3,

        /// <summary>An applied status instance.</summary>
        Status = 4,
    }

    /// <summary>
    /// Where an ability execution comes from. It is deliberately not assumed to be an entity, so items, traps and natural hazards can all be sources.
    /// The source is distinct from the instigator, who is the entity credited with the result, if there is one.
    /// </summary>
    public readonly struct SourceHandle : IEquatable<SourceHandle>
    {
        private SourceHandle(SourceKind kind, ulong id)
        {
            Kind = kind;
            Id = id;
        }

        /// <summary>
        /// The source with no origin.
        /// </summary>
        public static SourceHandle None => default;

        public SourceKind Kind { get; }

        /// <summary>
        /// The raw ID, interpreted according to <see cref="Kind"/>.
        /// </summary>
        public ulong Id { get; }

        public static SourceHandle FromEntity(EntityId entity) => new SourceHandle(SourceKind.Entity, entity.Value);

        public static SourceHandle FromItem(ItemInstanceId item) => new SourceHandle(SourceKind.Item, item.Value);

        public static SourceHandle FromWorldObject(ulong worldObjectId) => new SourceHandle(SourceKind.WorldObject, worldObjectId);

        public static SourceHandle FromStatus(StatusInstanceId status) => new SourceHandle(SourceKind.Status, status.Value);

        public bool TryGetEntity(out EntityId entity)
        {
            entity = Kind == SourceKind.Entity ? new EntityId(Id) : default;
            return Kind == SourceKind.Entity;
        }

        public bool TryGetItem(out ItemInstanceId item)
        {
            item = Kind == SourceKind.Item ? new ItemInstanceId(Id) : default;
            return Kind == SourceKind.Item;
        }

        public bool TryGetStatus(out StatusInstanceId status)
        {
            status = Kind == SourceKind.Status ? new StatusInstanceId((uint)Id) : default;
            return Kind == SourceKind.Status;
        }

        public bool Equals(SourceHandle other) => Kind == other.Kind && Id == other.Id;

        public override bool Equals(object obj) => obj is SourceHandle other && Equals(other);

        public override int GetHashCode() => HashCode.Combine(Kind, Id);

        public override string ToString() => Kind == SourceKind.None ? "Source(None)" : "Source(" + Kind + "#" + Id + ")";

        public static bool operator ==(SourceHandle a, SourceHandle b) => a.Equals(b);

        public static bool operator !=(SourceHandle a, SourceHandle b) => !a.Equals(b);
    }
}
