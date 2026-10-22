using UnityEngine;

namespace SimpleRPG.Abilities
{
    /// <summary>
    /// Which applications of a status share a group of stacks.
    /// </summary>
    public enum StatusGrouping : byte
    {
        /// <summary>One group per status on a target, whoever applied it.</summary>
        PerStatus = 0,

        /// <summary>One group per source, so each item or caster keeps its own stacks.</summary>
        PerSource = 1,

        /// <summary>One group per instigator.</summary>
        PerInstigator = 2,
    }

    /// <summary>
    /// What happens when a status is applied to a target that already has it.
    /// </summary>
    public enum ReapplicationPolicy : byte
    {
        /// <summary>Add a stack, up to the limit.</summary>
        AddStack = 0,

        /// <summary>No new stack; only refresh the timers.</summary>
        RefreshOnly = 1,

        /// <summary>End the old status and start the new application fresh.</summary>
        Replace = 2,

        /// <summary>Nothing happens.</summary>
        Ignore = 3,
    }

    /// <summary>
    /// How the stacks of a group time out.
    /// </summary>
    public enum DurationPolicy : byte
    {
        /// <summary>The whole group has one timer and ends together.</summary>
        SharedDuration = 0,

        /// <summary>Each stack has its own timer and ends on its own.</summary>
        IndependentPerStack = 1,
    }

    /// <summary>
    /// Which timers a reapplication restarts.
    /// </summary>
    public enum RefreshPolicy : byte
    {
        /// <summary>None.</summary>
        None = 0,

        /// <summary>Every existing stack. With a shared duration, the shared timer.</summary>
        All = 1,

        /// <summary>The most recently applied existing stack. With a shared duration, the shared timer.</summary>
        Newest = 2,

        /// <summary>The oldest existing stack. With a shared duration, the shared timer.</summary>
        Oldest = 3,
    }

    /// <summary>
    /// How the strength of several stacks combines into one tick.
    /// </summary>
    public enum MagnitudePolicy : byte
    {
        /// <summary>The stacks' strengths add up.</summary>
        Additive = 0,

        /// <summary>Only the strongest stack counts.</summary>
        StrongestOnly = 1,
    }

    /// <summary>
    /// What happens when a stack is applied but the group is already full.
    /// </summary>
    public enum OverflowPolicy : byte
    {
        /// <summary>The application is lost.</summary>
        Ignore = 0,

        /// <summary>No new stack, but the timers are refreshed as they would be for a reapplication.</summary>
        Refresh = 1,

        /// <summary>The oldest stack is replaced by the new one.</summary>
        ReplaceOldest = 2,

        /// <summary>The weakest stack is replaced by the new one, if the new one is at least as strong.</summary>
        ReplaceWeakest = 3,
    }

    /// <summary>
    /// How a status with several stacks ticks.
    /// </summary>
    public enum PeriodicMode : byte
    {
        /// <summary>One tick for the group, using the combined strength.</summary>
        Combined = 0,

        /// <summary>Every stack ticks on its own schedule with its own strength.</summary>
        PerStack = 1,
    }

    /// <summary>
    /// A status effect such as Burning or Poison: how it stacks, how long it lasts and what it does each tick. The status is the same asset for
    /// every target; what is on a target at the moment lives in that target's status record. Nothing here is code: a new status is new data.
    /// </summary>
    [CreateAssetMenu(fileName = "Status", menuName = "SimpleRPG/Status")]
    public class StatusDefinition : GameplayDefinition
    {
        [SerializeField]
        [Tooltip("The name shown to the player.")]
        private string statusName = "New Status";

        [SerializeField]
        [Tooltip("The icon shown on the afflicted character.")]
        private Sprite icon;

        [Header("Stacking")]
        [SerializeField]
        [Tooltip("Which applications share a group of stacks.")]
        private StatusGrouping grouping = StatusGrouping.PerStatus;

        [SerializeField]
        [Tooltip("What happens when the status is applied to a target that already has it.")]
        private ReapplicationPolicy reapplication = ReapplicationPolicy.AddStack;

        [SerializeField]
        [Min(1)]
        [Tooltip("The most stacks one group can hold.")]
        private int maxStacks = 5;

        [SerializeField]
        [Tooltip("What happens when a stack is applied to a full group.")]
        private OverflowPolicy overflow = OverflowPolicy.Refresh;

        [Header("Duration")]
        [SerializeField]
        [Min(0.05f)]
        [Tooltip("How long a stack lasts, in seconds.")]
        private float durationSeconds = 5f;

        [SerializeField]
        [Tooltip("Whether the group shares one timer or each stack has its own.")]
        private DurationPolicy duration = DurationPolicy.SharedDuration;

        [SerializeField]
        [Tooltip("Which timers a reapplication restarts.")]
        private RefreshPolicy refresh = RefreshPolicy.All;

        [Header("Strength and ticks")]
        [SerializeField]
        [Tooltip("How several stacks combine into one tick.")]
        private MagnitudePolicy magnitude = MagnitudePolicy.Additive;

        [SerializeField]
        [Min(0f)]
        [Tooltip("Seconds between ticks. Zero means the status never ticks.")]
        private float tickIntervalSeconds = 1f;

        [SerializeField]
        [Tooltip("Whether the group ticks once, or every stack ticks on its own.")]
        private PeriodicMode periodic = PeriodicMode.Combined;

        [SerializeField]
        [Tooltip("The ability that runs on each tick. It starts from a Status Tick node and knows the holder, the strength and the stack count.")]
        private AbilityDefinition tickAbility;

        public string StatusName => statusName;

        public Sprite Icon => icon;

        public StatusGrouping Grouping => grouping;

        public ReapplicationPolicy Reapplication => reapplication;

        public int MaxStacks => Mathf.Max(1, maxStacks);

        public OverflowPolicy Overflow => overflow;

        public float DurationSeconds => durationSeconds;

        public DurationPolicy Duration => duration;

        public RefreshPolicy Refresh => refresh;

        public MagnitudePolicy Magnitude => magnitude;

        public float TickIntervalSeconds => tickIntervalSeconds;

        public PeriodicMode Periodic => periodic;

        public AbilityDefinition TickAbility => tickAbility;

        /// <summary>
        /// Sets the status up in code. Used by tools and tests that build statuses without the inspector.
        /// </summary>
        public void Configure(
            string name,
            float durationSeconds,
            float tickIntervalSeconds,
            AbilityDefinition tickAbility = null,
            StatusGrouping grouping = StatusGrouping.PerStatus,
            ReapplicationPolicy reapplication = ReapplicationPolicy.AddStack,
            int maxStacks = 5,
            OverflowPolicy overflow = OverflowPolicy.Refresh,
            DurationPolicy duration = DurationPolicy.SharedDuration,
            RefreshPolicy refresh = RefreshPolicy.All,
            MagnitudePolicy magnitude = MagnitudePolicy.Additive,
            PeriodicMode periodic = PeriodicMode.Combined)
        {
            statusName = name;
            this.durationSeconds = durationSeconds;
            this.tickIntervalSeconds = tickIntervalSeconds;
            this.tickAbility = tickAbility;
            this.grouping = grouping;
            this.reapplication = reapplication;
            this.maxStacks = maxStacks;
            this.overflow = overflow;
            this.duration = duration;
            this.refresh = refresh;
            this.magnitude = magnitude;
            this.periodic = periodic;
        }
    }
}
