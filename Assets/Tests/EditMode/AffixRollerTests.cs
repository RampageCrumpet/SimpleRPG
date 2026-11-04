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

        private static AffixCandidate Candidate(uint id, float weight = 1f, float min = 1f, float max = 3f, string family = "", AffixKind kind = AffixKind.Prefix, int values = 1)
        {
            var ranges = new List<AffixValueRange>();
            for (int i = 0; i < values; i++) ranges.Add(new AffixValueRange(min, max));
            return new AffixCandidate(id, weight, kind, family, ranges);
        }

        // Plenty of room for each kind, so a test about something else is not limited by it.
        private static AffixLimits Limits(int min, int max) => new AffixLimits(min, max, 99, 99);

        [Test]
        public void NothingToRoll_GivesNoAffixes_AndDrawsNothing()
        {
            var random = new Scripted();

            ItemAffixes none = AffixRoller.Roll(new List<AffixCandidate>(), Limits(1, 2), random);

            Assert.IsTrue(none.IsEmpty);
            Assert.AreEqual(0, random.Draws, "An item with nothing to roll must not disturb the stream, or adding affixes to some items would change the loot of the rest.");
        }

        [Test]
        public void TheDrawOrderIsFixed_OneForTheCount_ThenTwoPerAffix()
        {
            var random = new Scripted(0.99, 0.0, 0.0, 0.0, 0.0);

            ItemAffixes rolled = AffixRoller.Roll(new[] { Candidate(1), Candidate(2), Candidate(3) }, Limits(1, 2), random);

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
                int count = AffixRoller.Roll(pool, Limits(1, 3), random).Count;
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

            Assert.AreEqual(10u, AffixRoller.Roll(pool, Limits(1, 1), new Scripted(0.0, 0.10, 0.5))[0].AffixId);
            Assert.AreEqual(20u, AffixRoller.Roll(pool, Limits(1, 1), new Scripted(0.0, 0.30, 0.5))[0].AffixId);
            Assert.AreEqual(20u, AffixRoller.Roll(pool, Limits(1, 1), new Scripted(0.0, 0.99, 0.5))[0].AffixId);
        }

        [Test]
        public void AnAffixNeverRollsTwice_AndNeitherDoesItsFamily()
        {
            var random = new SeededRandom(5);
            AffixCandidate[] pool =
            {
                Candidate(1, family: "strength"), Candidate(2, family: "strength"), Candidate(3, family: "armor"), Candidate(4, family: "armor"),
            };

            for (int i = 0; i < 300; i++)
            {
                ItemAffixes rolled = AffixRoller.Roll(pool, Limits(4, 4), random);
                var ids = Enumerable.Range(0, rolled.Count).Select(n => rolled[n].AffixId).ToList();

                CollectionAssert.AllItemsAreUnique(ids);
                Assert.LessOrEqual(ids.Count(id => id <= 2), 1, "At most one of the strength family.");
                Assert.LessOrEqual(ids.Count(id => id >= 3), 1, "At most one of the armor family.");
                Assert.AreEqual(2, rolled.Count, "Two families, so two affixes however many were asked for.");
            }
        }

        [Test]
        public void ValuesStayInsideTheRange_AndAreWholeThousandths()
        {
            var random = new SeededRandom(99);

            for (int i = 0; i < 300; i++)
            {
                AffixRoll roll = AffixRoller.Roll(new[] { Candidate(1, min: 2f, max: 5f) }, Limits(1, 1), random)[0];

                Assert.That(roll.ValuesMilli[0], Is.InRange(2000, 5000));
                Assert.AreEqual(roll.ValuesMilli[0] / 1000f, roll.Value(0), 1e-6f);
            }
        }

        [Test]
        public void ARangeOfOnePoint_AlwaysRollsThatValue()
        {
            AffixRoll roll = AffixRoller.Roll(new[] { Candidate(1, min: 4f, max: 4f) }, Limits(1, 1), new SeededRandom(3))[0];

            Assert.AreEqual(4000, roll.ValuesMilli[0]);
        }

        [Test]
        public void NoMoreThanTheMaximumIsRolled_EvenIfMoreAreAvailable()
        {
            AffixCandidate[] many = Enumerable.Range(1, 9).Select(i => Candidate((uint)i)).ToArray();

            Assert.AreEqual(3, AffixRoller.Roll(many, Limits(3, 3), new SeededRandom(1)).Count);
        }

        [Test]
        public void PrefixesAndSuffixesAreLimitedSeparately()
        {
            var random = new SeededRandom(8);
            AffixCandidate[] pool =
            {
                Candidate(1, kind: AffixKind.Prefix), Candidate(2, kind: AffixKind.Prefix), Candidate(3, kind: AffixKind.Prefix),
                Candidate(4, kind: AffixKind.Suffix), Candidate(5, kind: AffixKind.Suffix), Candidate(6, kind: AffixKind.Suffix),
            };

            for (int i = 0; i < 200; i++)
            {
                ItemAffixes rolled = AffixRoller.Roll(pool, new AffixLimits(6, 6, 1, 2), random);
                var ids = Enumerable.Range(0, rolled.Count).Select(n => rolled[n].AffixId).ToList();

                Assert.AreEqual(1, ids.Count(id => id <= 3), "One prefix at most.");
                Assert.AreEqual(2, ids.Count(id => id >= 4), "Two suffixes at most.");
            }
        }

        [Test]
        public void AnAffixCanRollAnyNumberOfValues_EachDrawnInOrderWithItsOwnRange()
        {
            var ranges = new List<AffixValueRange> { new AffixValueRange(0f, 10f), new AffixValueRange(100f, 200f), new AffixValueRange(5f, 5f), new AffixValueRange(-2f, -1f) };
            var random = new Scripted(0.0, 0.0, 0.5, 0.25, 0.9, 0.5);

            ItemAffixes rolled = AffixRoller.Roll(new[] { new AffixCandidate(7, 1f, AffixKind.Suffix, "", ranges) }, Limits(1, 1), random);

            AffixRoll roll = rolled[0];
            Assert.AreEqual(4, roll.ValueCount);
            CollectionAssert.AreEqual(new[] { 5000, 125000, 5000, -1500 }, roll.ValuesMilli);
            Assert.AreEqual(1 + 1 + 4, random.Draws, "One for the count, one to pick, one per value.");
        }

        [Test]
        public void AnAffixThatWouldNotFitTheItem_IsNotRolled()
        {
            // Each takes 2 + 6 = 8 words; an item has room for ItemAffixes.WordCapacity.
            AffixCandidate[] big = Enumerable.Range(1, 10).Select(i => Candidate((uint)i, values: 6)).ToArray();

            ItemAffixes rolled = AffixRoller.Roll(big, Limits(10, 10), new SeededRandom(2));

            Assert.AreEqual(ItemAffixes.WordCapacity / 8, rolled.Count);
            Assert.LessOrEqual(rolled.WordsUsed, ItemAffixes.WordCapacity);
        }

        [Test]
        public void TheSameSeedRollsTheSameItem()
        {
            AffixCandidate[] pool = Enumerable.Range(1, 6).Select(i => Candidate((uint)i, min: 1f, max: 10f)).ToArray();

            ItemAffixes a = AffixRoller.Roll(pool, Limits(1, 4), new SeededRandom(1234));
            ItemAffixes b = AffixRoller.Roll(pool, Limits(1, 4), new SeededRandom(1234));
            ItemAffixes c = AffixRoller.Roll(pool, Limits(1, 4), new SeededRandom(4321));

            Assert.AreEqual(a, b);
            Assert.AreNotEqual(a, c);
        }

        [Test]
        public void AZeroMaximum_RollsNothing()
        {
            Assert.IsTrue(AffixRoller.Roll(new[] { Candidate(1) }, Limits(0, 0), new SeededRandom(1)).IsEmpty);
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
        public void RollsPackInOrder_WithAnyNumberOfValues_AndReadBack()
        {
            ItemAffixes packed = ItemAffixes.From(new[] { new AffixRoll(7, 2500), new AffixRoll(9, -1500, 40, 7), new AffixRoll(4) });

            Assert.AreEqual(3, packed.Count);
            Assert.AreEqual(7u, packed[0].AffixId);
            Assert.AreEqual(2.5f, packed[0].Value(0), 1e-6f);
            Assert.AreEqual(3, packed[1].ValueCount);
            Assert.AreEqual(-1.5f, packed[1].Value(0), 1e-6f);
            Assert.AreEqual(7, packed[1].ValuesMilli[2]);
            Assert.AreEqual(4u, packed[2].AffixId);
            Assert.AreEqual(0, packed[2].ValueCount);
            Assert.AreEqual(2 + 1 + 2 + 3 + 2, packed.WordsUsed);
        }

        [Test]
        public void ARollThatDoesNotFitIsLeftOut_AndAnEmptyIdIsLeftOut()
        {
            var rolls = new List<AffixRoll> { new AffixRoll(1, new int[20]), new AffixRoll(0, 99), new AffixRoll(2, new int[20]), new AffixRoll(3, 5) };

            ItemAffixes packed = ItemAffixes.From(rolls);

            Assert.AreEqual(2, packed.Count, "Affix 1 fits (22 words), 2 does not (44), 3 does (3 more words).");
            Assert.AreEqual(1u, packed[0].AffixId);
            Assert.AreEqual(3u, packed[1].AffixId);
        }

        [Test]
        public void ADamagedRunNeverReadsPastTheEnd()
        {
            // An id, then a value count far larger than the item has room for.
            ItemAffixes damaged = ItemAffixes.From(new[] { new AffixRoll(1, 1) });
            var field = typeof(ItemAffixes).GetField("block0", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            object block = field.GetValue(damaged);
            block.GetType().GetField("w1", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).SetValue(block, 9999);
            object boxed = damaged;
            field.SetValue(boxed, block);
            damaged = (ItemAffixes)boxed;

            Assert.AreEqual(0, damaged.Count);
            Assert.AreEqual(0, damaged.WordsUsed);
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
            ItemAffixes a = ItemAffixes.From(new[] { new AffixRoll(1, 1000), new AffixRoll(2, 2000, 5) });
            ItemAffixes same = ItemAffixes.From(new[] { new AffixRoll(1, 1000), new AffixRoll(2, 2000, 5) });
            ItemAffixes different = ItemAffixes.From(new[] { new AffixRoll(1, 1000), new AffixRoll(2, 2000, 6) });

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
