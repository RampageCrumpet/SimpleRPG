using NUnit.Framework;
using SimpleRPG.Gameplay;
using System;

namespace SimpleRPG.Abilities.Tests.EditMode
{
    public class GameplayClockTests
    {
        [TestCase(0.0, 30, 0)]
        [TestCase(-1.0, 30, 0)]
        [TestCase(1.0, 30, 30)]
        [TestCase(0.1, 30, 3)]
        [TestCase(0.5, 30, 15)]
        [TestCase(0.001, 30, 1)]
        [TestCase(0.05, 30, 2)]
        public void SecondsToTicks_RoundsUpAndNeverEndsEarly(double seconds, int rate, long expected)
        {
            Assert.AreEqual(expected, GameplayTime.SecondsToTicks(seconds, rate));
        }

        [TestCase(0.1f, 30, 3)]
        [TestCase(0.6f, 30, 18)]
        [TestCase(0.25f, 30, 8)]
        [TestCase(0.7f, 30, 21)]
        [TestCase(1.1f, 30, 33)]
        [TestCase(5f, 30, 150)]
        public void SecondsToTicks_AuthoredFloats_AreNotRoundedUpABySingleTickByTheirImprecision(float seconds, int rate, long expected)
        {
            Assert.AreEqual(expected, GameplayTime.SecondsToTicks(seconds, rate));
        }

        [Test]
        public void SecondsToTicks_RejectsANonPositiveTickRate()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => GameplayTime.SecondsToTicks(1, 0));
        }

        [Test]
        public void ManualClock_AdvancesTicksAndSeconds()
        {
            ManualGameplayClock clock = new ManualGameplayClock(tickRate: 30);

            clock.Advance(45);

            Assert.AreEqual(45, clock.Tick);
            Assert.AreEqual(1.5, clock.ElapsedSeconds, 1e-9);
        }

        [Test]
        public void ManualClock_AdvanceSeconds_RoundsUpToWholeTicks()
        {
            ManualGameplayClock clock = new ManualGameplayClock(tickRate: 30);

            clock.AdvanceSeconds(0.05);

            Assert.AreEqual(2, clock.Tick);
        }

        [Test]
        public void ManualClock_CannotGoBackwards()
        {
            ManualGameplayClock clock = new ManualGameplayClock();

            Assert.Throws<ArgumentOutOfRangeException>(() => clock.Advance(-1));
        }
    }

    public class CooldownTrackerTests
    {
        private static readonly DefinitionId Firebolt = DefinitionId.NewId();
        private static readonly DefinitionId Icebolt = DefinitionId.NewId();

        [Test]
        public void UnusedDefinition_IsReady()
        {
            CooldownTracker cooldowns = new CooldownTracker(new ManualGameplayClock());

            Assert.IsTrue(cooldowns.IsReady(Firebolt));
            Assert.AreEqual(0, cooldowns.TicksRemaining(Firebolt));
        }

        [Test]
        public void TryStart_BlocksUntilTheCooldownElapses()
        {
            ManualGameplayClock clock = new ManualGameplayClock(tickRate: 30);
            CooldownTracker cooldowns = new CooldownTracker(clock);

            Assert.IsTrue(cooldowns.TryStart(Firebolt, 1.0));
            Assert.IsFalse(cooldowns.TryStart(Firebolt, 1.0));
            Assert.AreEqual(30, cooldowns.TicksRemaining(Firebolt));

            clock.Advance(29);
            Assert.IsFalse(cooldowns.IsReady(Firebolt));

            clock.Advance(1);
            Assert.IsTrue(cooldowns.IsReady(Firebolt));
            Assert.IsTrue(cooldowns.TryStart(Firebolt, 1.0));
        }

        [Test]
        public void RejectedUse_DoesNotRestartTheCooldown()
        {
            ManualGameplayClock clock = new ManualGameplayClock(tickRate: 30);
            CooldownTracker cooldowns = new CooldownTracker(clock);
            cooldowns.TryStart(Firebolt, 1.0);

            clock.Advance(20);
            cooldowns.TryStart(Firebolt, 1.0);

            Assert.AreEqual(10, cooldowns.TicksRemaining(Firebolt));
        }

        [Test]
        public void Cooldowns_AreIndependentPerDefinition()
        {
            CooldownTracker cooldowns = new CooldownTracker(new ManualGameplayClock());

            cooldowns.TryStart(Firebolt, 5.0);

            Assert.IsTrue(cooldowns.IsReady(Icebolt));
        }

        [Test]
        public void Tolerance_AcceptsAUseJustBeforeTheCooldownEnds()
        {
            ManualGameplayClock clock = new ManualGameplayClock(tickRate: 30);
            CooldownTracker cooldowns = new CooldownTracker(clock, toleranceTicks: 3);
            cooldowns.TryStart(Firebolt, 1.0);

            clock.Advance(26);
            Assert.IsFalse(cooldowns.IsReady(Firebolt));

            clock.Advance(1);
            Assert.IsTrue(cooldowns.IsReady(Firebolt));
        }

        [Test]
        public void ZeroCooldown_LeavesNothingToWaitFor()
        {
            CooldownTracker cooldowns = new CooldownTracker(new ManualGameplayClock());

            Assert.IsTrue(cooldowns.TryStart(Firebolt, 0));
            Assert.IsTrue(cooldowns.IsReady(Firebolt));
        }
    }
}
