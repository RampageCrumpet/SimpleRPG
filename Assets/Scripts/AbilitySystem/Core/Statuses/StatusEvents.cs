namespace SimpleRPG.Gameplay
{
    /// <summary>
    /// Why a status, or one of its stacks, ended.
    /// </summary>
    public enum RemovalReason : byte
    {
        /// <summary>It ran out of time.</summary>
        Expired = 0,

        /// <summary>It was dispelled.</summary>
        Dispelled = 1,

        /// <summary>The source that applied it was removed (the item that applied it was taken off).</summary>
        SourceRemoved = 2,

        /// <summary>The entity holding it died.</summary>
        TargetDied = 3,

        /// <summary>A newer application took its place.</summary>
        Replaced = 4,

        /// <summary>Something removed it directly.</summary>
        Explicit = 5,
    }

    /// <summary>
    /// A status appeared on an entity: the first stack of a new group.
    /// </summary>
    public sealed class StatusAppliedEvent : GameplayEvent
    {
        public StatusAppliedEvent(EntityId target, DefinitionId status, StatusInstanceId group, SourceHandle source, EntityId instigator)
        {
            Target = target;
            Status = status;
            Group = group;
            Source = source;
            Instigator = instigator;
        }

        public EntityId Target { get; }

        public DefinitionId Status { get; }

        public StatusInstanceId Group { get; }

        public SourceHandle Source { get; }

        public EntityId Instigator { get; }
    }

    /// <summary>
    /// A stack was added to a status.
    /// </summary>
    public sealed class StatusStackAddedEvent : GameplayEvent
    {
        public StatusStackAddedEvent(EntityId target, DefinitionId status, StatusInstanceId group, StatusInstanceId stack, double potency, int stackCount)
        {
            Target = target;
            Status = status;
            Group = group;
            Stack = stack;
            Potency = potency;
            StackCount = stackCount;
        }

        public EntityId Target { get; }

        public DefinitionId Status { get; }

        public StatusInstanceId Group { get; }

        public StatusInstanceId Stack { get; }

        public double Potency { get; }

        /// <summary>How many stacks the status has now.</summary>
        public int StackCount { get; }
    }

    /// <summary>
    /// A status's timers were restarted without a stack being added, so what it shows (the time left) changed.
    /// </summary>
    public sealed class StatusRefreshedEvent : GameplayEvent
    {
        public StatusRefreshedEvent(EntityId target, DefinitionId status, StatusInstanceId group)
        {
            Target = target;
            Status = status;
            Group = group;
        }

        public EntityId Target { get; }

        public DefinitionId Status { get; }

        public StatusInstanceId Group { get; }
    }

    /// <summary>
    /// A single stack ended while its status carried on, or as the last stack of a status that ended.
    /// </summary>
    public sealed class StatusStackExpiredEvent : GameplayEvent
    {
        public StatusStackExpiredEvent(EntityId target, DefinitionId status, StatusInstanceId group, StatusInstanceId stack, RemovalReason reason, int stacksLeft)
        {
            Target = target;
            Status = status;
            Group = group;
            Stack = stack;
            Reason = reason;
            StacksLeft = stacksLeft;
        }

        public EntityId Target { get; }

        public DefinitionId Status { get; }

        public StatusInstanceId Group { get; }

        public StatusInstanceId Stack { get; }

        public RemovalReason Reason { get; }

        public int StacksLeft { get; }
    }

    /// <summary>
    /// A status's periodic effect ran.
    /// </summary>
    public sealed class StatusTickedEvent : GameplayEvent
    {
        public StatusTickedEvent(EntityId target, DefinitionId status, StatusInstanceId group, EntityId instigator, double potency, int stackCount)
        {
            Target = target;
            Status = status;
            Group = group;
            Instigator = instigator;
            Potency = potency;
            StackCount = stackCount;
        }

        public EntityId Target { get; }

        public DefinitionId Status { get; }

        public StatusInstanceId Group { get; }

        /// <summary>Who the tick's effects are credited to. They are credited even if that entity has since died.</summary>
        public EntityId Instigator { get; }

        public double Potency { get; }

        public int StackCount { get; }
    }

    /// <summary>
    /// A status ended: its last stack is gone.
    /// </summary>
    public sealed class StatusRemovedEvent : GameplayEvent
    {
        public StatusRemovedEvent(EntityId target, DefinitionId status, StatusInstanceId group, RemovalReason reason)
        {
            Target = target;
            Status = status;
            Group = group;
            Reason = reason;
        }

        public EntityId Target { get; }

        public DefinitionId Status { get; }

        public StatusInstanceId Group { get; }

        public RemovalReason Reason { get; }
    }
}
