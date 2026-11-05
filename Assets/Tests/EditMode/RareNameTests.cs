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

        private RareNameComponents Words()
        {
            RareNameComponents words = Track(ScriptableObject.CreateInstance<RareNameComponents>());
            Set(words, "firstWords", new List<NameWord> { Word(1, "Grim"), Word(2, "Dread"), Word(3, "Gloom") });
            Set(words, "secondWords", new List<NameWord> { Word(10, "Bulwark"), Word(11, "Shell") });
            return words;
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

        // An armor asset whose pool rolls a prefix and a suffix and makes anything with both rare.
        private ArmorData Armor(RareNameComponents words, int rareFrom, out AffixDefinition[] affixes)
        {
            AffixDefinition prefix = Affix("Riveted", AffixKind.Prefix, "simplerpg.stat.armor_rating");
            AffixDefinition suffix = Affix("of the Bear", AffixKind.Suffix, "simplerpg.attr.constitution");
            affixes = new[] { prefix, suffix };
            DefinitionRegistry registry = Track(ScriptableObject.CreateInstance<DefinitionRegistry>());
            Assert.IsTrue(registry.Rebuild(new GameplayDefinition[] { prefix, suffix }).Success);
            DefinitionRegistry.Instance = registry;

            AffixPool pool = Track(ScriptableObject.CreateInstance<AffixPool>());
            Set(pool, "affixes", new List<AffixDefinition> { prefix, suffix });
            Set(pool, "minimumCount", 2);
            Set(pool, "maximumCount", 2);
            Set(pool, "maximumPrefixes", 1);
            Set(pool, "maximumSuffixes", 1);
            Set(pool, "rareNames", words);
            Set(pool, "rareFromCount", rareFrom);

            ArmorData armor = Track(ScriptableObject.CreateInstance<ArmorData>());
            armor.EnsureId();
            Set(armor, "<ItemName>k__BackingField", "Plate Armor");
            Set(armor, "affixPool", pool);
            return armor;
        }

        [Test]
        public void AWordIsPickedBySampleAcrossTheList()
        {
            RareNameComponents words = Words();

            Assert.AreEqual(1, RareNameComponents.Pick(words.FirstWords, 0.0));
            Assert.AreEqual(2, RareNameComponents.Pick(words.FirstWords, 0.5));
            Assert.AreEqual(3, RareNameComponents.Pick(words.FirstWords, 0.999));
            Assert.AreEqual(0, RareNameComponents.Pick(new List<NameWord>(), 0.5), "No words, no pick.");
        }

        [Test]
        public void AnItemWithEnoughAffixes_IsRare_AndNamedFromTwoWords_NotFromItsAffixes()
        {
            ArmorData armor = Armor(Words(), rareFrom: 2, out _);

            Item instance = Track(armor.CloneInstance(armor.RollAffixes(new SeededRandom(7))));

            Assert.IsTrue(instance.Affixes.IsRare);
            StringAssert.DoesNotContain("Riveted", instance.DisplayName);
            Assert.AreEqual(2, instance.DisplayName.Split(' ').Length, "Two words.");
            Assert.AreEqual(instance.DisplayName, instance.RareName);
            var lines = ItemTooltip.Build(instance);
            Assert.AreEqual(instance.DisplayName, lines[0].Text);
            Assert.AreEqual("Plate Armor", lines[1].Text, "The base item is named beneath the rare name.");
        }

        [Test]
        public void AnItemBelowTheThreshold_IsNotRare_AndDrawsNothingExtra()
        {
            ArmorData rare = Armor(Words(), rareFrom: 0, out _);
            var plain = new SeededRandom(3);
            var again = new SeededRandom(3);

            ItemAffixes a = rare.RollAffixes(plain);
            ItemAffixes b = rare.RollAffixes(again);

            Assert.IsFalse(a.IsRare);
            Assert.AreEqual(a, b);
            Assert.AreEqual("Riveted Plate Armor of the Bear", Track(rare.CloneInstance(a)).DisplayName, "Named for its affixes as before.");
        }

        [Test]
        public void TheSameSeedGivesTheSameName_AndSeedsVary()
        {
            ArmorData armor = Armor(Words(), rareFrom: 2, out _);
            var names = new HashSet<string>();

            for (int seed = 1; seed <= 40; seed++)
            {
                string first = Track(armor.CloneInstance(armor.RollAffixes(new SeededRandom(seed)))).DisplayName;
                string again = Track(armor.CloneInstance(armor.RollAffixes(new SeededRandom(seed)))).DisplayName;
                Assert.AreEqual(first, again);
                names.Add(first);
            }

            Assert.Greater(names.Count, 1, "Different seeds do not all give the same name.");
        }

        [Test]
        public void EditingTheWordList_NeverRenamesAnItemAlreadyRolled()
        {
            RareNameComponents words = Words();
            ArmorData armor = Armor(words, rareFrom: 2, out _);
            Item instance = Track(armor.CloneInstance(armor.RollAffixes(new SeededRandom(7))));
            string name = instance.RareName;
            (int first, int second) = instance.Affixes.RareName;

            // Add words ahead of everything and change the order: the ids it holds still mean the same words.
            Set(words, "firstWords", new List<NameWord> { Word(99, "Cruel") }.Concat(words.FirstWords.Reverse()).ToList());

            Assert.AreEqual((first, second), instance.Affixes.RareName);
            Assert.AreEqual(name, Track(armor.CloneInstance(instance.Affixes)).RareName);
        }

        [Test]
        public void ARemovedWord_FallsBackToTheAffixNames_NotAnError()
        {
            RareNameComponents words = Words();
            ArmorData armor = Armor(words, rareFrom: 2, out _);
            Item instance = Track(armor.CloneInstance(armor.RollAffixes(new SeededRandom(7))));
            Set(words, "firstWords", new List<NameWord> { Word(50, "Other") });

            Assert.IsNull(instance.RareName);
            Assert.AreEqual("Riveted Plate Armor of the Bear", Track(armor.CloneInstance(instance.Affixes)).DisplayName);
        }

        [Test]
        public void ARareNameIsPartOfWhatIsStoredAndCompared()
        {
            ItemAffixes plain = ItemAffixes.From(new[] { new AffixRoll(1, 100) });
            ItemAffixes rare = plain.WithRareName(2, 11);

            Assert.AreNotEqual(plain, rare);
            Assert.AreEqual(rare, plain.WithRareName(2, 11));
            Assert.AreEqual((2, 11), rare.RareName);
            Assert.IsFalse(plain.IsRare);
        }

        [Test]
        public void APoolThatMakesRaresWithoutWords_IsReported()
        {
            ArmorData armor = Armor(null, rareFrom: 2, out _);

            Assert.IsNotEmpty(armor.AffixPool.Problems());
        }
    }
}
