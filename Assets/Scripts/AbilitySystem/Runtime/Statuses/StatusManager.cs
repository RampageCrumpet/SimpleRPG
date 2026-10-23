using SimpleRPG.Gameplay;
using System;
using System.Collections.Generic;
using System.Linq;

namespace SimpleRPG.Abilities
{
    /// <summary>
    /// What came of applying a status.
    /// </summary>
    public enum StatusApplyResult : byte
    {
        /// <summary>The target had no such status; a new one started.</summary>
        Started = 0,

        /// <summary>A stack was added to the existing status.</summary>
        StackAdded = 1,

        /// <summary>No stack was added, but the timers were refreshed.</summary>
        Refreshed = 2,

        /// <summary>The old status was ended and the new application took its place.</summary>
        Replaced = 3,

        /// <summary>The application had no effect.</summary>
        Ignored = 4,

        /// <summary>The target cannot be afflicted: it is unknown or dead.</summary>
        RejectedTarget = 5,
    }

    /// <summary>
    /// A request to put a status on an entity.
    /// </summary>
    public readonly struct StatusApplication
    {
        public StatusApplication(EntityId target, StatusDefinition status, SourceHandle source, EntityId instigator, double potency)
        {
            Target = target;
            Status = status;
            Source = source;
            Instigator = instigator;
            Potency = potency;
        }

        public EntityId Target { get; }

        public StatusDefinition Status { get; }

        /// <summary>Where the application came from: the item or entity that afflicted the target.</summary>
        public SourceHandle Source { get; }

        /// <summary>The entity credited with the status's effects, if any.</summary>
        public EntityId Instigator { get; }

        /// <summary>The strength the stack was applied with. It is fixed for the life of the stack.</summary>
        public double Potency { get; }
    }

    /// <summary>
    /// Applies statuses. The status manager implements it.
    /// </summary>
    public interface IStatusService
    {
        StatusApplyResult Apply(in StatusApplication application);
    }

    /// <summary>
    /// One stack of a status as it is right now, for display and tests.
    /// </summary>
    public readonly struct StackSnapshot
    {
        public StackSnapshot(StatusInstanceId id, double potency, long appliedTick, long expiryTick, SourceHandle source, EntityId instigator)
        {
            Id = id;
            Potency = potency;
            AppliedTick = appliedTick;
            ExpiryTick = expiryTick;
            Source = source;
            Instigator = instigator;
        }

        public StatusInstanceId Id { get; }

        public double Potency { get; }

        public long AppliedTick { get; }

        public long ExpiryTick { get; }

        public SourceHandle Source { get; }

        public EntityId Instigator { get; }
    }

    /// <summary>
    /// A status on an entity as it is right now, for display and tests.
    /// </summary>
    public sealed class StatusSnapshot
    {
        public StatusSnapshot(StatusInstanceId group, DefinitionId status, IReadOnlyList<StackSnapshot> stacks, long sharedExpiryTick, double effectivePotency)
        {
            Group = group;
            Status = status;
            Stacks = stacks;
            SharedExpiryTick = sharedExpiryTick;
            EffectivePotency = effectivePotency;
        }

        public StatusInstanceId Group { get; }

        public DefinitionId Status { get; }

        public IReadOnlyList<StackSnapshot> Stacks { get; }

        public int StackCount => Stacks.Count;

        /// <summary>When the whole group ends, for a shared duration. -1 when each stack has its own timer.</summary>
        public long SharedExpiryTick { get; }

        /// <summary>The strength a combined tick would have right now.</summary>
        public double EffectivePotency { get; }
    }

    /// <summary>
    /// Holds the statuses on every entity and runs them: stacking, timers and periodic effects. Server only. Time comes from the gameplay clock, so
    /// nothing here depends on frame rate. Statuses are only ever changed through this class, never by editing their stacks directly.
    /// </summary>
    public sealed class StatusManager : IStatusService, IDisposable
    {
        private sealed class Stack
        {
            public StatusInstanceId Id;
            public long AppliedTick;
            public long ExpiryTick;
            public long NextTick;
            public SourceHandle Source;
            public EntityId Instigator;
            public double Potency;
            public List<ModifierHandle> StatHandles;
        }

        private sealed class Group
        {
            public StatusInstanceId Id;
            public StatusDefinition Definition;
            public DefinitionId DefinitionId;
            public EntityId Target;
            public SourceHandle KeySource;
            public EntityId KeyInstigator;
            public long SharedExpiry;
            public long NextTick;
            public bool Removed;
            public readonly List<Stack> Stacks = new List<Stack>();
        }

        private readonly IGameplayClock clock;
        private readonly GameplayEventDispatcher events;
        private readonly ICombatantRegistry combatants;
        private readonly AttributeSetRegistry attributes;
        private readonly IdCounter ids = new IdCounter();
        private readonly Dictionary<EntityId, List<Group>> byTarget = new Dictionary<EntityId, List<Group>>();
        private readonly List<SubscriptionHandle> subscriptions = new List<SubscriptionHandle>();
        private long lastProcessed = long.MinValue;

        public StatusManager(IGameplayClock clock, GameplayEventDispatcher events, ICombatantRegistry combatants, AttributeSetRegistry attributes = null)
        {
            this.attributes = attributes;
            this.clock = clock ?? throw new ArgumentNullException(nameof(clock));
            this.events = events ?? throw new ArgumentNullException(nameof(events));
            this.combatants = combatants ?? throw new ArgumentNullException(nameof(combatants));

            // Statuses end with the entity that holds them, after the death's own reactions have run.
            subscriptions.Add(events.Subscribe<DeathFinalizeEvent>(e => RemoveAll(e.Context.Victim, RemovalReason.TargetDied)));
            subscriptions.Add(events.Subscribe<EntityRemovedEvent>(e => Forget(e.Entity)));
        }

        /// <summary>
        /// Runs each status's tick ability. Set once the executor exists.
        /// </summary>
        public AbilityExecutor Executor { get; set; }

        /// <summary>
        /// Raised with a description when a status cannot do something, for example its tick ability has errors.
        /// </summary>
        public event Action<string> Diagnostic;

        public void Dispose()
        {
            foreach (List<Group> groups in byTarget.Values)
            {
                foreach (Group group in groups) WithdrawAllStatModifiers(group);
            }

            foreach (SubscriptionHandle handle in subscriptions) events.Unsubscribe(handle);
            subscriptions.Clear();
            byTarget.Clear();
        }

        // ----- Queries -----

        /// <summary>
        /// The number of different statuses on an entity.
        /// </summary>
        public int GroupCount(EntityId target) => byTarget.TryGetValue(target, out List<Group> groups) ? groups.Count : 0;

        /// <summary>
        /// Gets the groups of one status on an entity. A status with per-source or per-instigator grouping can have several.
        /// </summary>
        public IReadOnlyList<StatusSnapshot> GetStatuses(EntityId target, StatusDefinition status)
        {
            if (!byTarget.TryGetValue(target, out List<Group> groups))
            {
                return Array.Empty<StatusSnapshot>();
            }

            return groups.Where(g => g.Definition == status).Select(Snapshot).ToList();
        }

        /// <summary>
        /// Gets the single group of a status on an entity, for a status that has one.
        /// </summary>
        public bool TryGetStatus(EntityId target, StatusDefinition status, out StatusSnapshot snapshot)
        {
            IReadOnlyList<StatusSnapshot> all = GetStatuses(target, status);
            snapshot = all.Count > 0 ? all[0] : null;
            return snapshot != null;
        }

        public bool HasStatus(EntityId target, StatusDefinition status) => GetStatuses(target, status).Count > 0;

        // ----- Applying -----

        /// <inheritdoc/>
        public StatusApplyResult Apply(in StatusApplication application)
        {
            StatusDefinition definition = application.Status;
            if (definition == null || !application.Target.IsValid || !combatants.TryGet(application.Target, out ICombatant holder) || !holder.IsAlive)
            {
                return StatusApplyResult.RejectedTarget;
            }

            long now = clock.Tick;
            Group group = FindGroup(application);
            if (group == null)
            {
                group = NewGroup(application, now);
                AddStack(group, application, now);
                RaiseApplied(group, application);
                RaiseStackAdded(group, group.Stacks[group.Stacks.Count - 1]);
                Drain();
                return StatusApplyResult.Started;
            }

            StatusApplyResult result = Reapply(group, application, now);
            Drain();
            return result;
        }

        private StatusApplyResult Reapply(Group group, in StatusApplication application, long now)
        {
            StatusDefinition definition = group.Definition;

            switch (definition.Reapplication)
            {
                case ReapplicationPolicy.Ignore:
                    return StatusApplyResult.Ignored;

                case ReapplicationPolicy.Replace:
                    RemoveGroup(group, RemovalReason.Replaced);
                    Group fresh = NewGroup(application, now);
                    AddStack(fresh, application, now);
                    RaiseApplied(fresh, application);
                    RaiseStackAdded(fresh, fresh.Stacks[fresh.Stacks.Count - 1]);
                    return StatusApplyResult.Replaced;

                case ReapplicationPolicy.RefreshOnly:
                    Refresh(group, definition.Refresh, now);
                    return StatusApplyResult.Refreshed;

                default:
                    break;
            }

            if (group.Stacks.Count < definition.MaxStacks)
            {
                Refresh(group, definition.Refresh, now);
                Stack added = AddStack(group, application, now);
                RaiseStackAdded(group, added);
                return StatusApplyResult.StackAdded;
            }

            switch (definition.Overflow)
            {
                case OverflowPolicy.Refresh:
                    Refresh(group, definition.Refresh, now);
                    return StatusApplyResult.Refreshed;

                case OverflowPolicy.ReplaceOldest:
                    RemoveStack(group, Oldest(group), RemovalReason.Replaced);
                    return AddAfterReplacing(group, application, now);

                case OverflowPolicy.ReplaceWeakest:
                    Stack weakest = group.Stacks.OrderBy(s => s.Potency).ThenBy(s => s.AppliedTick).ThenBy(s => s.Id.Value).First();
                    if (application.Potency < weakest.Potency)
                    {
                        return StatusApplyResult.Ignored;
                    }

                    RemoveStack(group, weakest, RemovalReason.Replaced);
                    return AddAfterReplacing(group, application, now);

                default:
                    return StatusApplyResult.Ignored;
            }
        }

        private StatusApplyResult AddAfterReplacing(Group group, in StatusApplication application, long now)
        {
            // Replacing the only stack of a one-stack status ended the whole group, so the new application starts a new one.
            if (group.Removed)
            {
                Group fresh = NewGroup(application, now);
                AddStack(fresh, application, now);
                RaiseApplied(fresh, application);
                RaiseStackAdded(fresh, fresh.Stacks[fresh.Stacks.Count - 1]);
                return StatusApplyResult.StackAdded;
            }

            Refresh(group, group.Definition.Refresh, now);
            Stack added = AddStack(group, application, now);
            RaiseStackAdded(group, added);
            return StatusApplyResult.StackAdded;
        }

        private Group FindGroup(in StatusApplication application)
        {
            if (!byTarget.TryGetValue(application.Target, out List<Group> groups))
            {
                return null;
            }

            StatusGrouping grouping = application.Status.Grouping;
            foreach (Group group in groups)
            {
                if (group.Definition != application.Status) continue;
                if (grouping == StatusGrouping.PerStatus) return group;
                if (grouping == StatusGrouping.PerSource && group.KeySource == application.Source) return group;
                if (grouping == StatusGrouping.PerInstigator && group.KeyInstigator == application.Instigator) return group;
            }

            return null;
        }

        private Group NewGroup(in StatusApplication application, long now)
        {
            StatusDefinition definition = application.Status;
            Group group = new Group
            {
                Id = NewId(),
                Definition = definition,
                DefinitionId = definition.Id,
                Target = application.Target,
                KeySource = definition.Grouping == StatusGrouping.PerSource ? application.Source : default,
                KeyInstigator = definition.Grouping == StatusGrouping.PerInstigator ? application.Instigator : default,
                SharedExpiry = now + DurationTicks(definition),
                NextTick = IntervalTicks(definition) > 0 ? now + IntervalTicks(definition) : long.MaxValue,
            };

            if (!byTarget.TryGetValue(application.Target, out List<Group> groups))
            {
                groups = new List<Group>();
                byTarget.Add(application.Target, groups);
            }

            groups.Add(group);
            return group;
        }

        private Stack AddStack(Group group, in StatusApplication application, long now)
        {
            StatusDefinition definition = group.Definition;
            long interval = IntervalTicks(definition);
            Stack stack = new Stack
            {
                Id = NewId(),
                AppliedTick = now,
                ExpiryTick = definition.Duration == DurationPolicy.SharedDuration ? group.SharedExpiry : now + DurationTicks(definition),
                NextTick = interval > 0 ? now + interval : long.MaxValue,
                Source = application.Source,
                Instigator = application.Instigator,
                Potency = application.Potency,
            };

            group.Stacks.Add(stack);
            ReconcileStatModifiers(group);
            return stack;
        }

        /// <summary>
        /// Restarts timers as the policy says. With a shared duration any refresh restarts the one shared timer; otherwise it restarts the chosen stacks.
        /// </summary>
        private void Refresh(Group group, RefreshPolicy policy, long now)
        {
            if (policy == RefreshPolicy.None || group.Stacks.Count == 0)
            {
                return;
            }

            long expiry = now + DurationTicks(group.Definition);
            if (group.Definition.Duration == DurationPolicy.SharedDuration)
            {
                group.SharedExpiry = expiry;
                foreach (Stack stack in group.Stacks) stack.ExpiryTick = expiry;
                return;
            }

            switch (policy)
            {
                case RefreshPolicy.All:
                    foreach (Stack stack in group.Stacks) stack.ExpiryTick = expiry;
                    break;

                case RefreshPolicy.Newest:
                    group.Stacks.OrderByDescending(s => s.AppliedTick).ThenByDescending(s => s.Id.Value).First().ExpiryTick = expiry;
                    break;

                case RefreshPolicy.Oldest:
                    Oldest(group).ExpiryTick = expiry;
                    break;
            }
        }

        private static Stack Oldest(Group group) => group.Stacks.OrderBy(s => s.AppliedTick).ThenBy(s => s.Id.Value).First();

        // ----- Stat modifiers -----

        /// <summary>
        /// Makes the modifiers on the holder match the stacks that should count: every stack for an additive status, only the strongest otherwise.
        /// Each stack owns the handles of what it added, so exactly that contribution is withdrawn and nothing is added twice.
        /// </summary>
        private void ReconcileStatModifiers(Group group)
        {
            if (attributes == null || group.Definition.StatModifiers.Count == 0 || group.Removed)
            {
                return;
            }

            Stack strongest = group.Stacks.Count == 0 ? null : group.Stacks.OrderByDescending(s => s.Potency).ThenBy(s => s.AppliedTick).ThenBy(s => s.Id.Value).First();
            foreach (Stack stack in group.Stacks)
            {
                bool counts = group.Definition.Magnitude == MagnitudePolicy.Additive || stack == strongest;
                bool applied = stack.StatHandles != null && stack.StatHandles.Count > 0;
                if (counts && !applied)
                {
                    GiveStatModifiers(group, stack);
                }
                else if (!counts && applied)
                {
                    WithdrawStatModifiers(group, stack);
                }
            }
        }

        private void GiveStatModifiers(Group group, Stack stack)
        {
            if (!attributes.TryGetSet(SourceHandle.FromEntity(group.Target), out AttributeSet set))
            {
                return;
            }

            stack.StatHandles = new List<ModifierHandle>();
            foreach (StatusStatModifier spec in group.Definition.StatModifiers)
            {
                stack.StatHandles.Add(set.AddModifier(new AttributeModifier(new AttributeId(spec.attributeKey), spec.layer, spec.value, stack.Source)));
            }
        }

        private void WithdrawStatModifiers(Group group, Stack stack)
        {
            if (stack.StatHandles == null)
            {
                return;
            }

            if (attributes != null && attributes.TryGetSet(SourceHandle.FromEntity(group.Target), out AttributeSet set))
            {
                foreach (ModifierHandle handle in stack.StatHandles) set.RemoveModifier(handle);
            }

            stack.StatHandles = null;
        }

        private void WithdrawAllStatModifiers(Group group)
        {
            foreach (Stack stack in group.Stacks) WithdrawStatModifiers(group, stack);
        }

        // ----- Removing -----

        /// <summary>
        /// Ends every group of a status on an entity.
        /// </summary>
        /// <returns>How many groups ended.</returns>
        public int Dispel(EntityId target, StatusDefinition status)
        {
            if (!byTarget.TryGetValue(target, out List<Group> groups))
            {
                return 0;
            }

            List<Group> matching = groups.Where(g => g.Definition == status).ToList();
            foreach (Group group in matching) RemoveGroup(group, RemovalReason.Dispelled);
            Drain();
            return matching.Count;
        }

        /// <summary>
        /// Ends every status on an entity.
        /// </summary>
        public int RemoveAll(EntityId target, RemovalReason reason)
        {
            if (!byTarget.TryGetValue(target, out List<Group> groups))
            {
                return 0;
            }

            List<Group> all = groups.ToList();
            foreach (Group group in all) RemoveGroup(group, reason);
            Drain();
            return all.Count;
        }

        /// <summary>
        /// Removes what one source applied, wherever it is. Stacks from other sources stay, even in the same group.
        /// </summary>
        /// <returns>How many stacks were removed.</returns>
        public int RemoveFromSource(SourceHandle source)
        {
            int removed = 0;
            foreach (EntityId target in byTarget.Keys.OrderBy(k => k.Value).ToList())
            {
                foreach (Group group in byTarget[target].ToList())
                {
                    foreach (Stack stack in group.Stacks.Where(s => s.Source == source).ToList())
                    {
                        RemoveStack(group, stack, RemovalReason.SourceRemoved);
                        removed++;
                    }
                }
            }

            Drain();
            return removed;
        }

        private void RemoveStack(Group group, Stack stack, RemovalReason reason)
        {
            group.Stacks.Remove(stack);
            WithdrawStatModifiers(group, stack);
            ReconcileStatModifiers(group);
            Raise(new StatusStackExpiredEvent(group.Target, group.DefinitionId, group.Id, stack.Id, reason, group.Stacks.Count));

            if (group.Stacks.Count == 0)
            {
                RemoveGroup(group, reason);
            }
        }

        private void RemoveGroup(Group group, RemovalReason reason)
        {
            if (group.Removed)
            {
                return;
            }

            group.Removed = true;
            WithdrawAllStatModifiers(group);
            if (byTarget.TryGetValue(group.Target, out List<Group> groups))
            {
                groups.Remove(group);
                if (groups.Count == 0) byTarget.Remove(group.Target);
            }

            Raise(new StatusRemovedEvent(group.Target, group.DefinitionId, group.Id, reason));
        }

        /// <summary>
        /// An entity left the game: its statuses are dropped without ceremony.
        /// </summary>
        private void Forget(EntityId entity)
        {
            if (byTarget.TryGetValue(entity, out List<Group> groups))
            {
                foreach (Group group in groups)
                {
                    group.Removed = true;
                    WithdrawAllStatModifiers(group);
                }

                byTarget.Remove(entity);
            }
        }

        // ----- Time -----

        /// <summary>
        /// Brings every status up to the clock's current tick: runs the ticks that are due and ends the stacks that have run out, in time order.
        /// Calling it again for the same tick does nothing.
        /// </summary>
        public void Process()
        {
            long now = clock.Tick;
            if (now == lastProcessed)
            {
                return;
            }

            lastProcessed = now;

            // A fixed order, so two runs of the same game process the same things in the same sequence.
            foreach (EntityId target in byTarget.Keys.OrderBy(k => k.Value).ToList())
            {
                if (!byTarget.TryGetValue(target, out List<Group> groups))
                {
                    continue;
                }

                foreach (Group group in groups.ToList())
                {
                    ProcessGroup(group, now);
                }
            }
        }

        private void ProcessGroup(Group group, long now)
        {
            while (!group.Removed)
            {
                long tickTime = NextTickTime(group);
                long expiryTime = NextExpiry(group);
                long next = Math.Min(tickTime, expiryTime);
                if (next > now)
                {
                    break;
                }

                // A tick due on the very tick a stack expires still happens: the stack lasts through its last tick.
                if (tickTime <= expiryTime)
                {
                    RunTick(group, tickTime);
                }
                else
                {
                    ExpireAt(group, expiryTime);
                }
            }

            Drain();
        }

        private long NextTickTime(Group group)
        {
            if (IntervalTicks(group.Definition) == 0 || group.Stacks.Count == 0)
            {
                return long.MaxValue;
            }

            return group.Definition.Periodic == PeriodicMode.PerStack ? group.Stacks.Min(s => s.NextTick) : group.NextTick;
        }

        private long NextExpiry(Group group)
        {
            if (group.Stacks.Count == 0)
            {
                return long.MaxValue;
            }

            return group.Definition.Duration == DurationPolicy.SharedDuration ? group.SharedExpiry : group.Stacks.Min(s => s.ExpiryTick);
        }

        private void ExpireAt(Group group, long time)
        {
            if (group.Definition.Duration == DurationPolicy.SharedDuration)
            {
                RemoveGroup(group, RemovalReason.Expired);
                return;
            }

            foreach (Stack stack in group.Stacks.Where(s => s.ExpiryTick <= time).OrderBy(s => s.Id.Value).ToList())
            {
                if (group.Removed) return;
                RemoveStack(group, stack, RemovalReason.Expired);
            }
        }

        private void RunTick(Group group, long time)
        {
            long interval = IntervalTicks(group.Definition);

            if (group.Definition.Periodic == PeriodicMode.PerStack)
            {
                foreach (Stack stack in group.Stacks.Where(s => s.NextTick == time).OrderBy(s => s.Id.Value).ToList())
                {
                    // The schedule moves on first, so a tick that changes the status cannot make it run twice.
                    stack.NextTick += interval;
                    if (group.Removed || !group.Stacks.Contains(stack)) continue;
                    ExecuteTick(group, stack.Potency, 1, stack.Instigator);
                }

                return;
            }

            group.NextTick += interval;
            List<Stack> alive = group.Stacks.ToList();
            if (alive.Count == 0)
            {
                return;
            }

            ExecuteTick(group, Combine(alive, group.Definition.Magnitude), alive.Count, Credited(alive).Instigator);
        }

        /// <summary>
        /// The strength of a combined tick. Additive and independent contributions both add the stacks up; the strongest-only policy takes the largest.
        /// </summary>
        private static double Combine(IReadOnlyList<Stack> stacks, MagnitudePolicy policy)
        {
            return policy == MagnitudePolicy.StrongestOnly ? stacks.Max(s => s.Potency) : stacks.Sum(s => s.Potency);
        }

        /// <summary>
        /// A combined tick is credited to the stack that contributes most, and to the most recent one among equals.
        /// </summary>
        private static Stack Credited(IReadOnlyList<Stack> stacks) =>
            stacks.OrderByDescending(s => s.Potency).ThenByDescending(s => s.AppliedTick).ThenByDescending(s => s.Id.Value).First();

        private void ExecuteTick(Group group, double potency, int stackCount, EntityId instigator)
        {
            AbilityDefinition ability = group.Definition.TickAbility;
            if (ability != null && Executor != null)
            {
                CompileResult compiled = ability.Compile();
                if (compiled.Success)
                {
                    Position3 position = combatants.TryGet(group.Target, out ICombatant holder) ? holder.Position : default;
                    ExecutionRequest request = new ExecutionRequest(
                        SourceHandle.FromStatus(group.Id),
                        instigator,
                        position,
                        default,
                        new StatusInvocation(group.Target, potency, stackCount));
                    Executor.Execute(compiled.Ability, request);
                }
                else
                {
                    Diagnostic?.Invoke(group.Definition.name + "'s tick ability " + ability.name + " has errors and cannot run:\n" + compiled.Report);
                }
            }

            Raise(new StatusTickedEvent(group.Target, group.DefinitionId, group.Id, instigator, potency, stackCount));
        }

        // ----- Helpers -----

        private StatusSnapshot Snapshot(Group group)
        {
            List<StackSnapshot> stacks = group.Stacks
                .OrderBy(s => s.AppliedTick).ThenBy(s => s.Id.Value)
                .Select(s => new StackSnapshot(s.Id, s.Potency, s.AppliedTick, s.ExpiryTick, s.Source, s.Instigator))
                .ToList();

            double effective = group.Stacks.Count == 0 ? 0d : Combine(group.Stacks, group.Definition.Magnitude);
            long shared = group.Definition.Duration == DurationPolicy.SharedDuration ? group.SharedExpiry : -1;
            return new StatusSnapshot(group.Id, group.DefinitionId, stacks, shared, effective);
        }

        private long DurationTicks(StatusDefinition definition) => GameplayTime.SecondsToTicks(definition.DurationSeconds, clock.TickRate);

        private long IntervalTicks(StatusDefinition definition) =>
            definition.TickIntervalSeconds > 0f ? GameplayTime.SecondsToTicks(definition.TickIntervalSeconds, clock.TickRate) : 0;

        private StatusInstanceId NewId() => new StatusInstanceId((uint)ids.Next());

        private void RaiseApplied(Group group, in StatusApplication application) =>
            Raise(new StatusAppliedEvent(group.Target, group.DefinitionId, group.Id, application.Source, application.Instigator));

        private void RaiseStackAdded(Group group, Stack stack) =>
            Raise(new StatusStackAddedEvent(group.Target, group.DefinitionId, group.Id, stack.Id, stack.Potency, group.Stacks.Count));

        private void Raise(GameplayEvent gameplayEvent) => events.Enqueue(gameplayEvent);

        private void Drain()
        {
            if (!events.IsDraining)
            {
                events.Drain();
            }
        }
    }
}
