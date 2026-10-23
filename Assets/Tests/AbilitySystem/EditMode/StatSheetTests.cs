using NUnit.Framework;
using SimpleRPG.Gameplay;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using EntityId = SimpleRPG.Gameplay.EntityId;

namespace SimpleRPG.Abilities.Tests.EditMode
{
    /// <summary>
    /// The stat catalog and sheets: the shared IDs, the Willpower rename, player (derived) and enemy (direct) resolution, and curves.
    /// The numbers here belong to the tests; the shipped balance lives in the authored profile assets.
    /// </summary>
    public class StatSheetTests
    {
        private static readonly SourceHandle Ring = SourceHandle.FromItem(new ItemInstanceId(1));

        private static StatSheet PlayerSheet() => new StatSheet(StatOwnership.Derived, new[]
        {
            new StatRule(DerivedStats.MaximumHealth, 0, new[] { new StatContribution(PrimaryAttributes.Constitution, ContributionCurve.Linear(10)) }),
            new StatRule(DerivedStats.MovementSpeed, 2, new[] { new StatContribution(PrimaryAttributes.Agility, ContributionCurve.Linear(0.2)) }),
            new StatRule(DerivedStats.PhysicalDamageReduction, 0, new[] { new StatContribution(PrimaryAttributes.Constitution, ContributionCurve.Diminishing(0.8, 20)) }),
            new StatRule(DerivedStats.AttackSpeed, 1, new[] { new StatContribution(DerivedStats.MovementSpeed, ContributionCurve.Linear(0.1)) }),
        });

        [Test]
        public void Catalog_HasTheTwelveDistinctDerivedStats()
        {
            Assert.AreEqual(12, DerivedStats.All.Length);
            CollectionAssert.AllItemsAreUnique(DerivedStats.All);
            Assert.IsTrue(DerivedStats.All.All(DerivedStats.IsCatalogued));
            Assert.IsFalse(DerivedStats.IsCatalogued(PrimaryAttributes.Strength));
            Assert.IsTrue(DerivedStats.All.All(x => Array.IndexOf(PrimaryAttributes.All, x) < 0), "No derived stat may collide with a core attribute.");
        }

        [Test]
        public void CoreAttributes_AreTheSixIncludingWillpower()
        {
            string[] keys = PrimaryAttributes.All.Select(x => x.Key.Substring(x.Key.LastIndexOf('.') + 1)).ToArray();

            CollectionAssert.AreEqual(new[] { "strength", "constitution", "dexterity", "agility", "willpower", "intelligence" }, keys);
        }

        [Test]
        public void LegacyWisdomKey_IsTheSameAttributeAsWillpower()
        {
            AttributeId legacy = new AttributeId(PrimaryAttributes.LegacyWisdomKey);
            AttributeSet set = new AttributeSet();
            set.SetBase(legacy, 14);

            Assert.AreEqual(PrimaryAttributes.Willpower, legacy);
            Assert.AreEqual(14d, set.Get(PrimaryAttributes.Willpower), "A value stored under the old key reads as Willpower.");
        }

        [Test]
        public void Player_ResolvesDerivedStatsFromCoreAttributes()
        {
            AttributeSet set = PlayerSheet().CreateSet();
            set.SetBase(PrimaryAttributes.Constitution, 12);
            set.SetBase(PrimaryAttributes.Agility, 10);

            Assert.AreEqual(120d, set.Get(DerivedStats.MaximumHealth), 1e-9);
            Assert.AreEqual(4d, set.Get(DerivedStats.MovementSpeed), 1e-9);
        }

        [Test]
        public void Player_DerivedStatCanBuildOnAnotherDerivedStat()
        {
            AttributeSet set = PlayerSheet().CreateSet();
            set.SetBase(PrimaryAttributes.Agility, 10);

            // Movement speed is 4, so attack speed is 1 + 0.4.
            Assert.AreEqual(1.4d, set.Get(DerivedStats.AttackSpeed), 1e-9);

            set.AddModifier(AttributeModifier.Flat(DerivedStats.MovementSpeed, 1, Ring));

            Assert.AreEqual(1.5d, set.Get(DerivedStats.AttackSpeed), 1e-9, "A modifier on a derived stat flows into the stats built on it.");
        }

        [Test]
        public void Player_ModifierOnCoreAttribute_ChangesDerivedValueAndRemovalRestoresIt()
        {
            AttributeSet set = PlayerSheet().CreateSet();
            set.SetBase(PrimaryAttributes.Constitution, 10);
            double before = set.Get(DerivedStats.MaximumHealth);

            ModifierHandle handle = set.AddModifier(AttributeModifier.Flat(PrimaryAttributes.Constitution, 5, Ring));
            Assert.AreEqual(150d, set.Get(DerivedStats.MaximumHealth), 1e-9);

            set.RemoveModifier(handle);
            Assert.AreEqual(before, set.Get(DerivedStats.MaximumHealth), 1e-9);
        }

        [Test]
        public void Player_ModifierOnDerivedStat_AppliesAfterTheFormula()
        {
            AttributeSet set = PlayerSheet().CreateSet();
            set.SetBase(PrimaryAttributes.Agility, 10);

            set.AddModifier(AttributeModifier.Factor(DerivedStats.MovementSpeed, 0.5, Ring));

            Assert.AreEqual(2d, set.Get(DerivedStats.MovementSpeed), 1e-9);
        }

        [Test]
        public void Player_ChangingACoreAttribute_InvalidatesTheDerivedValue()
        {
            AttributeSet set = PlayerSheet().CreateSet();
            set.SetBase(PrimaryAttributes.Constitution, 10);
            Assert.AreEqual(100d, set.Get(DerivedStats.MaximumHealth), 1e-9);

            set.SetBase(PrimaryAttributes.Constitution, 20);

            Assert.AreEqual(200d, set.Get(DerivedStats.MaximumHealth), 1e-9);
        }

        [Test]
        public void DiminishingCurve_RisesTowardsItsMaximumAndHalvesAtTheHalfPoint()
        {
            ContributionCurve curve = ContributionCurve.Diminishing(0.8, 20);

            Assert.AreEqual(0d, curve.Evaluate(0), 1e-9);
            Assert.AreEqual(0d, curve.Evaluate(-5), 1e-9);
            Assert.AreEqual(0.4d, curve.Evaluate(20), 1e-9);
            Assert.Less(curve.Evaluate(1000), 0.8d);
            Assert.Greater(curve.Evaluate(1000), 0.78d);
            Assert.Less(curve.Evaluate(40) - curve.Evaluate(20), curve.Evaluate(20) - curve.Evaluate(0), "Each further point is worth less.");
        }

        [Test]
        public void Player_DiminishingContribution_ResolvesThroughTheSheet()
        {
            AttributeSet set = PlayerSheet().CreateSet();
            set.SetBase(PrimaryAttributes.Constitution, 20);

            Assert.AreEqual(0.4d, set.Get(DerivedStats.PhysicalDamageReduction), 1e-9);
        }

        [Test]
        public void Curve_RejectsUnusableParameters()
        {
            Assert.Throws<ArgumentException>(() => ContributionCurve.Diminishing(1, 0));
            Assert.Throws<ArgumentException>(() => ContributionCurve.Linear(double.NaN));
        }

        [Test]
        public void Direct_ResolvesAuthoredValuesWithoutCoreAttributes()
        {
            StatSheet sheet = new StatSheet(StatOwnership.Direct, new[]
            {
                new StatRule(DerivedStats.MaximumHealth, 75),
                new StatRule(DerivedStats.MovementSpeed, 2.5),
            });

            AttributeSet set = sheet.CreateSet();

            Assert.AreEqual(75d, set.Get(DerivedStats.MaximumHealth));
            Assert.AreEqual(2.5d, set.Get(DerivedStats.MovementSpeed));
            Assert.AreEqual(0d, set.Get(PrimaryAttributes.Strength), "No core attribute is needed or supplied.");
            Assert.AreEqual(0d, set.Get(DerivedStats.MagicalPower), "A stat the sheet does not author reads as zero.");
        }

        [Test]
        public void Direct_ModifiersStillApplyOnTopOfAuthoredValues()
        {
            AttributeSet set = new StatSheet(StatOwnership.Direct, new[] { new StatRule(DerivedStats.MovementSpeed, 4) }).CreateSet();

            ModifierHandle handle = set.AddModifier(AttributeModifier.Factor(DerivedStats.MovementSpeed, 0.5, Ring));
            Assert.AreEqual(2d, set.Get(DerivedStats.MovementSpeed), 1e-9);

            set.RemoveModifier(handle);
            Assert.AreEqual(4d, set.Get(DerivedStats.MovementSpeed), 1e-9);
        }

        [Test]
        public void Direct_RejectsContributions()
        {
            Assert.Throws<ArgumentException>(() => new StatSheet(StatOwnership.Direct, new[]
            {
                new StatRule(DerivedStats.MaximumHealth, 1, new[] { new StatContribution(PrimaryAttributes.Constitution, ContributionCurve.Linear(1)) }),
            }));
        }

        [Test]
        public void Sheet_RejectsCoreAttributeAsAStatAndDuplicates()
        {
            Assert.Throws<ArgumentException>(() => new StatRule(PrimaryAttributes.Strength, 1));
            Assert.Throws<ArgumentException>(() => new StatSheet(StatOwnership.Derived, new[] { new StatRule(DerivedStats.MaximumHealth, 1), new StatRule(DerivedStats.MaximumHealth, 2) }));
        }

        [Test]
        public void SheetsShareOneSchemaButNotState()
        {
            StatSheet sheet = PlayerSheet();
            AttributeSet first = sheet.CreateSet();
            AttributeSet second = sheet.CreateSet();
            first.SetBase(PrimaryAttributes.Constitution, 10);
            second.SetBase(PrimaryAttributes.Constitution, 30);

            Assert.AreSame(sheet.Schema, sheet.Schema);
            Assert.AreEqual(100d, first.Get(DerivedStats.MaximumHealth), 1e-9);
            Assert.AreEqual(300d, second.Get(DerivedStats.MaximumHealth), 1e-9);
        }

        [Test]
        public void GrantEngine_ModifierHandles_WorkOnAPlayerResolvedSet()
        {
            // Item grants add modifiers to the same set through handles, whatever the set's profile.
            AttributeSetRegistry registry = new AttributeSetRegistry();
            SourceHandle wearer = SourceHandle.FromEntity(new EntityId(7));
            AttributeSet set = PlayerSheet().CreateSet();
            set.SetBase(PrimaryAttributes.Constitution, 10);
            registry.Register(wearer, set);

            registry.TryGetSet(wearer, out AttributeSet found);
            ModifierHandle handle = found.AddModifier(AttributeModifier.Flat(PrimaryAttributes.Constitution, 2, Ring));

            Assert.AreSame(set, found);
            Assert.AreEqual(120d, set.Get(DerivedStats.MaximumHealth), 1e-9);
            Assert.IsTrue(found.RemoveModifier(handle));
            Assert.AreEqual(100d, set.Get(DerivedStats.MaximumHealth), 1e-9);
        }

        [Test]
        public void Profile_BuildsFromAuthoredData_AndRejectsInvalidData()
        {
            StatProfile profile = ScriptableObject.CreateInstance<StatProfile>();
            try
            {
                profile.Configure(StatOwnership.Derived, new[]
                {
                    new StatRuleData
                    {
                        statKey = DerivedStats.MovementSpeed.Key,
                        baseValue = 1,
                        contributions = new List<StatContributionData>
                        {
                            new StatContributionData { sourceKey = PrimaryAttributes.Agility.Key, curve = CurveKind.Linear, scale = 0.3 },
                        },
                    },
                });

                AttributeSet set = profile.CreateSet();
                set.SetBase(PrimaryAttributes.Agility, 10);
                Assert.AreEqual(4d, set.Get(DerivedStats.MovementSpeed), 1e-9);

                profile.Configure(StatOwnership.Derived, new[] { new StatRuleData { statKey = PrimaryAttributes.Strength.Key } });
                Assert.Throws<ArgumentException>(() => { StatSheet unused = profile.Sheet; });
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(profile);
            }
        }
    }
}
