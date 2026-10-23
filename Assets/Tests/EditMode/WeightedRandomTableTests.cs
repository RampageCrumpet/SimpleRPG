using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using SimpleRPG.InventorySystem.LootGeneration;
using UnityEngine;
using UnityEngine.TestTools;

namespace SimpleRPG.Tests.EditMode
{
    /// <summary>
    /// Checks weighted loot selection: proportional picks, invalid weights, empty and null-only tables, Always entries,
    /// roll counts, unique entries, callbacks and seeded reproducibility.
    /// </summary>
    public class WeightedRandomTableTests
    {
        private const int Seeds = 200;

        private readonly List<ScriptableObject> created = new List<ScriptableObject>();

        [TearDown]
        public void TearDown()
        {
            foreach (ScriptableObject asset in created)
            {
                UnityEngine.Object.DestroyImmediate(asset);
            }

            created.Clear();
        }

        [Test]
        public void WeightedSelection_IsProportionalToWeight()
        {
            LootableItem light = Entry(weight: 1);
            LootableItem heavy = Entry(weight: 3);
            WeightedRandomTable table = Table(count: 1, light, heavy);
            var random = new System.Random(7);

            int lightPicks = 0;
            const int rolls = 10000;
            for (int i = 0; i < rolls; i++)
            {
                if (Pick(table, random).Single() == light)
                {
                    lightPicks++;
                }
            }

            // Expect roughly 25% for the light entry; the bounds are far wider than the sampling noise for a fixed seed.
            Assert.That(lightPicks, Is.InRange(2300, 2700));
        }

        [Test]
        public void ZeroWeight_IsNeverSelected()
        {
            LootableItem zero = Entry(weight: 0);
            LootableItem one = Entry(weight: 1);
            WeightedRandomTable table = Table(count: 1, zero, one);
            var random = new System.Random(1);

            for (int i = 0; i < 1000; i++)
            {
                Assert.AreSame(one, Pick(table, random).Single());
            }
        }

        [Test]
        public void NegativeOrNonFiniteWeight_IsIgnored()
        {
            LootableItem negative = Entry(weight: -5);
            LootableItem notANumber = Entry(weight: double.NaN);
            LootableItem infinite = Entry(weight: double.PositiveInfinity);
            LootableItem valid = Entry(weight: 1);
            WeightedRandomTable table = Table(count: 1, negative, notANumber, infinite, valid);
            var random = new System.Random(2);

            for (int i = 0; i < 50; i++)
            {
                // Each roll warns once per rejected entry.
                for (int rejected = 0; rejected < 3; rejected++)
                {
                    LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex("is not a finite non-negative weight"));
                }

                Assert.AreSame(valid, Pick(table, random).Single());
            }
        }

        [Test]
        public void AllWeightsZero_YieldsNoDrop_AndTerminates()
        {
            WeightedRandomTable table = Table(count: 3, Entry(weight: 0), Entry(weight: 0));

            Assert.That(Pick(table, new System.Random(3)), Is.Empty);
        }

        [Test]
        public void EmptyTable_YieldsNoDrop()
        {
            WeightedRandomTable table = Table(count: 3);
            WeightedRandomTable nullContents = Table(count: 3);
            nullContents.Contents = null;

            Assert.That(Pick(table, new System.Random(4)), Is.Empty);
            Assert.That(Pick(nullContents, new System.Random(4)), Is.Empty);
        }

        [Test]
        public void NullOnlyTable_Terminates_WithNoDrop()
        {
            WeightedRandomTable table = Table(count: 5);
            table.Contents = new List<WeightedRandomEntry> { Create<NullLoot>(weight: 1), Create<NullLoot>(weight: 1) };

            Assert.That(Pick(table, new System.Random(5)), Is.Empty);
        }

        [Test]
        public void NullEntriesInContents_AreSkipped()
        {
            LootableItem valid = Entry(weight: 1);
            WeightedRandomTable table = Table(count: 2, valid);
            table.Contents.Insert(0, null);
            table.Contents.Add(null);
            var random = new System.Random(6);

            for (int i = 0; i < Seeds; i++)
            {
                List<WeightedRandomEntry> result = Pick(table, random);
                Assert.That(result, Has.Count.EqualTo(2));
                Assert.That(result.All(x => x == valid), Is.True);
            }
        }

        [Test]
        public void NullLoot_CanProduceNoDrop_AsAChance()
        {
            LootableItem item = Entry(weight: 1);
            WeightedRandomTable table = Table(count: 1, Create<NullLoot>(weight: 1), item);

            int noDrop = 0;
            int drop = 0;
            for (int seed = 0; seed < Seeds; seed++)
            {
                List<WeightedRandomEntry> result = Pick(table, new System.Random(seed));
                if (result.Count == 0)
                {
                    noDrop++;
                }
                else
                {
                    Assert.AreSame(item, result.Single());
                    drop++;
                }
            }

            Assert.That(noDrop, Is.GreaterThan(0));
            Assert.That(drop, Is.GreaterThan(0));
        }

        [Test]
        public void Count_BoundsTheNumberOfRolls()
        {
            WeightedRandomTable table = Table(count: 3, Entry(weight: 1), Entry(weight: 2));

            for (int seed = 0; seed < Seeds; seed++)
            {
                Assert.That(Pick(table, new System.Random(seed)), Has.Count.EqualTo(3));
            }
        }

        [Test]
        public void NonPositiveCount_YieldsNoRolls()
        {
            LootableItem item = Entry(weight: 1);

            Assert.That(Pick(Table(count: 0, item), new System.Random(8)), Is.Empty);
            Assert.That(Pick(Table(count: -4, item), new System.Random(8)), Is.Empty);
        }

        [Test]
        public void AlwaysEntries_AllApply_AndDisabledOnesDoNot()
        {
            LootableItem alwaysA = Entry(weight: 0, always: true);
            LootableItem alwaysB = Entry(weight: 0, always: true);
            LootableItem alwaysDisabled = Entry(weight: 0, always: true, enabled: false);
            LootableItem weighted = Entry(weight: 1);
            WeightedRandomTable table = Table(count: 1, alwaysA, alwaysB, alwaysDisabled, weighted);

            List<WeightedRandomEntry> result = Pick(table, new System.Random(9));

            Assert.That(result, Has.Count.EqualTo(3));
            Assert.That(result.Count(x => x == alwaysA), Is.EqualTo(1));
            Assert.That(result.Count(x => x == alwaysB), Is.EqualTo(1));
            Assert.That(result.Contains(alwaysDisabled), Is.False);
            Assert.That(result.Count(x => x == weighted), Is.EqualTo(1));
        }

        [Test]
        public void AlwaysEntries_DoNotCountAgainstRolls()
        {
            LootableItem always = Entry(weight: 0, always: true);
            WeightedRandomTable table = Table(count: 0, always);

            Assert.That(Pick(table, new System.Random(10)).Single(), Is.SameAs(always));
        }

        [Test]
        public void UniqueEntries_DropAtMostOnce()
        {
            LootableItem unique = Entry(weight: 1, unique: true);
            LootableItem common = Entry(weight: 1);
            WeightedRandomTable table = Table(count: 5, unique, common);

            for (int seed = 0; seed < Seeds; seed++)
            {
                List<WeightedRandomEntry> result = Pick(table, new System.Random(seed));
                Assert.That(result.Count(x => x == unique), Is.LessThanOrEqualTo(1));
                Assert.That(result, Has.Count.EqualTo(5));
            }
        }

        [Test]
        public void UniqueEntries_ExhaustedTable_StopsWithoutLooping()
        {
            LootableItem unique = Entry(weight: 1, unique: true);
            WeightedRandomTable table = Table(count: 10, unique);

            List<WeightedRandomEntry> result = Pick(table, new System.Random(11));

            Assert.That(result.Single(), Is.SameAs(unique));
        }

        [Test]
        public void DuplicateEntries_AreWeightedAsSeparateEntries()
        {
            LootableItem item = Entry(weight: 1);
            WeightedRandomTable table = Table(count: 1, item, item);

            for (int seed = 0; seed < Seeds; seed++)
            {
                Assert.AreSame(item, Pick(table, new System.Random(seed)).Single());
            }
        }

        [Test]
        public void Callbacks_FireOnceForTheTableAndOncePerSelection()
        {
            LootableItem item = Entry(weight: 1);
            int preCalls = 0;
            int selectedCalls = 0;
            int postCalls = 0;
            ResultEventArgs postArgs = null;
            item.PreResultEvaluation += (sender, e) => preCalls++;
            item.ObjectSelected += (sender, e) => selectedCalls++;
            item.PostResultEvaluation += (sender, e) =>
            {
                postCalls++;
                postArgs = (ResultEventArgs)e;
            };
            WeightedRandomTable table = Table(count: 2, item);

            List<WeightedRandomEntry> result = Pick(table, new System.Random(12));

            Assert.That(preCalls, Is.EqualTo(1));
            Assert.That(selectedCalls, Is.EqualTo(2));
            Assert.That(postCalls, Is.EqualTo(1));
            Assert.That(postArgs.LootResult.Count(), Is.EqualTo(result.Count));
        }

        [Test]
        public void SeededRandom_IsReproducible_IncludingNestedTables()
        {
            WeightedRandomTable nested = Table(count: 2, Entry(weight: 1), Entry(weight: 2), Entry(weight: 3));
            WeightedRandomTable outer = Table(count: 4, nested, Entry(weight: 1), Create<NullLoot>(weight: 1));

            for (int seed = 0; seed < Seeds; seed++)
            {
                List<WeightedRandomEntry> first = Pick(outer, new System.Random(seed));
                List<WeightedRandomEntry> second = Pick(outer, new System.Random(seed));

                Assert.That(second, Is.EqualTo(first));
            }
        }

        private LootableItem Entry(double weight, bool always = false, bool unique = false, bool enabled = true)
        {
            LootableItem entry = Create<LootableItem>(weight);
            entry.Always = always;
            entry.Unique = unique;
            entry.Enabled = enabled;
            return entry;
        }

        private T Create<T>(double weight) where T : WeightedRandomEntry
        {
            T entry = ScriptableObject.CreateInstance<T>();
            entry.DropChance = weight;
            created.Add(entry);
            return entry;
        }

        private WeightedRandomTable Table(int count, params WeightedRandomEntry[] contents)
        {
            WeightedRandomTable table = ScriptableObject.CreateInstance<WeightedRandomTable>();
            table.Count = count;
            table.Contents = contents.ToList();
            created.Add(table);
            return table;
        }

        private static List<WeightedRandomEntry> Pick(WeightedRandomTable table, System.Random random)
        {
            return table.GetResult(random).ToList();
        }
    }
}
