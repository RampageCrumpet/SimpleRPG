using NUnit.Framework;
using SimpleRPG.Abilities;
using SimpleRPG.Gameplay;
using SimpleRPG.InventorySystem;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace SimpleRPG.Tests.EditMode
{
    /// <summary>
    /// Rare items: enough affixes makes an item rare, it is named from two words of a list the owner supplies, and the name is fixed at the roll.
    /// </summary>
    public class RareNameTests
    {
        private readonly List<Object> created = new List<Object>();
        private DefinitionRegistry previousRegistry;

        [SetUp]
        public void SetUp() => previousRegistry = DefinitionRegistry.Instance;

        [TearDown]
        public void TearDown()
        {
            DefinitionRegistry.Instance = previousRegistry;
            foreach (Object o in created)
            {
                if (o != null) Object.DestroyImmediate(o);
            }

            created.Clear();
        }

        private static void Set(object target, string field, object value)
        {
            for (System.Type type = target.GetType(); type != null; type = type.BaseType)
            {
                FieldInfo info = type.GetField(field, BindingFlags.Instance | BindingFlags.NonPublic);
                if (info != null)
                {
                    info.SetValue(target, value);
                    return;
                }
            }

            throw new System.ArgumentException(field);
        }

        private T Track<T>(T made) where T : Object
        {
            created.Add(made);
            return made;
        }

        private static NameWord Word(int id, string text) => new NameWord { id = id, text = text };

        private RareNames Names()
        {
            RareNames names = Track(ScriptableObject.CreateInstance<RareNames>());
            Set(names, "names", new List<NameWord> { Word(1, "Grave Bite"), Word(2, "Iron Requiem"), Word(3, "Dusk Reaver") });
            return names;
        }

        private AffixDefinition Affix(string display, AffixKind kind, string key)
        {
            AffixDefinition affix = Track(ScriptableObject.CreateInstance<AffixDefinition>());
            affix.EnsureId();
            Set(affix, "displayName", display);
            Set(affix, "kind", kind);
            Set(affix, "effects", new List<AffixEffect> { new StatModifierEffect(AttributeLabels.For(key), key, ModifierLayer.Add, 1f, 1f) });
            return affix;
        }

        // An armor asset whose pool rolls a prefix and a suffix and, with the given chance, makes the item rare.
        private ArmorData Armor(RareNames names, float rareChance)
        {
            AffixDefinition prefix = Affix("Riveted", AffixKind.Prefix, "simplerpg.stat.armor_rating");
            AffixDefinition suffix = Affix("of the Bear", AffixKind.Suffix, "simplerpg.attr.constitution");
            DefinitionRegistry registry = Track(ScriptableObject.CreateInstance<DefinitionRegistry>());
            Assert.IsTrue(registry.Rebuild(new GameplayDefinition[] { prefix, suffix }).Success);
            DefinitionRegistry.Instance = registry;

            AffixPool pool = Track(ScriptableObject.CreateInstance<AffixPool>());
            Set(pool, "affixes", new List<AffixDefinition> { prefix, suffix });
            Set(pool, "minimumCount", 2);
            Set(pool, "maximumCount", 2);
            Set(pool, "maximumPrefixes", 1);
            Set(pool, "maximumSuffixes", 1);
            Set(pool, "rareNames", names);
            Set(pool, "rareChance", rareChance);
            Set(pool, "rareMinimumCount", 1);
            Set(pool, "rareMaximumCount", 2);
            Set(pool, "rareMaximumPrefixes", 1);
            Set(pool, "rareMaximumSuffixes", 1);

            ArmorData armor = Track(ScriptableObject.CreateInstance<ArmorData>());
            armor.EnsureId();
            Set(armor, "<ItemName>k__BackingField", "Plate Armor");
            Set(armor, "affixPool", pool);
            return armor;
        }

        [Test]
        public void ANameIsPickedBySampleAcrossTheList()
        {
            RareNames names = Names();

            Assert.AreEqual(1, names.Pick(0.0));
            Assert.AreEqual(2, names.Pick(0.5));
            Assert.AreEqual(3, names.Pick(0.999));
            Assert.AreEqual(0, Track(ScriptableObject.CreateInstance<RareNames>()).Pick(0.5), "No names, no pick.");
        }

        [Test]
        public void ARareItem_IsCalledByItsName_NotByItsAffixes_WithTheBaseItemBeneath()
        {
            ArmorData armor = Armor(Names(), rareChance: 1f);

            Item instance = Track(armor.CloneInstance(armor.RollAffixes(new SeededRandom(7))));

            Assert.IsTrue(instance.Affixes.IsRare);
            StringAssert.DoesNotContain("Riveted", instance.DisplayName);
            CollectionAssert.Contains(new[] { "Grave Bite", "Iron Requiem", "Dusk Reaver" }, instance.DisplayName);
            Assert.AreEqual(instance.DisplayName, instance.RareName);
            var lines = ItemTooltip.Build(instance);
            Assert.AreEqual(instance.DisplayName, lines[0].Text);
            Assert.AreEqual("Plate Armor", lines[1].Text);
        }

        [Test]
        public void APoolThatCannotBeRare_DrawsExactlyWhatItDidBefore_AndNamesFromAffixes()
        {
            ArmorData armor = Armor(Names(), rareChance: 0f);
            var a = new SeededRandom(3);
            var b = new SeededRandom(3);

            ItemAffixes first = armor.RollAffixes(a);
            ItemAffixes second = armor.RollAffixes(b);

            Assert.IsFalse(first.IsRare);
            Assert.AreEqual(first, second);
            Assert.AreEqual(a.NextDouble(), b.NextDouble(), "Same stream position: no extra draws.");
            Assert.AreEqual("Riveted Plate Armor of the Bear", Track(armor.CloneInstance(first)).DisplayName);
        }

        [Test]
        public void ARollWithAChance_IsRareSometimes_AndTheSameSeedGivesTheSameItem()
        {
            ArmorData armor = Armor(Names(), rareChance: 0.5f);
            int rare = 0;

            for (int seed = 1; seed <= 200; seed++)
            {
                ItemAffixes a = armor.RollAffixes(new SeededRandom(seed));
                Assert.AreEqual(a, armor.RollAffixes(new SeededRandom(seed)));
                if (a.IsRare) rare++;
            }

            Assert.That(rare, Is.InRange(60, 140), "About half.");
        }

        [Test]
        public void EditingTheNameList_NeverRenamesAnItemAlreadyRolled()
        {
            RareNames names = Names();
            ArmorData armor = Armor(names, rareChance: 1f);
            Item instance = Track(armor.CloneInstance(armor.RollAffixes(new SeededRandom(7))));
            string name = instance.RareName;
            int id = instance.Affixes.RareNameId;

            // New names ahead of everything and the order reversed: the id it holds still means the same name.
            Set(names, "names", new List<NameWord> { Word(99, "Cruel Fortune") }.Concat(names.Names.Reverse()).ToList());

            Assert.AreEqual(id, instance.Affixes.RareNameId);
            Assert.AreEqual(name, Track(armor.CloneInstance(instance.Affixes)).RareName);
        }

        [Test]
        public void ARemovedName_FallsBackToTheAffixNames_NotAnError()
        {
            RareNames names = Names();
            ArmorData armor = Armor(names, rareChance: 1f);
            Item instance = Track(armor.CloneInstance(armor.RollAffixes(new SeededRandom(7))));
            Set(names, "names", new List<NameWord> { Word(50, "Other") });

            Assert.IsNull(instance.RareName);
            StringAssert.Contains("Plate Armor", Track(armor.CloneInstance(instance.Affixes)).DisplayName);
            StringAssert.DoesNotContain("Other", Track(armor.CloneInstance(instance.Affixes)).DisplayName);
        }

        [Test]
        public void ARareNameIsPartOfWhatIsStoredAndCompared()
        {
            ItemAffixes plain = ItemAffixes.From(new[] { new AffixRoll(1, 100) });
            ItemAffixes rare = plain.WithRareName(11);

            Assert.AreNotEqual(plain, rare);
            Assert.AreEqual(rare, plain.WithRareName(11));
            Assert.AreEqual(11, rare.RareNameId);
            Assert.IsFalse(plain.IsRare);
        }

        [Test]
        public void APoolThatMakesRaresWithoutNames_IsReported()
        {
            ArmorData armor = Armor(null, rareChance: 0.2f);

            Assert.IsNotEmpty(armor.AffixPool.Problems());
        }

        [Test]
        public void TheSeedNames_AreTheHundredSuppliedPlusTheOnesAddedAfter_WithPermanentIds()
        {
            RareNames seed = Track(SimpleRPG.EffectTools.RareNameSeed.Build());

            Assert.AreEqual(112, seed.Names.Count);
            Assert.IsNull(seed.Problem());
            Assert.AreEqual("Grave Bite", seed.TextOf(1));
            Assert.AreEqual("Eternal Silence", seed.TextOf(100));
            Assert.AreEqual("Salt Wound", seed.TextOf(112));
        }
    }
}
