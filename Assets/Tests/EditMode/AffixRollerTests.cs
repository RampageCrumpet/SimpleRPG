using NUnit.Framework;
using SimpleRPG.Gameplay;
using SimpleRPG.InventorySystem;
using System;
using System.Collections.Generic;
using System.Linq;

namespace SimpleRPG.Tests.EditMode
{
    /// <summary>
    /// How an item instance rolls its affixes: how many, which, and what value. Fixed draw order, no repeats, ranges respected.
    /// </summary>
    public class AffixRollerTests
    {
        // A random stream that gives exactly the numbers it is told to, and counts how many were taken.
        private sealed class Scripted : IGameplayRandom
        {
            private readonly Queue<double> values;

            public Scripted(params double[] values) => this.values = new Queue<double>(values);

            public int Draws { get; private set; }

            public double NextDouble()
            {
                Draws++;
                return values.Count > 0 ? values.Dequeue() : 0d;
            }
        }

        private static AffixCandidate Candidate(uint id, float weight = 1f, float min = 1f, float max = 3f, string group = "") =>
            new AffixCandidate(id, weight, min, max, group);

        [Test]
        public void NothingToRoll_GivesNoAffixes_AndDrawsNothing()
        {
            var random = new Scripted();

            ItemAffixes none = AffixRoller.Roll(new List<AffixCandidate>(), 1, 2, random);

            Assert.IsTrue(none.IsEmpty);
            Assert.AreEqual(0, random.Draws, "An item with nothing to roll must not disturb the stream, or adding affixes to some items would change the loot of the rest.");
        }

        [Test]
        public void TheDrawOrderIsFixed_OneForTheCount_ThenTwoPerAffix()
        {
            var random = new Scripted(0.99, 0.0, 0.0, 0.0, 0.0);

            ItemAffixes rolled = AffixRoller.Roll(new[] { Candidate(1), Candidate(2), Candidate(3) }, 1, 2, random);

            Assert.AreEqual(2, rolled.Count, "0.99 of the way through 1..2 is two.");
            Assert.AreEqual(1 + 2 * 2, random.Draws);
        }

        [Test]
        public void TheCountIsBetweenTheMinimumAndMaximum_Inclusive()
        {
            var seen = new HashSet<int>();
            var random = new SeededRandom(11);
            AffixCandidate[] pool = { Candidate(1), Candidate(2), Candidate(3), Candidate(4) };

            for (int i = 0; i < 500; i++)
            {
                int count = AffixRoller.Roll(pool, 1, 3, random).Count;
                Assert.That(count, Is.InRange(1, 3));
                seen.Add(count);
            }

            CollectionAssert.AreEquivalent(new[] { 1, 2, 3 }, seen, "Every count in the range comes up.");
        }

        [Test]
        public void ThePickFollowsTheWeights()
        {
            // Weights 1 and 3: the first quarter of the range is the first affix, the rest the second.
            AffixCandidate[] pool = { Candidate(10, weight: 1f), Candidate(20, weight: 3f) };

            Assert.AreEqual(10u, AffixRoller.Roll(pool, 1, 1, new Scripted(0.0, 0.10, 0.5))[0].AffixId);
            Assert.AreEqual(20u, AffixRoller.Roll(pool, 1, 1, new Scripted(0.0, 0.30, 0.5))[0].AffixId);
            Assert.AreEqual(20u, AffixRoller.Roll(pool, 1, 1, new Scripted(0.0, 0.99, 0.5))[0].AffixId);
        }

        [Test]
        public void AnAffixNeverRollsTwice_AndNeitherDoesItsGroup()
        {
            var random = new SeededRandom(5);
            AffixCandidate[] pool =
            {
                Candidate(1, group: "strength"), Candidate(2, group: "strength"), Candidate(3, group: "armor"), Candidate(4, group: "armor"),
            };

            for (int i = 0; i < 300; i++)
            {
                ItemAffixes rolled = AffixRoller.Roll(pool, 4, 4, random);
                var ids = Enumerable.Range(0, rolled.Count).Select(n => rolled[n].AffixId).ToList();

                CollectionAssert.AllItemsAreUnique(ids);
                Assert.LessOrEqual(ids.Count(id => id <= 2), 1, "At most one of the strength group.");
                Assert.LessOrEqual(ids.Count(id => id >= 3), 1, "At most one of the armor group.");
                Assert.AreEqual(2, rolled.Count, "Two groups, so two affixes however many were asked for.");
            }
        }

        [Test]
        public void ValuesStayInsideTheRange_AndAreWholeThousandths()
        {
            var random = new SeededRandom(99);

            for (int i = 0; i < 300; i++)
            {
                AffixRoll roll = AffixRoller.Roll(new[] { Candidate(1, min: 2f, max: 5f) }, 1, 1, random)[0];

                Assert.That(roll.ValueMilli, Is.InRange(2000, 5000));
                Assert.AreEqual(roll.ValueMilli / 1000f, roll.Value, 1e-6f);
            }
        }

        [Test]
        public void ARangeOfOnePoint_AlwaysRollsThatValue()
        {
            AffixRoll roll = AffixRoller.Roll(new[] { Candidate(1, min: 4f, max: 4f) }, 1, 1, new SeededRandom(3))[0];

            Assert.AreEqual(4000, roll.ValueMilli);
        }

        [Test]
        public void NoMoreThanTheMaximumPossible_EvenIfAskedFor()
        {
            AffixCandidate[] many = Enumerable.Range(1, 9).Select(i => Candidate((uint)i)).ToArray();

            Assert.AreEqual(ItemAffixes.MaxAffixes, AffixRoller.Roll(many, 9, 9, new SeededRandom(1)).Count);
        }

        [Test]
        public void TheSameSeedRollsTheSameItem()
        {
            AffixCandidate[] pool = Enumerable.Range(1, 6).Select(i => Candidate((uint)i, min: 1f, max: 10f)).ToArray();

            ItemAffixes a = AffixRoller.Roll(pool, 1, 4, new SeededRandom(1234));
            ItemAffixes b = AffixRoller.Roll(pool, 1, 4, new SeededRandom(1234));
            ItemAffixes c = AffixRoller.Roll(pool, 1, 4, new SeededRandom(4321));

            Assert.AreEqual(a, b);
            Assert.AreNotEqual(a, c);
        }

        [Test]
        public void AZeroMaximum_RollsNothing()
        {
            Assert.IsTrue(AffixRoller.Roll(new[] { Candidate(1) }, 0, 0, new SeededRandom(1)).IsEmpty);
        }
    }

    /// <summary>
    /// The packed form of an item's affixes.
    /// </summary>
    public class ItemAffixesPackingTests
    {
        [Test]
        public void NothingPacksToEmpty()
        {
            Assert.IsTrue(ItemAffixes.From(new AffixRoll[0]).IsEmpty);
            Assert.IsTrue(ItemAffixes.From(null).IsEmpty);
            Assert.AreEqual(0, default(ItemAffixes).Count);
        }

        [Test]
        public void RollsPackInOrder_AndReadBack()
        {
            ItemAffixes packed = ItemAffixes.From(new[] { new AffixRoll(7, 2500), new AffixRoll(9, -1500), new AffixRoll(4, 100) });

            Assert.AreEqual(3, packed.Count);
            Assert.AreEqual(7u, packed[0].AffixId);
            Assert.AreEqual(2500, packed[0].ValueMilli);
            Assert.AreEqual(2.5f, packed[0].Value, 1e-6f);
            Assert.AreEqual(-1.5f, packed[1].Value, 1e-6f);
            Assert.AreEqual(4u, packed[2].AffixId);
        }

        [Test]
        public void MoreThanFourAreCut_AndAnEmptyIdIsLeftOut()
        {
            var rolls = new List<AffixRoll> { new AffixRoll(1, 1), new AffixRoll(0, 99), new AffixRoll(2, 2), new AffixRoll(3, 3), new AffixRoll(4, 4), new AffixRoll(5, 5) };

            ItemAffixes packed = ItemAffixes.From(rolls);

            Assert.AreEqual(4, packed.Count);
            Assert.AreEqual(4u, packed[3].AffixId, "The empty id was skipped, so the fourth is id 4.");
        }

        [Test]
        public void TheIndexerRefusesAPlaceThatIsEmpty()
        {
            ItemAffixes packed = ItemAffixes.From(new[] { new AffixRoll(1, 1) });

            Assert.Throws<ArgumentOutOfRangeException>(() => { var _ = packed[1]; });
            Assert.Throws<ArgumentOutOfRangeException>(() => { var _ = packed[-1]; });
        }

        [Test]
        public void EqualRollsAreEqual_AndADifferentValueIsNot()
        {
            ItemAffixes a = ItemAffixes.From(new[] { new AffixRoll(1, 1000), new AffixRoll(2, 2000) });
            ItemAffixes same = ItemAffixes.From(new[] { new AffixRoll(1, 1000), new AffixRoll(2, 2000) });
            ItemAffixes different = ItemAffixes.From(new[] { new AffixRoll(1, 1000), new AffixRoll(2, 2001) });

            Assert.AreEqual(a, same);
            Assert.AreEqual(a.GetHashCode(), same.GetHashCode());
            Assert.AreNotEqual(a, different);
        }

        [Test]
        public void ValuesAreKeptInThousandths()
        {
            Assert.AreEqual(2500, AffixRoll.ToMilli(2.5));
            Assert.AreEqual(1235, AffixRoll.ToMilli(1.2346));
            Assert.AreEqual(-500, AffixRoll.ToMilli(-0.5));
        }
    }
}
