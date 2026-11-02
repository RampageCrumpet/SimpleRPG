using NUnit.Framework;
using SimpleRPG.Delve;
using System;

namespace SimpleRPG.Tests.EditMode
{
    /// <summary>
    /// How a delve ends: the party extracts together, or everyone dies.
    /// </summary>
    public class DelveRulesTests
    {
        // ---- The extraction channel ----

        [Test]
        public void Channel_RunsWhileTheWholeLivingPartyIsInTheZone_AndCompletes()
        {
            var channel = new ExtractionChannel(10f);

            channel.Step(4f, 3, 3);
            Assert.AreEqual(0.4f, channel.Progress, 1e-5f);
            Assert.IsFalse(channel.IsComplete);

            channel.Step(6f, 3, 3);
            Assert.IsTrue(channel.IsComplete);
            Assert.AreEqual(1f, channel.Progress, 1e-5f);
        }

        [Test]
        public void Channel_StartsOver_WhenAnyoneIsOutsideTheZone()
        {
            var channel = new ExtractionChannel(10f);
            channel.Step(8f, 3, 3);

            channel.Step(0.2f, 3, 2);

            Assert.AreEqual(0f, channel.Progress);
            channel.Step(5f, 3, 3);
            Assert.AreEqual(0.5f, channel.Progress, 1e-5f);
        }

        [Test]
        public void Channel_IgnoresTheDead_ButNeedsSomeoneAlive()
        {
            var channel = new ExtractionChannel(10f);

            // Two of three are dead; the one left standing in the zone is the whole living party.
            channel.Step(5f, 1, 1);
            Assert.AreEqual(0.5f, channel.Progress, 1e-5f);

            channel.Step(1f, 0, 0);
            Assert.AreEqual(0f, channel.Progress, "With nobody alive there is nobody to extract.");
        }

        [Test]
        public void Channel_StaysComplete_OnceDone()
        {
            var channel = new ExtractionChannel(1f);
            channel.Step(1f, 1, 1);

            channel.Step(1f, 2, 0);

            Assert.IsTrue(channel.IsComplete);
        }

        [Test]
        public void Channel_ResetStartsAgain()
        {
            var channel = new ExtractionChannel(1f);
            channel.Step(1f, 1, 1);

            channel.Reset();

            Assert.IsFalse(channel.IsComplete);
            Assert.AreEqual(0f, channel.Progress);
        }

        [Test]
        public void Channel_NeedsAPositiveTime()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new ExtractionChannel(0f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new ExtractionChannel(-1f));
        }

        [Test]
        public void Channel_NeverGoesBackwardsOnANegativeStep()
        {
            var channel = new ExtractionChannel(10f);
            channel.Step(5f, 1, 1);

            channel.Step(-3f, 1, 1);

            Assert.AreEqual(0.5f, channel.Progress, 1e-5f);
        }

        // ---- When the delve ends ----

        [Test]
        public void Delve_KeepsGoingWhileSomeoneIsAlive()
        {
            Assert.AreEqual(DelvePhase.Delving, DelveRules.Evaluate(true, 2, false));
        }

        [Test]
        public void Delve_IsWiped_WhenEveryoneIsDead()
        {
            Assert.AreEqual(DelvePhase.Wiped, DelveRules.Evaluate(true, 0, false));
        }

        [Test]
        public void Delve_IsNotWiped_BeforeAnyPlayerHasArrived()
        {
            Assert.AreEqual(DelvePhase.Delving, DelveRules.Evaluate(false, 0, false));
        }

        [Test]
        public void Delve_IsExtracted_WhenTheChannelCompletes()
        {
            Assert.AreEqual(DelvePhase.Extracted, DelveRules.Evaluate(true, 3, true));
        }

        [Test]
        public void Extraction_NeverCountsWithNobodyAlive()
        {
            Assert.AreEqual(DelvePhase.Wiped, DelveRules.Evaluate(true, 0, true));
        }
    }
}
