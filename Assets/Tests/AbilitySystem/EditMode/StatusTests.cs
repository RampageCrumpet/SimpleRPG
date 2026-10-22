using NUnit.Framework;
using SimpleRPG.Gameplay;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using EntityId = SimpleRPG.Gameplay.EntityId;

namespace SimpleRPG.Abilities.Tests.EditMode
{
    /// <summary>
    /// Runs statuses with the real status manager, executor, combat pipeline and event dispatcher on a manual clock (30 ticks a second).
    /// Burning is the shared-duration example (stacks add up and refresh together); Poison is the independent one (every stack runs its own timer).
    /// </summary>
    public class StatusTests
    {
        private const int Rate = 30;

        private readonly List<Object> created = new List<Object>();

        private ManualGameplayClock clock;
        private GameplayEventDispatcher events;
        private CombatantRegistry combatants;
        private CombatResolver combat;
        private ExecutionServices services;
        private AbilityExecutor executor;
        private StatusManager statuses;
        private List<string> diagnostics;
        private List<string> log;

        private TestCombatant target;
        private TestCombatant mage;
        private TestCombatant spider;

        private StatusDefinition burning;
        private StatusDefinition poison;

        private static readonly SourceHandle Staff = SourceHandle.FromItem(new ItemInstanceId(50));
        private static readonly SourceHandle Fang = SourceHandle.FromItem(new ItemInstanceId(51));

        [SetUp]
        public void SetUp()
        {
            clock = new ManualGameplayClock(Rate);
            events = new GameplayEventDispatcher();
            combatants = new CombatantRegistry();
            combat = new CombatResolver(events, combatants);
            services = new ExecutionServices(new AttributeSetRegistry(), combat, new ScriptedProjectiles());
            statuses = new StatusManager(clock, events, combatants);
            services.Statuses = statuses;
            executor = new AbilityExecutor(services);
            statuses.Executor = executor;
            diagnostics = new List<string>();
            log = new List<string>();
            services.Diagnostic += diagnostics.Add;
            statuses.Diagnostic += diagnostics.Add;
            events.Diagnostic += diagnostics.Add;

            target = Add(1, 1000);
            mage = Add(2, 100);
            spider = Add(3, 100);

            AbilityDefinition tick = AbilityBuilder.StatusTick();
            created.Add(tick);

            burning = Status("Burning", durationSeconds: 5f, tickSeconds: 1f, tick, StatusGrouping.PerStatus, ReapplicationPolicy.AddStack, 5, OverflowPolicy.Refresh, DurationPolicy.SharedDuration, RefreshPolicy.All, MagnitudePolicy.Additive, PeriodicMode.Combined);
            poison = Status("Poison", durationSeconds: 6f, tickSeconds: 1f, tick, StatusGrouping.PerStatus, ReapplicationPolicy.AddStack, 5, OverflowPolicy.Refresh, DurationPolicy.IndependentPerStack, RefreshPolicy.None, MagnitudePolicy.Additive, PeriodicMode.PerStack);
        }

        [TearDown]
        public void TearDown()
        {
            statuses.Dispose();
            foreach (Object o in created)
            {
                if (o != null) Object.DestroyImmediate(o);
            }

            created.Clear();
        }

        private StatusDefinition Status(string name, float durationSeconds, float tickSeconds, AbilityDefinition tick, StatusGrouping grouping, ReapplicationPolicy reapplication, int max, OverflowPolicy overflow, DurationPolicy duration, RefreshPolicy refresh, MagnitudePolicy magnitude, PeriodicMode periodic)
        {
            StatusDefinition status = ScriptableObject.CreateInstance<StatusDefinition>();
            status.name = name;
            status.AssignNewId();
            status.Configure(name, durationSeconds, tickSeconds, tick, grouping, reapplication, max, overflow, duration, refresh, magnitude, periodic);
            created.Add(status);
            return status;
        }

        private TestCombatant Add(ulong id, int health)
        {
            TestCombatant combatant = new TestCombatant(id, health);
            combatants.Register(combatant);
            return combatant;
        }

        private StatusApplyResult Apply(StatusDefinition status, SourceHandle source, TestCombatant instigator, double potency, TestCombatant on = null)
        {
            on ??= target;
            return statuses.Apply(new StatusApplication(on.Id, status, source, instigator != null ? instigator.Id : default, potency));
        }

        /// <summary>
        /// Moves the clock forward one second at a time, processing statuses after each tick of the clock.
        /// </summary>
        private void Pass(double seconds)
        {
            long ticks = GameplayTime.SecondsToTicks(seconds, Rate);
            for (long i = 0; i < ticks; i++)
            {
                clock.Advance(1);
                statuses.Process();
            }
        }

        private StatusSnapshot Snapshot(StatusDefinition status, TestCombatant on = null)
        {
            Assert.IsTrue(statuses.TryGetStatus((on ?? target).Id, status, out StatusSnapshot snapshot), "The status should be on the target.");
            return snapshot;
        }

        private List<int> DamageTaken(TestCombatant on = null)
        {
            List<int> taken = new List<int>();
            EntityId id = (on ?? target).Id;
            events.Subscribe<DamageResolvedEvent>(e =>
            {
                if (e.Request.Target == id) taken.Add(e.HealthBefore - e.HealthAfter);
            });
            return taken;
        }

        // ----- Applying and the lifecycle events -----

        [Test]
        public void ApplyingAStatus_Starts_AndRaisesAppliedThenStackAdded()
        {
            events.Subscribe<StatusAppliedEvent>(e => log.Add("applied"));
            events.Subscribe<StatusStackAddedEvent>(e => log.Add("stack " + e.StackCount));

            StatusApplyResult result = Apply(burning, Staff, mage, 2);

            Assert.AreEqual(StatusApplyResult.Started, result);
            CollectionAssert.AreEqual(new[] { "applied", "stack 1" }, log);
            Assert.AreEqual(1, Snapshot(burning).StackCount);
        }

        [Test]
        public void TheStatusKnowsWhoAppliedIt_AndWhatFrom()
        {
            events.Subscribe<StatusAppliedEvent>(e => log.Add(e.Source + "/" + e.Instigator));

            Apply(burning, Staff, mage, 2);

            CollectionAssert.AreEqual(new[] { Staff + "/" + mage.Id }, log);
            StackSnapshot stack = Snapshot(burning).Stacks[0];
            Assert.AreEqual(Staff, stack.Source);
            Assert.AreEqual(mage.Id, stack.Instigator);
            Assert.AreEqual(2d, stack.Potency);
        }

        [Test]
        public void ADeadOrUnknownTarget_CannotBeAfflicted()
        {
            combat.Apply(new DamageRequest(spider.Id, 1000));

            Assert.AreEqual(StatusApplyResult.RejectedTarget, Apply(burning, Staff, mage, 2, on: spider));
            Assert.AreEqual(StatusApplyResult.RejectedTarget, statuses.Apply(new StatusApplication(new EntityId(404), burning, Staff, mage.Id, 2)));
            Assert.AreEqual(StatusApplyResult.RejectedTarget, statuses.Apply(new StatusApplication(target.Id, null, Staff, mage.Id, 2)));
        }

        // ----- Burning: shared duration, stacks add up -----

        [Test]
        public void Burning_ReappliedAtDifferentTimes_RefreshesEveryStackToTheSameExpiry()
        {
            Apply(burning, Staff, mage, 2);
            Pass(2);
            Apply(burning, Staff, mage, 2);
            Pass(1);
            Apply(burning, Staff, mage, 2);

            StatusSnapshot snapshot = Snapshot(burning);

            Assert.AreEqual(3, snapshot.StackCount);
            long expected = clock.Tick + GameplayTime.SecondsToTicks(5, Rate);
            Assert.AreEqual(expected, snapshot.SharedExpiryTick, "Every application refreshes the whole group: 3 s in, plus 5 s.");
            Assert.IsTrue(snapshot.Stacks.All(s => s.ExpiryTick == expected), "Every stack shares the group's expiry.");
        }

        [Test]
        public void Burning_StillBurnsJustBeforeItsRefreshedExpiry_AndEndsAsOneGroup()
        {
            Apply(burning, Staff, mage, 2);
            Pass(2);
            Apply(burning, Staff, mage, 2);
            Pass(3);
            Apply(burning, Staff, mage, 2);
            int removed = 0;
            StatusRemovedEvent removal = null;
            int stackExpiries = 0;
            events.Subscribe<StatusRemovedEvent>(e => { removed++; removal = e; });
            events.Subscribe<StatusStackExpiredEvent>(e => stackExpiries++);

            Pass(4.9);
            Assert.IsTrue(statuses.HasStatus(target.Id, burning), "Still burning 4.9 s after the last application.");

            Pass(0.2);

            Assert.IsFalse(statuses.HasStatus(target.Id, burning));
            Assert.AreEqual(1, removed, "One group, one expiration.");
            Assert.AreEqual(RemovalReason.Expired, removal.Reason);
            Assert.AreEqual(0, stackExpiries, "A shared group ends together, not stack by stack.");
        }

        [Test]
        public void Burning_TicksWithTheStacksAddedUp()
        {
            List<int> taken = DamageTaken();
            Apply(burning, Staff, mage, 2);
            Apply(burning, Staff, mage, 2);
            Apply(burning, Staff, mage, 3);

            Pass(1);

            CollectionAssert.AreEqual(new[] { 7 }, taken, "2 + 2 + 3 in one combined tick");
        }

        [Test]
        public void Burning_TicksOncePerSecond_ForItsWholeDuration_IncludingTheLastSecond()
        {
            List<int> taken = DamageTaken();
            Apply(burning, Staff, mage, 4);

            Pass(10);

            Assert.AreEqual(5, taken.Count, "Five seconds of burning is five ticks.");
            Assert.IsTrue(taken.All(d => d == 4));
        }

        [Test]
        public void ReapplyingBurning_DoesNotRestartTheTickSchedule()
        {
            List<int> ticksAt = new List<int>();
            events.Subscribe<StatusTickedEvent>(e => ticksAt.Add((int)clock.Tick));
            Apply(burning, Staff, mage, 2);
            Pass(0.5);

            Apply(burning, Staff, mage, 2);
            Pass(2);

            Assert.AreEqual(new[] { 30, 60 }, ticksAt.ToArray(), "Ticks stay on the second marks however often it is reapplied.");
        }

        [Test]
        public void StrongestOnly_TicksWithTheLargestStack()
        {
            StatusDefinition strongest = Status("Scald", 5, 1, AbilityBuilder.StatusTick("T2"), StatusGrouping.PerStatus, ReapplicationPolicy.AddStack, 5, OverflowPolicy.Refresh, DurationPolicy.SharedDuration, RefreshPolicy.All, MagnitudePolicy.StrongestOnly, PeriodicMode.Combined);
            List<int> taken = DamageTaken();
            Apply(strongest, Staff, mage, 2);
            Apply(strongest, Staff, mage, 9);
            Apply(strongest, Staff, mage, 4);

            Pass(1);

            CollectionAssert.AreEqual(new[] { 9 }, taken);
        }

        // ----- Poison: independent stacks, each with its own timer -----

        [Test]
        public void Poison_StacksAppliedAtDifferentTimes_ExpireOnTheirOwn()
        {
            List<string> expiries = new List<string>();
            events.Subscribe<StatusStackExpiredEvent>(e => expiries.Add(clock.Tick / Rate + ":" + e.StacksLeft));
            int removedAt = -1;
            events.Subscribe<StatusRemovedEvent>(e => removedAt = (int)(clock.Tick / Rate));
            Apply(poison, Staff, mage, 2);
            Pass(2);
            Apply(poison, Staff, mage, 5);
            Pass(2);
            Apply(poison, Staff, mage, 3);

            Pass(10);

            CollectionAssert.AreEqual(new[] { "6:2", "8:1", "10:0" }, expiries, "Applied at 0, 2 and 4 s with 6 s each: they end at 6, 8 and 10 s.");
            Assert.AreEqual(10, removedAt, "The status ends when its last stack does.");
        }

        [Test]
        public void Poison_ReappliedStacks_DoNotRefreshTheOlderOnes()
        {
            Apply(poison, Staff, mage, 2);
            Pass(3);
            Apply(poison, Staff, mage, 2);

            StatusSnapshot snapshot = Snapshot(poison);

            Assert.AreEqual(180, snapshot.Stacks[0].ExpiryTick, "The first stack still ends at 6 s.");
            Assert.AreEqual(270, snapshot.Stacks[1].ExpiryTick, "The second at 3 s plus 6 s.");
            Assert.AreEqual(-1, snapshot.SharedExpiryTick, "There is no shared timer.");
        }

        [Test]
        public void Poison_EveryStackTicksWithItsOwnStrength()
        {
            List<int> taken = DamageTaken();
            Apply(poison, Staff, mage, 2);
            Pass(2);
            Apply(poison, Fang, spider, 5);
            Pass(2);
            Apply(poison, Staff, mage, 3);

            Pass(10);

            // Stack 1 (2 per second) ticks at 1..6 s, stack 2 (5) at 3..8 s, stack 3 (3) at 5..10 s: six ticks each.
            Assert.AreEqual(6 * 2 + 6 * 5 + 6 * 3, taken.Sum());
            Assert.AreEqual(18, taken.Count);
        }

        [Test]
        public void Poison_StacksKeepTheStrengthTheyWereAppliedWith_AndTheirOwnSources()
        {
            Apply(poison, Staff, mage, 2);
            Apply(poison, Fang, spider, 7);

            StatusSnapshot snapshot = Snapshot(poison);

            Assert.AreEqual(2d, snapshot.Stacks[0].Potency);
            Assert.AreEqual(Staff, snapshot.Stacks[0].Source);
            Assert.AreEqual(7d, snapshot.Stacks[1].Potency);
            Assert.AreEqual(Fang, snapshot.Stacks[1].Source);
            Assert.AreEqual(spider.Id, snapshot.Stacks[1].Instigator);
        }

        // ----- Strength is fixed when applied -----

        [Test]
        public void AStacksStrength_IsFixedAtApplication_EvenIfTheCastersStatChangesAfterwards()
        {
            AttributeSetRegistry attributes = new AttributeSetRegistry();
            AttributeSet mageStats = new AttributeSet();
            mageStats.SetBase(new AttributeId(AbilityBuilder.MagicPower), 6);
            attributes.Register(SourceHandle.FromEntity(mage.Id), mageStats);
            ScriptedProjectiles projectiles = new ScriptedProjectiles();
            services = new ExecutionServices(attributes, combat, projectiles) { Statuses = statuses };
            executor = new AbilityExecutor(services);
            statuses.Executor = executor;
            GameObject prefab = new GameObject("Projectile");
            created.Add(prefab);
            AbilityDefinition ignite = AbilityBuilder.Ignite(prefab, poison, 0, fromAttribute: true);
            created.Add(ignite);
            executor.Execute(ignite.Compile().Ability, new ExecutionRequest(SourceHandle.FromEntity(mage.Id), mage.Id, default, new Position3(0, 0, 1)));
            projectiles.Hit(0, target.Id);

            mageStats.SetBase(new AttributeId(AbilityBuilder.MagicPower), 99);
            List<int> taken = DamageTaken();
            Pass(2);

            Assert.AreEqual(6d, Snapshot(poison).Stacks[0].Potency);
            CollectionAssert.AreEqual(new[] { 6, 6 }, taken);
        }

        [Test]
        public void TheIgniteAbility_PutsTheStatusOnTheVictim_NotTheCaster()
        {
            ScriptedProjectiles projectiles = new ScriptedProjectiles();
            services = new ExecutionServices(new AttributeSetRegistry(), combat, projectiles) { Statuses = statuses };
            executor = new AbilityExecutor(services);
            GameObject prefab = new GameObject("Projectile");
            created.Add(prefab);
            AbilityDefinition ignite = AbilityBuilder.Ignite(prefab, burning, 3);
            created.Add(ignite);
            Assert.IsTrue(ignite.Compile().Success, ignite.Compile().Report.ToString());

            executor.Execute(ignite.Compile().Ability, new ExecutionRequest(Staff, mage.Id, default, new Position3(0, 0, 1)));
            projectiles.Hit(0, target.Id);

            Assert.IsTrue(statuses.HasStatus(target.Id, burning));
            Assert.IsFalse(statuses.HasStatus(mage.Id, burning));
            StackSnapshot stack = Snapshot(burning).Stacks[0];
            Assert.AreEqual(Staff, stack.Source);
            Assert.AreEqual(mage.Id, stack.Instigator);
            Assert.AreEqual(3d, stack.Potency);
        }

        // ----- Attribution -----

        [Test]
        public void TickDamage_IsCreditedToTheApplier_AndStaysCreditedAfterTheApplierDies()
        {
            List<DamageRequest> requests = new List<DamageRequest>();
            events.Subscribe<DamageResolvedEvent>(e => { if (e.Request.Target == target.Id) requests.Add(e.Request); });
            Apply(poison, Staff, mage, 4);
            Pass(1);

            combat.Apply(new DamageRequest(mage.Id, 1000));
            Assert.IsFalse(mage.IsAlive);
            Pass(2);

            Assert.AreEqual(3, requests.Count);
            Assert.IsTrue(requests.All(r => r.Instigator == mage.Id), "The applier's death does not orphan the poison's damage.");
            Assert.IsTrue(requests.All(r => r.Provenance == DamageProvenance.Periodic));
            Assert.IsTrue(requests.All(r => r.Source.Kind == SourceKind.Status));
        }

        [Test]
        public void ThePoisonThatKills_CreditsItsApplier()
        {
            TestCombatant weak = Add(10, 7);
            DeathContext death = null;
            events.Subscribe<EntityDiedEvent>(e => death = e.Context);
            Apply(poison, Staff, mage, 4, on: weak);

            Pass(2);

            Assert.IsNotNull(death);
            Assert.AreEqual(mage.Id, death.Killer);
            Assert.AreEqual(DamageProvenance.Periodic, death.KillingProvenance);
        }

        // ----- Death and removal -----

        [Test]
        public void WhenTheHolderDies_ItsStatusesEnd_AfterTheDeathsReactions()
        {
            List<string> order = new List<string>();
            events.Subscribe<EntityDiedEvent>(e => order.Add("died, still burning=" + statuses.HasStatus(target.Id, burning)));
            events.Subscribe<StatusRemovedEvent>(e => order.Add("removed " + e.Reason));
            Apply(burning, Staff, mage, 2);

            combat.Apply(new DamageRequest(target.Id, 5000));

            CollectionAssert.AreEqual(new[] { "died, still burning=True", "removed TargetDied" }, order);
            Assert.IsFalse(statuses.HasStatus(target.Id, burning));
        }

        [Test]
        public void ADeadHolder_TakesNoMoreTicks()
        {
            Apply(burning, Staff, mage, 2);
            combat.Apply(new DamageRequest(target.Id, 5000));
            int ticks = 0;
            events.Subscribe<StatusTickedEvent>(e => ticks++);

            Pass(10);

            Assert.AreEqual(0, ticks);
        }

        [Test]
        public void Dispel_EndsTheStatus_WithTheDispelledReason()
        {
            RemovalReason reason = RemovalReason.Expired;
            events.Subscribe<StatusRemovedEvent>(e => reason = e.Reason);
            Apply(burning, Staff, mage, 2);

            int groups = statuses.Dispel(target.Id, burning);

            Assert.AreEqual(1, groups);
            Assert.AreEqual(RemovalReason.Dispelled, reason);
            Assert.IsFalse(statuses.HasStatus(target.Id, burning));
            Assert.AreEqual(0, statuses.Dispel(target.Id, burning));
        }

        [Test]
        public void RemovingOneSource_LeavesAnotherSourcesStacksInTheSameGroup()
        {
            Apply(poison, Staff, mage, 2);
            Apply(poison, Fang, spider, 5);
            List<RemovalReason> reasons = new List<RemovalReason>();
            events.Subscribe<StatusStackExpiredEvent>(e => reasons.Add(e.Reason));

            int removed = statuses.RemoveFromSource(Staff);

            Assert.AreEqual(1, removed);
            StatusSnapshot snapshot = Snapshot(poison);
            Assert.AreEqual(1, snapshot.StackCount);
            Assert.AreEqual(Fang, snapshot.Stacks[0].Source);
            CollectionAssert.AreEqual(new[] { RemovalReason.SourceRemoved }, reasons);
        }

        [Test]
        public void RemovingTheLastStacksSource_EndsTheStatus()
        {
            Apply(poison, Staff, mage, 2);
            RemovalReason reason = RemovalReason.Expired;
            events.Subscribe<StatusRemovedEvent>(e => reason = e.Reason);

            statuses.RemoveFromSource(Staff);

            Assert.IsFalse(statuses.HasStatus(target.Id, poison));
            Assert.AreEqual(RemovalReason.SourceRemoved, reason);
        }

        [Test]
        public void AnEntityThatLeavesTheGame_TakesItsStatusesWithoutCeremony()
        {
            Apply(burning, Staff, mage, 2);
            int removedEvents = 0;
            events.Subscribe<StatusRemovedEvent>(e => removedEvents++);

            events.Enqueue(new EntityRemovedEvent(target.Id));
            events.Drain();
            Pass(10);

            Assert.AreEqual(0, statuses.GroupCount(target.Id));
            Assert.AreEqual(0, removedEvents);
        }

        // ----- Grouping -----

        [Test]
        public void PerSourceGrouping_KeepsEachSourcesStacksApart()
        {
            StatusDefinition bleed = Status("Bleed", 5, 0, null, StatusGrouping.PerSource, ReapplicationPolicy.AddStack, 5, OverflowPolicy.Refresh, DurationPolicy.SharedDuration, RefreshPolicy.All, MagnitudePolicy.Additive, PeriodicMode.Combined);

            Apply(bleed, Staff, mage, 1);
            Apply(bleed, Staff, mage, 1);
            Apply(bleed, Fang, spider, 1);

            IReadOnlyList<StatusSnapshot> groups = statuses.GetStatuses(target.Id, bleed);
            Assert.AreEqual(2, groups.Count);
            CollectionAssert.AreEquivalent(new[] { 2, 1 }, groups.Select(g => g.StackCount).ToArray());
        }

        [Test]
        public void PerInstigatorGrouping_KeepsEachAppliersStacksApart()
        {
            StatusDefinition curse = Status("Curse", 5, 0, null, StatusGrouping.PerInstigator, ReapplicationPolicy.AddStack, 5, OverflowPolicy.Refresh, DurationPolicy.SharedDuration, RefreshPolicy.All, MagnitudePolicy.Additive, PeriodicMode.Combined);

            Apply(curse, Staff, mage, 1);
            Apply(curse, Fang, mage, 1);
            Apply(curse, Fang, spider, 1);

            Assert.AreEqual(2, statuses.GetStatuses(target.Id, curse).Count);
        }

        [Test]
        public void PerStatusGrouping_MergesEveryApplier()
        {
            Apply(burning, Staff, mage, 1);
            Apply(burning, Fang, spider, 1);

            Assert.AreEqual(1, statuses.GetStatuses(target.Id, burning).Count);
            Assert.AreEqual(2, Snapshot(burning).StackCount);
        }

        // ----- Reapplication and overflow -----

        [Test]
        public void IgnoreReapplication_ChangesNothing()
        {
            StatusDefinition once = Status("Once", 5, 0, null, StatusGrouping.PerStatus, ReapplicationPolicy.Ignore, 5, OverflowPolicy.Refresh, DurationPolicy.SharedDuration, RefreshPolicy.All, MagnitudePolicy.Additive, PeriodicMode.Combined);
            Apply(once, Staff, mage, 1);
            Pass(2);

            StatusApplyResult result = Apply(once, Staff, mage, 1);

            Assert.AreEqual(StatusApplyResult.Ignored, result);
            Assert.AreEqual(1, Snapshot(once).StackCount);
            Assert.AreEqual(150, Snapshot(once).SharedExpiryTick, "Not refreshed.");
        }

        [Test]
        public void ReplaceReapplication_EndsTheOldAndStartsTheNew()
        {
            StatusDefinition swap = Status("Swap", 5, 0, null, StatusGrouping.PerStatus, ReapplicationPolicy.Replace, 5, OverflowPolicy.Refresh, DurationPolicy.SharedDuration, RefreshPolicy.All, MagnitudePolicy.Additive, PeriodicMode.Combined);
            Apply(swap, Staff, mage, 1);
            Pass(2);
            RemovalReason reason = RemovalReason.Expired;
            events.Subscribe<StatusRemovedEvent>(e => reason = e.Reason);

            StatusApplyResult result = Apply(swap, Fang, spider, 9);

            Assert.AreEqual(StatusApplyResult.Replaced, result);
            Assert.AreEqual(RemovalReason.Replaced, reason);
            StatusSnapshot snapshot = Snapshot(swap);
            Assert.AreEqual(1, snapshot.StackCount);
            Assert.AreEqual(9d, snapshot.Stacks[0].Potency);
            Assert.AreEqual(Fang, snapshot.Stacks[0].Source);
        }

        [Test]
        public void RefreshOnlyReapplication_AddsNoStack_ButRestartsTheTimer()
        {
            StatusDefinition keep = Status("Keep", 5, 0, null, StatusGrouping.PerStatus, ReapplicationPolicy.RefreshOnly, 5, OverflowPolicy.Refresh, DurationPolicy.SharedDuration, RefreshPolicy.All, MagnitudePolicy.Additive, PeriodicMode.Combined);
            Apply(keep, Staff, mage, 1);
            Pass(2);

            StatusApplyResult result = Apply(keep, Staff, mage, 1);

            Assert.AreEqual(StatusApplyResult.Refreshed, result);
            Assert.AreEqual(1, Snapshot(keep).StackCount);
            Assert.AreEqual(60 + 150, Snapshot(keep).SharedExpiryTick);
        }

        [Test]
        public void RefreshPolicyNone_LetsANewStackJoinTheRunningTimer()
        {
            StatusDefinition steady = Status("Steady", 5, 0, null, StatusGrouping.PerStatus, ReapplicationPolicy.AddStack, 5, OverflowPolicy.Refresh, DurationPolicy.SharedDuration, RefreshPolicy.None, MagnitudePolicy.Additive, PeriodicMode.Combined);
            Apply(steady, Staff, mage, 1);
            Pass(2);

            Apply(steady, Staff, mage, 1);

            Assert.AreEqual(150, Snapshot(steady).SharedExpiryTick, "The timer was not restarted.");
            Assert.AreEqual(2, Snapshot(steady).StackCount);
        }

        [Test]
        public void IndependentRefresh_All_Newest_Oldest_RestartTheRightStacks()
        {
            StatusDefinition MakeIndependent(string name, RefreshPolicy refresh) =>
                Status(name, 6, 0, null, StatusGrouping.PerStatus, ReapplicationPolicy.AddStack, 5, OverflowPolicy.Refresh, DurationPolicy.IndependentPerStack, refresh, MagnitudePolicy.Additive, PeriodicMode.Combined);

            StatusDefinition all = MakeIndependent("All", RefreshPolicy.All);
            StatusDefinition newest = MakeIndependent("Newest", RefreshPolicy.Newest);
            StatusDefinition oldest = MakeIndependent("Oldest", RefreshPolicy.Oldest);
            foreach (StatusDefinition status in new[] { all, newest, oldest })
            {
                Apply(status, Staff, mage, 1);
            }

            Pass(2);
            foreach (StatusDefinition status in new[] { all, newest, oldest })
            {
                Apply(status, Staff, mage, 1);
            }

            Pass(2);
            foreach (StatusDefinition status in new[] { all, newest, oldest })
            {
                Apply(status, Staff, mage, 1);
            }

            // Applied at 0, 2 and 4 s with 6 s (180 ticks) each. At 2 s the only existing stack is the one from 0 s; at 4 s there are two.
            CollectionAssert.AreEqual(new long[] { 300, 300, 300 }, Snapshot(all).Stacks.Select(s => s.ExpiryTick).ToArray(), "All: every existing stack restarts each time.");
            CollectionAssert.AreEqual(new long[] { 240, 300, 300 }, Snapshot(newest).Stacks.Select(s => s.ExpiryTick).ToArray(), "Newest: only the most recent existing stack restarts, so the first stack last restarted at 2 s.");
            CollectionAssert.AreEqual(new long[] { 300, 240, 300 }, Snapshot(oldest).Stacks.Select(s => s.ExpiryTick).ToArray(), "Oldest: only the oldest existing stack restarts, so the second stack keeps its own timer.");
        }

        [Test]
        public void AtTheStackLimit_OverflowRefresh_AddsNothingButRefreshes()
        {
            StatusDefinition capped = Status("Capped", 5, 0, null, StatusGrouping.PerStatus, ReapplicationPolicy.AddStack, 2, OverflowPolicy.Refresh, DurationPolicy.SharedDuration, RefreshPolicy.All, MagnitudePolicy.Additive, PeriodicMode.Combined);
            Apply(capped, Staff, mage, 1);
            Apply(capped, Staff, mage, 1);
            Pass(2);

            StatusApplyResult result = Apply(capped, Staff, mage, 1);

            Assert.AreEqual(StatusApplyResult.Refreshed, result);
            Assert.AreEqual(2, Snapshot(capped).StackCount);
            Assert.AreEqual(60 + 150, Snapshot(capped).SharedExpiryTick);
        }

        [Test]
        public void AtTheStackLimit_OverflowIgnore_ChangesNothing()
        {
            StatusDefinition capped = Status("Capped", 5, 0, null, StatusGrouping.PerStatus, ReapplicationPolicy.AddStack, 2, OverflowPolicy.Ignore, DurationPolicy.SharedDuration, RefreshPolicy.All, MagnitudePolicy.Additive, PeriodicMode.Combined);
            Apply(capped, Staff, mage, 1);
            Apply(capped, Staff, mage, 1);
            Pass(2);

            StatusApplyResult result = Apply(capped, Staff, mage, 1);

            Assert.AreEqual(StatusApplyResult.Ignored, result);
            Assert.AreEqual(150, Snapshot(capped).SharedExpiryTick, "An ignored application does not refresh either.");
        }

        [Test]
        public void AtTheStackLimit_ReplaceOldest_SwapsTheOldestStack()
        {
            StatusDefinition capped = Status("Capped", 6, 0, null, StatusGrouping.PerStatus, ReapplicationPolicy.AddStack, 2, OverflowPolicy.ReplaceOldest, DurationPolicy.IndependentPerStack, RefreshPolicy.None, MagnitudePolicy.Additive, PeriodicMode.Combined);
            Apply(capped, Staff, mage, 1);
            Pass(1);
            Apply(capped, Staff, mage, 2);
            Pass(1);

            StatusApplyResult result = Apply(capped, Staff, mage, 3);

            Assert.AreEqual(StatusApplyResult.StackAdded, result);
            CollectionAssert.AreEqual(new[] { 2d, 3d }, Snapshot(capped).Stacks.Select(s => s.Potency).ToArray(), "The first stack is gone.");
        }

        [Test]
        public void AtTheStackLimit_ReplaceWeakest_OnlyReplacesWithAnEqualOrStrongerStack()
        {
            StatusDefinition capped = Status("Capped", 6, 0, null, StatusGrouping.PerStatus, ReapplicationPolicy.AddStack, 2, OverflowPolicy.ReplaceWeakest, DurationPolicy.IndependentPerStack, RefreshPolicy.None, MagnitudePolicy.Additive, PeriodicMode.Combined);
            Apply(capped, Staff, mage, 5);
            Apply(capped, Staff, mage, 2);

            StatusApplyResult weaker = Apply(capped, Staff, mage, 1);
            StatusApplyResult stronger = Apply(capped, Staff, mage, 4);

            Assert.AreEqual(StatusApplyResult.Ignored, weaker, "A weaker stack does not displace a stronger one.");
            Assert.AreEqual(StatusApplyResult.StackAdded, stronger);
            CollectionAssert.AreEquivalent(new[] { 5d, 4d }, Snapshot(capped).Stacks.Select(s => s.Potency).ToArray());
        }

        [Test]
        public void ReplacingTheOnlyStackOfAOneStackStatus_StartsItAgain()
        {
            StatusDefinition single = Status("Single", 5, 0, null, StatusGrouping.PerStatus, ReapplicationPolicy.AddStack, 1, OverflowPolicy.ReplaceOldest, DurationPolicy.SharedDuration, RefreshPolicy.All, MagnitudePolicy.Additive, PeriodicMode.Combined);
            Apply(single, Staff, mage, 1);

            StatusApplyResult result = Apply(single, Staff, mage, 8);

            Assert.AreEqual(StatusApplyResult.StackAdded, result);
            Assert.AreEqual(1, Snapshot(single).StackCount);
            Assert.AreEqual(8d, Snapshot(single).Stacks[0].Potency);
        }

        // ----- Time -----

        [Test]
        public void ProcessingTheSameTickTwice_DoesNothingTheSecondTime()
        {
            List<int> taken = DamageTaken();
            Apply(burning, Staff, mage, 2);
            clock.Advance(30);

            statuses.Process();
            statuses.Process();

            Assert.AreEqual(1, taken.Count);
        }

        [Test]
        public void ALongGap_CatchesUpEveryTickInOrder_ThenEndsTheStatus()
        {
            List<int> taken = DamageTaken();
            Apply(burning, Staff, mage, 2);

            clock.Advance(30 * 20);
            statuses.Process();

            Assert.AreEqual(5, taken.Count, "Five ticks, not twenty: it ended after five seconds.");
            Assert.IsFalse(statuses.HasStatus(target.Id, burning));
        }

        [Test]
        public void AStatusWithNoTickInterval_NeverTicks_ButStillExpires()
        {
            StatusDefinition silent = Status("Silent", 2, 0, null, StatusGrouping.PerStatus, ReapplicationPolicy.AddStack, 5, OverflowPolicy.Refresh, DurationPolicy.SharedDuration, RefreshPolicy.All, MagnitudePolicy.Additive, PeriodicMode.Combined);
            int ticks = 0;
            events.Subscribe<StatusTickedEvent>(e => ticks++);
            Apply(silent, Staff, mage, 1);

            Pass(5);

            Assert.AreEqual(0, ticks);
            Assert.IsFalse(statuses.HasStatus(target.Id, silent));
        }

        [Test]
        public void AStatusWithNoTickAbility_StillRaisesTheTickEvent()
        {
            StatusDefinition marker = Status("Marker", 3, 1, null, StatusGrouping.PerStatus, ReapplicationPolicy.AddStack, 5, OverflowPolicy.Refresh, DurationPolicy.SharedDuration, RefreshPolicy.All, MagnitudePolicy.Additive, PeriodicMode.Combined);
            int ticks = 0;
            events.Subscribe<StatusTickedEvent>(e => ticks++);
            Apply(marker, Staff, mage, 1);

            Pass(3);

            Assert.AreEqual(3, ticks);
        }

        [Test]
        public void ATickAbilityWithErrors_IsReported_AndDoesNotStopTheStatus()
        {
            AbilityDefinition broken = AbilityBuilder.Empty("Broken");
            created.Add(broken);
            broken.AddNode("simplerpg.nonexistent.v1", null, "mystery");
            StatusDefinition faulty = Status("Faulty", 3, 1, broken, StatusGrouping.PerStatus, ReapplicationPolicy.AddStack, 5, OverflowPolicy.Refresh, DurationPolicy.SharedDuration, RefreshPolicy.All, MagnitudePolicy.Additive, PeriodicMode.Combined);
            Apply(faulty, Staff, mage, 1);

            Assert.DoesNotThrow(() => Pass(4));

            Assert.IsTrue(diagnostics.Any(d => d.Contains("has errors")));
            Assert.IsFalse(statuses.HasStatus(target.Id, faulty), "It still ran out of time.");
        }

        // ----- Compile-time rules for the status nodes -----

        [Test]
        public void StatusNodes_AreInThePalette()
        {
            Assert.IsTrue(AbilityNodeRegistry.Default.Search("status").Count() >= 5);
            Assert.IsTrue(AbilityNodeRegistry.Default.Search("tick").Any(d => d.TypeId == NodeTypes.StatusTick));
        }

        [Test]
        public void TheTickAbility_Compiles()
        {
            AbilityDefinition tick = AbilityBuilder.StatusTick();
            created.Add(tick);

            Assert.IsTrue(tick.Compile().Success, tick.Compile().Report.ToString());
        }

        [Test]
        public void StatusPotency_InAnOrdinaryAbility_IsRejected()
        {
            AbilityDefinition ability = AbilityBuilder.Empty("Misplaced");
            created.Add(ability);
            string damage = ability.AddNode(NodeTypes.Damage, null, "damage");
            string holder = ability.AddNode(NodeTypes.StatusHolder, null, "holder");
            string potency = ability.AddNode(NodeTypes.StatusPotency, null, "potency");
            ability.Connect("activate", BuiltInNodes.Next, damage, BuiltInNodes.In);
            ability.Connect(holder, BuiltInNodes.Value, damage, BuiltInNodes.Target);
            ability.Connect(potency, BuiltInNodes.Value, damage, BuiltInNodes.Amount);

            CompileResult result = ability.Compile();

            Assert.IsFalse(result.Success);
            Assert.IsTrue(result.Report.Errors.Any(e => e.NodeId == "potency" && e.Text.Contains("status tick ability")), result.Report.ToString());
        }

        [Test]
        public void AnAbilityWithTwoEntryNodes_IsRejected()
        {
            AbilityDefinition ability = AbilityBuilder.Empty("TwoEntries");
            created.Add(ability);
            ability.AddNode(NodeTypes.StatusTick, null, "tick");

            Assert.IsTrue(ability.Compile().Report.Errors.Any(e => e.Text.Contains("exactly one")));
        }

        [Test]
        public void ApplyStatusWithoutAStatus_IsRejected()
        {
            AbilityDefinition ability = AbilityBuilder.Empty("NoStatus");
            created.Add(ability);
            ability.AddNode(NodeTypes.ApplyStatus, new ApplyStatusConfig(), "apply");

            Assert.IsTrue(ability.Compile().Report.Errors.Any(e => e.NodeId == "apply" && e.Text.Contains("status is required")));
        }
    }
}
