using NUnit.Framework;
using SimpleRPG.Gameplay;
using System.Collections.Generic;
using UnityEngine;
using EntityId = SimpleRPG.Gameplay.EntityId;

namespace SimpleRPG.Abilities.Tests.EditMode
{
    /// <summary>
    /// A status that changes a stat while active, and withdraws exactly that contribution on every way it can end.
    /// </summary>
    public class StatusStatModifierTests
    {
        private const int Rate = 30;

        private static readonly SourceHandle Frost = SourceHandle.FromItem(new ItemInstanceId(70));
        private static readonly SourceHandle Ice = SourceHandle.FromItem(new ItemInstanceId(71));

        private readonly List<Object> created = new List<Object>();

        private ManualGameplayClock clock;
        private GameplayEventDispatcher events;
        private CombatantRegistry combatants;
        private AttributeSetRegistry attributes;
        private StatusManager statuses;
        private TestCombatant target;
        private AttributeSet set;

        [SetUp]
        public void SetUp()
        {
            clock = new ManualGameplayClock(Rate);
            events = new GameplayEventDispatcher();
            combatants = new CombatantRegistry();
            attributes = new AttributeSetRegistry();
            statuses = new StatusManager(clock, events, combatants, attributes);

            target = new TestCombatant(1, 100);
            combatants.Register(target);
            set = new StatSheet(StatOwnership.Direct, new[] { new StatRule(DerivedStats.MovementSpeed, 4) }).CreateSet();
            attributes.Register(SourceHandle.FromEntity(target.Id), set);
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

        private StatusDefinition Slow(
            ReapplicationPolicy reapplication = ReapplicationPolicy.AddStack,
            int maxStacks = 3,
            OverflowPolicy overflow = OverflowPolicy.Refresh,
            MagnitudePolicy magnitude = MagnitudePolicy.Additive,
            StatusGrouping grouping = StatusGrouping.PerStatus,
            double factor = 0.5)
        {
            StatusDefinition status = ScriptableObject.CreateInstance<StatusDefinition>();
            status.AssignNewId();
            status.Configure(
                "Slow", 5f, 0f, null, grouping, reapplication, maxStacks, overflow, DurationPolicy.SharedDuration, RefreshPolicy.All, magnitude, PeriodicMode.Combined,
                new[] { new StatusStatModifier { attributeKey = DerivedStats.MovementSpeed.Key, layer = ModifierLayer.Multiply, value = factor } });
            created.Add(status);
            return status;
        }

        private StatusApplyResult Apply(StatusDefinition status, SourceHandle source = default, double potency = 1d) =>
            statuses.Apply(new StatusApplication(target.Id, status, source, default, potency));

        private void Pass(double seconds)
        {
            long ticks = GameplayTime.SecondsToTicks(seconds, Rate);
            for (long i = 0; i < ticks; i++)
            {
                clock.Advance(1);
                statuses.Process();
            }
        }

        private double Speed => set.Get(DerivedStats.MovementSpeed);

        [Test]
        public void ActiveStatus_ChangesTheStat()
        {
            Apply(Slow());

            Assert.AreEqual(2d, Speed, 1e-9);
            Assert.AreEqual(1, set.ModifierCount);
        }

        [Test]
        public void Expiry_WithdrawsTheContribution()
        {
            Apply(Slow());

            Pass(6);

            Assert.AreEqual(4d, Speed, 1e-9);
            Assert.AreEqual(0, set.ModifierCount);
        }

        [Test]
        public void Dispel_WithdrawsTheContribution()
        {
            StatusDefinition slow = Slow();
            Apply(slow);

            statuses.Dispel(target.Id, slow);

            Assert.AreEqual(4d, Speed, 1e-9);
            Assert.AreEqual(0, set.ModifierCount);
        }

        [Test]
        public void RemoveAll_WithdrawsTheContribution()
        {
            Apply(Slow());

            statuses.RemoveAll(target.Id, RemovalReason.Dispelled);

            Assert.AreEqual(0, set.ModifierCount);
        }

        [Test]
        public void Death_WithdrawsTheContribution()
        {
            Apply(Slow());

            events.Enqueue(new DeathFinalizeEvent(new DeathContext(new DeathTransitionId(target.Id, 1), default, default, DamageProvenance.None, default, new List<SourceHandle>().AsReadOnly())));
            events.Drain();

            Assert.AreEqual(0, set.ModifierCount);
            Assert.AreEqual(4d, Speed, 1e-9);
        }

        [Test]
        public void RemovingTheSource_WithdrawsOnlyThatSourcesStack()
        {
            StatusDefinition slow = Slow(factor: 0.5);
            Apply(slow, Frost);
            Apply(slow, Ice);
            Assert.AreEqual(2, set.ModifierCount);
            Assert.AreEqual(1d, Speed, 1e-9, "Two additive stacks multiply: 4 * 0.5 * 0.5.");

            statuses.RemoveFromSource(Frost);

            Assert.AreEqual(1, set.ModifierCount);
            Assert.AreEqual(2d, Speed, 1e-9);
        }

        [Test]
        public void Replace_WithdrawsTheOldContributionAndAppliesTheNewOnce()
        {
            StatusDefinition slow = Slow(reapplication: ReapplicationPolicy.Replace);
            Apply(slow);

            Assert.AreEqual(StatusApplyResult.Replaced, Apply(slow));

            Assert.AreEqual(1, set.ModifierCount);
            Assert.AreEqual(2d, Speed, 1e-9);
        }

        [Test]
        public void RefreshAndIgnore_DoNotAddContributions()
        {
            StatusDefinition refresh = Slow(reapplication: ReapplicationPolicy.RefreshOnly);
            Apply(refresh);
            Apply(refresh);
            Assert.AreEqual(1, set.ModifierCount);
            statuses.Dispel(target.Id, refresh);

            StatusDefinition ignore = Slow(reapplication: ReapplicationPolicy.Ignore);
            Apply(ignore);
            Apply(ignore);
            Assert.AreEqual(1, set.ModifierCount);
        }

        [Test]
        public void Stacks_AddOneContributionEachUpToTheLimit_AndOverflowAddsNone()
        {
            StatusDefinition slow = Slow(maxStacks: 2, overflow: OverflowPolicy.Ignore);

            Apply(slow);
            Apply(slow);
            Apply(slow);

            Assert.AreEqual(2, set.ModifierCount);
        }

        [Test]
        public void OverflowReplaceOldest_SwapsTheContributionWithoutLeaking()
        {
            StatusDefinition slow = Slow(maxStacks: 2, overflow: OverflowPolicy.ReplaceOldest);
            Apply(slow);
            Apply(slow);

            Apply(slow);
            Apply(slow);

            Assert.AreEqual(2, set.ModifierCount);
            statuses.Dispel(target.Id, slow);
            Assert.AreEqual(0, set.ModifierCount);
        }

        [Test]
        public void StrongestOnly_CountsOneStackAtATime()
        {
            StatusDefinition slow = Slow(magnitude: MagnitudePolicy.StrongestOnly);
            Apply(slow, Frost, potency: 1d);
            Apply(slow, Ice, potency: 3d);

            Assert.AreEqual(1, set.ModifierCount);

            statuses.RemoveFromSource(Ice);

            Assert.AreEqual(1, set.ModifierCount, "The weaker stack takes over once the strongest ends.");
            statuses.Dispel(target.Id, slow);
            Assert.AreEqual(0, set.ModifierCount);
        }

        [Test]
        public void ServiceTeardown_WithdrawsEverything()
        {
            Apply(Slow());

            statuses.Dispose();

            Assert.AreEqual(0, set.ModifierCount);
            Assert.AreEqual(4d, Speed, 1e-9);
        }

        [Test]
        public void EntityRemoval_DropsTheStatusWithoutLeavingModifiers()
        {
            Apply(Slow());

            events.Enqueue(new EntityRemovedEvent(target.Id));
            events.Drain();

            Assert.AreEqual(0, set.ModifierCount);
        }

        [Test]
        public void StatusWithoutModifiers_TouchesNothing()
        {
            StatusDefinition plain = ScriptableObject.CreateInstance<StatusDefinition>();
            plain.AssignNewId();
            plain.Configure("Plain", 5f, 0f);
            created.Add(plain);

            Apply(plain);

            Assert.AreEqual(0, set.ModifierCount);
        }
    }
}
