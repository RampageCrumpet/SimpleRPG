using NUnit.Framework;
using SimpleRPG.Delve;
using System;

namespace SimpleRPG.Tests.EditMode
{
    /// <summary>
    /// A bigger party meets more groups of enemies; a lone player gets exactly the dungeon they always did.
    /// </summary>
    public class PartyScalingTests
    {
        [Test]
        public void ALonePlayerMeetsOneGroup_AndUnknownSizesCountAsOne()
        {
            Assert.AreEqual(1f, PartyScaling.Multiplier(1, 0.5f));
            Assert.AreEqual(1f, PartyScaling.Multiplier(0, 0.5f));
            Assert.AreEqual(1f, PartyScaling.Multiplier(-3, 0.5f));
        }

        [Test]
        public void EachPlayerBeyondTheFirstAddsTheConfiguredAmount()
        {
            Assert.AreEqual(2f, PartyScaling.Multiplier(3, 0.5f), 1e-5f);
            Assert.AreEqual(2.5f, PartyScaling.Multiplier(4, 0.5f), 1e-5f);
            Assert.AreEqual(1f, PartyScaling.Multiplier(4, 0f), "With no scaling a big party meets what a lone one does.");
            Assert.AreEqual(1f, PartyScaling.Multiplier(4, -1f), "A negative setting cannot make the dungeon emptier.");
        }

        [Test]
        public void AWholeMultiplierGivesExactlyThatManyGroups_AndDrawsNothing()
        {
            var random = new Random(7);
            var untouched = new Random(7);

            Assert.AreEqual(1, PartyScaling.Groups(1, 0.5f, random));
            Assert.AreEqual(2, PartyScaling.Groups(3, 0.5f, random));

            Assert.AreEqual(untouched.NextDouble(), random.NextDouble(), "A multiplier with no fraction must not disturb the random stream, so a seed gives the same dungeon.");
        }

        [Test]
        public void AFractionIsAChanceOfOneMoreGroup()
        {
            var random = new Random(1234);
            const int trials = 20000;
            long total = 0;

            for (int i = 0; i < trials; i++)
            {
                int groups = PartyScaling.Groups(2, 0.5f, random);
                Assert.That(groups, Is.EqualTo(1).Or.EqualTo(2));
                total += groups;
            }

            Assert.AreEqual(1.5d, total / (double)trials, 0.03d, "A multiplier of 1.5 averages one and a half groups.");
        }
    }
}
