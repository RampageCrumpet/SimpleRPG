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
    /// Affixes on real item assets: how an affix describes itself and turns into a grant, how a pool rolls for a slot, and how an item
    /// instance carries what it rolled into its grants and through a clone.
    /// </summary>
    public class ItemAffixTests
    {
        private readonly List<Object> created = new List<Object>();
        private DefinitionRegistry previousRegistry;
        private IGameplayRandom previousRandom;

        [SetUp]
        public void SetUp()
        {
            previousRegistry = DefinitionRegistry.Instance;
            previousRandom = GameplayServices.Current.Random;
        }

        [TearDown]
        public void TearDown()
        {
            DefinitionRegistry.Instance = previousRegistry;
            GameplayServices.Current.Random = previousRandom;
            foreach (Object item in created)
            {
                Object.DestroyImmediate(item);
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

        private AffixDefinition Affix(string key, ModifierLayer layer, float min, float max, string label = "Strength", string family = "", params EquipmentSlot[] slots)
        {
            return Affix(new List<AffixEffect> { new StatModifierEffect(label, key, layer, min, max) }, family, slots);
        }

        private AffixDefinition Affix(List<AffixEffect> effects, string family = "", params EquipmentSlot[] slots)
        {
            AffixDefinition affix = ScriptableObject.CreateInstance<AffixDefinition>();
            created.Add(affix);
            affix.EnsureId();
            Set(affix, "effects", effects);
            Set(affix, "family", family);
            Set(affix, "allowedSlots", slots.ToList());
            return affix;
        }

        private AffixPool Pool(int min, int max, params AffixDefinition[] affixes)
        {
            AffixPool pool = ScriptableObject.CreateInstance<AffixPool>();
            created.Add(pool);
            Set(pool, "affixes", affixes.ToList());
            Set(pool, "minimumCount", min);
            Set(pool, "maximumCount", max);
            Set(pool, "maximumPrefixes", 99);
            Set(pool, "maximumSuffixes", 99);
            return pool;
        }

        private T MakeItem<T>(AffixPool pool = null, Vector2Int? size = null) where T : Item
        {
            T item = ScriptableObject.CreateInstance<T>();
            created.Add(item);
            item.EnsureId();
            Set(item, "<ItemSize>k__BackingField", size ?? Vector2Int.one);
            Set(item, "affixPool", pool);
            return item;
        }

        private void Register(params GameplayDefinition[] definitions)
        {
            DefinitionRegistry registry = ScriptableObject.CreateInstance<DefinitionRegistry>();
            created.Add(registry);
            RegistryReport report = registry.Rebuild(definitions);
            Assert.IsTrue(report.Success, string.Join("; ", report.Errors));
            DefinitionRegistry.Instance = registry;
        }

        // ---- The affix itself ----

        [Test]
        public void AnAffixDescribesEachEffectByHowItApplies()
        {
            Assert.AreEqual("+3 Strength", Affix("k", ModifierLayer.Add, 1, 3).Describe(new[] { 3f })[0]);
            Assert.AreEqual("-2 Strength", Affix("k", ModifierLayer.Add, 1, 3).Describe(new[] { -2f })[0]);
            Assert.AreEqual("+10% Strength", Affix("k", ModifierLayer.PercentAdd, 0.05f, 0.2f).Describe(new[] { 0.1f })[0]);
            Assert.AreEqual("x1.5 Strength", Affix("k", ModifierLayer.Multiply, 1f, 2f).Describe(new[] { 1.5f })[0]);
        }

        [Test]
        public void AnAffixCanHaveSeveralEffects_EachWithItsOwnValueAndRange()
        {
            AffixDefinition affix = Affix(new List<AffixEffect>
            {
                new StatModifierEffect("Armor Rating", "simplerpg.stat.armor_rating", ModifierLayer.Add, 3f, 4f),
                new StatModifierEffect("Constitution", "simplerpg.attr.constitution", ModifierLayer.PercentAdd, 0.03f, 0.04f),
                new StatModifierEffect("Movement Speed", "simplerpg.stat.movement_speed", ModifierLayer.PercentAdd, -0.01f, -0.01f),
                new StatModifierEffect("Willpower", "simplerpg.attr.willpower", ModifierLayer.Add, 1f, 2f),
            });

            Assert.AreEqual(4, affix.ValueCount);
            Assert.AreEqual(0.03f, affix.Ranges()[1].Minimum, 1e-6f);
            var grants = new List<GrantSpec>();
            affix.AddGrants(new List<GrantSpec>(), new[] { 3.5f, 0.035f, -0.01f, 1.5f }, grants);

            Assert.AreEqual(4, grants.Count);
            Assert.AreEqual("simplerpg.attr.constitution", grants[1].AttributeKey);
            Assert.AreEqual(0.035f, grants[1].Value, 1e-6f);
            Assert.AreEqual(4, affix.Describe(new[] { 3.5f, 0.035f, -0.01f, 1.5f }).Count);
        }

        [Test]
        public void APercentageOfThisItem_IsAFlatAmountOfTheItemsOwnGrants_NotOfOtherArmor()
        {
            AffixDefinition affix = Affix(new List<AffixEffect>
            {
                new StatModifierEffect("Armor Rating", "simplerpg.stat.armor_rating", ModifierLayer.PercentAdd, 0.1f, 0.1f, AffixScope.ThisItem),
            });
            var own = new List<GrantSpec>
            {
                new GrantSpec { AttributeKey = "simplerpg.stat.armor_rating", Layer = ModifierLayer.Add, Value = 40f },
                new GrantSpec { AttributeKey = "simplerpg.attr.strength", Layer = ModifierLayer.Add, Value = 5f },
            };
            var grants = new List<GrantSpec>();

            affix.AddGrants(own, new[] { 0.1f }, grants);

            Assert.AreEqual(1, grants.Count);
            Assert.AreEqual(ModifierLayer.Add, grants[0].Layer, "It becomes a flat amount, so it cannot scale armor worn elsewhere.");
            Assert.AreEqual(4f, grants[0].Value, 1e-4f, "10% of this item's 40, not of the wearer's whole Armor Rating.");
            Assert.AreEqual("+10% Armor Rating (this item)", affix.Describe(new[] { 0.1f })[0]);
        }

        [Test]
        public void APercentageOfThisItem_OnAnItemWithNoneOfThatStat_AddsNothing()
        {
            AffixDefinition affix = Affix(new List<AffixEffect>
            {
                new StatModifierEffect("Armor Rating", "simplerpg.stat.armor_rating", ModifierLayer.PercentAdd, 0.1f, 0.1f, AffixScope.ThisItem),
            });
            var grants = new List<GrantSpec>();

            affix.AddGrants(new List<GrantSpec>(), new[] { 0.1f }, grants);

            Assert.IsEmpty(grants);
        }

        [Test]
        public void AnAffixIsOnlyAuthoredCorrectly_IfItHasEffectsThatAreSound()
        {
            Assert.IsNotNull(Affix(new List<AffixEffect>()).Problem(), "No effects.");
            Assert.IsNotNull(Affix("", ModifierLayer.Add, 1, 2).Problem(), "No attribute key.");
            Assert.IsNotNull(Affix("k", ModifierLayer.Add, 3, 1).Problem(), "Maximum below minimum.");
            Assert.IsNotNull(new StatModifierEffect("x", "k", ModifierLayer.Add, 1, 2, AffixScope.ThisItem).Problem(), "Only a percentage can be of this item.");
            Assert.IsNull(Affix("k", ModifierLayer.Add, 1, 2).Problem());
        }

        [Test]
        public void AnAffixOnAnAttributeNothingReads_IsNotLive_AndKeepsItsPoolFromShipping()
        {
            AffixDefinition inactive = Affix("simplerpg.attr.dexterity", ModifierLayer.Add, 1, 2);
            AffixDefinition live = Affix("simplerpg.stat.armor_rating", ModifierLayer.Add, 1, 2);

            Assert.IsFalse(inactive.IsLive(out string reason));
            StringAssert.Contains("dexterity", reason);
            Assert.IsTrue(live.IsLive(out _));
            Assert.IsEmpty(Pool(1, 1, live).Problems());
            Assert.IsNotEmpty(Pool(1, 1, live, inactive).Problems());
        }

        [Test]
        public void APoolThatCouldOutgrowAnItemsRoom_IsReported()
        {
            var effects = new List<AffixEffect>();
            for (int i = 0; i < 7; i++) effects.Add(new StatModifierEffect("x", "simplerpg.attr.strength", ModifierLayer.Add, 1, 2));
            AffixDefinition big = Affix(effects);
            AffixDefinition big2 = Affix(effects);
            AffixDefinition big3 = Affix(effects);
            AffixDefinition big4 = Affix(effects);

            Assert.IsNotEmpty(Pool(4, 4, big, big2, big3, big4).Problems(), "Four of 9 words each is 36, and an item has room for 32.");
            Assert.IsEmpty(Pool(1, 3, big, big2, big3, big4).Problems());
        }

        [Test]
        public void AnAffixNeedsItsItemLevel()
        {
            AffixDefinition affix = Affix("k", ModifierLayer.Add, 1, 2);
            Set(affix, "minimumItemLevel", 16);

            Assert.IsFalse(affix.AllowsItemLevel(15));
            Assert.IsTrue(affix.AllowsItemLevel(16));
            Assert.IsTrue(affix.AllowsItemLevel(40));
        }

        [Test]
        public void AnAffixOnlyRollsOnTheSlotsItAllows_AndAnEmptyListMeansAny()
        {
            AffixDefinition chestOnly = Affix("k", ModifierLayer.Add, 1, 2, slots: EquipmentSlot.chest);
            AffixDefinition anywhere = Affix("k", ModifierLayer.Add, 1, 2);

            Assert.IsTrue(chestOnly.AllowsSlot(EquipmentSlot.chest));
            Assert.IsFalse(chestOnly.AllowsSlot(EquipmentSlot.head));
            Assert.IsTrue(anywhere.AllowsSlot(EquipmentSlot.head));
        }

        // ---- Rolling from a pool ----

        [Test]
        public void ARollOffersOnlyTheTiersTheItemLevelAllows_AndOneTierPerFamily()
        {
            AffixDefinition low = Affix("simplerpg.attr.strength", ModifierLayer.Add, 1, 2, family: "might");
            AffixDefinition high = Affix("simplerpg.attr.strength", ModifierLayer.Add, 5, 6, family: "might");
            Set(high, "minimumItemLevel", 10);
            Set(high, "tier", 2);
            Register(low, high);
            ArmorData chest = MakeItem<ArmorData>(Pool(2, 2, low, high));
            Set(chest, "slot", EquipmentSlot.chest);

            for (int seed = 1; seed < 30; seed++)
            {
                Assert.AreEqual(1, chest.RollAffixes(new SeededRandom(seed), 9).Count);
                Assert.AreEqual(1, chest.RollAffixes(new SeededRandom(seed), 10).Count, "Same family, so only one tier of it even when both are allowed.");
            }

            DefinitionRegistry.Instance.TryGetShortId(low.Id, out uint lowId);
            Assert.AreEqual(lowId, chest.RollAffixes(new SeededRandom(1), 9)[0].AffixId, "Below level 10 only the first tier can roll.");
        }

        [Test]
        public void ARollFromAPool_UsesOnlyAffixesThatFitTheSlot_AndAreRegistered()
        {
            AffixDefinition forHead = Affix("a", ModifierLayer.Add, 1, 2, slots: EquipmentSlot.head);
            AffixDefinition forChest = Affix("b", ModifierLayer.Add, 1, 2, slots: EquipmentSlot.chest);
            AffixDefinition unregistered = Affix("c", ModifierLayer.Add, 1, 2);
            Register(forHead, forChest);
            AffixPool pool = Pool(2, 2, forHead, forChest, unregistered);
            ArmorData helmet = MakeItem<ArmorData>(pool);
            Set(helmet, "slot", EquipmentSlot.head);
            registryShortId(forHead, out uint headId);

            ItemAffixes rolled = helmet.RollAffixes(new SeededRandom(1));

            Assert.AreEqual(1, rolled.Count, "Only the head affix fits a helmet and is in the registry, so only one can roll.");
            Assert.AreEqual(headId, rolled[0].AffixId);
        }

        private static void registryShortId(GameplayDefinition definition, out uint id)
        {
            Assert.IsTrue(DefinitionRegistry.Instance.TryGetShortId(definition.Id, out id));
        }

        [Test]
        public void AnItemWithNoPool_RollsNothing_AndDrawsNothing()
        {
            Item plain = MakeItem<Item>();
            var random = new SeededRandom(7);
            double expectedNext = new SeededRandom(7).NextDouble();

            Assert.IsTrue(plain.RollAffixes(random).IsEmpty);
            Assert.AreEqual(expectedNext, random.NextDouble(), "The stream is exactly where it was.");
        }

        // ---- What an instance carries ----

        [Test]
        public void AnInstanceCarriesWhatItRolled_ThroughACloneOfTheClone()
        {
            AffixDefinition strength = Affix("simplerpg.attr.strength", ModifierLayer.Add, 2, 2);
            Register(strength);
            Item sword = MakeItem<Item>(Pool(1, 1, strength));

            ItemAffixes rolled = sword.RollAffixes(new SeededRandom(1));
            Item instance = sword.CloneInstance(rolled);
            created.Add(instance);
            Item copy = instance.CloneInstance(instance.Affixes);
            created.Add(copy);

            Assert.AreEqual(1, instance.Affixes.Count);
            Assert.AreEqual(2000, instance.Affixes[0].ValuesMilli[0]);
            Assert.AreEqual(rolled, copy.Affixes, "A copy of an instance keeps its roll.");
            Assert.IsTrue(sword.Affixes.IsEmpty, "The asset itself is never changed.");
        }

        [Test]
        public void TheGrantsOfAnInstanceAreItsAssetsPlusWhatItRolled()
        {
            AffixDefinition strength = Affix("simplerpg.attr.strength", ModifierLayer.Add, 3, 3);
            Register(strength);
            Item sword = MakeItem<Item>(Pool(1, 1, strength));
            Set(sword, "grants", new List<GrantSpec> { new GrantSpec { AttributeKey = "simplerpg.attr.armor", Layer = ModifierLayer.Add, Value = 5f } });
            Item instance = sword.CloneInstance(sword.RollAffixes(new SeededRandom(2)));
            created.Add(instance);

            IReadOnlyList<GrantSpec> grants = instance.AllGrants;

            Assert.AreEqual(2, grants.Count);
            Assert.AreEqual("simplerpg.attr.armor", grants[0].AttributeKey);
            Assert.AreEqual("simplerpg.attr.strength", grants[1].AttributeKey);
            Assert.AreEqual(3f, grants[1].Value, 1e-6f);
            Assert.AreEqual(1, sword.AllGrants.Count, "The asset on its own only has what it declares.");
        }

        [Test]
        public void AnAffixNoLongerInTheRegistry_IsSkipped_NotFatal()
        {
            AffixDefinition strength = Affix("simplerpg.attr.strength", ModifierLayer.Add, 3, 3);
            Register(strength);
            Item sword = MakeItem<Item>(Pool(1, 1, strength));
            Item instance = sword.CloneInstance(sword.RollAffixes(new SeededRandom(2)));
            created.Add(instance);

            // The affix is removed from the game after the item rolled it.
            Register();
            UnityEngine.TestTools.LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex("not in the DefinitionRegistry"));

            Assert.AreEqual(0, instance.AllGrants.Count);
        }

        [Test]
        public void ANewInstanceInAnInventoryRollsOnceFromTheGameplayStream()
        {
            AffixDefinition strength = Affix("simplerpg.attr.strength", ModifierLayer.Add, 1, 9);
            Register(strength);
            Item sword = MakeItem<Item>(Pool(1, 1, strength), new Vector2Int(1, 2));
            GameplayServices.Current.Random = new SeededRandom(42);
            var host = new GameObject("InventoryHost");
            created.Add(host);
            Inventory inventory = host.AddComponent<Inventory>();
            Set(inventory, "<InventorySize>k__BackingField", new Vector2Int(4, 4));
            inventory.Awake();

            Assert.IsTrue(inventory.AddItemInstance(sword));
            Assert.IsTrue(inventory.AddItemInstance(sword));

            List<Item> held = inventory.GetItems().Select(pair => pair.item).ToList();
            Assert.AreEqual(2, held.Count);
            Assert.AreEqual(1, held[0].Affixes.Count);
            Assert.AreEqual(1, held[1].Affixes.Count);
            Assert.AreNotEqual(held[0].Affixes, held[1].Affixes, "Each instance rolls for itself.");
            Assert.IsTrue(sword.Affixes.IsEmpty, "The asset stays plain.");
        }
    }
}
