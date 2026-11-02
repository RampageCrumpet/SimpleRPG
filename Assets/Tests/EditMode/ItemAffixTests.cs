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

        private AffixDefinition Affix(string key, ModifierLayer layer, float min, float max, string label = "Strength", string group = "", params EquipmentSlot[] slots)
        {
            AffixDefinition affix = ScriptableObject.CreateInstance<AffixDefinition>();
            created.Add(affix);
            affix.EnsureId();
            Set(affix, "label", label);
            Set(affix, "attributeKey", key);
            Set(affix, "layer", layer);
            Set(affix, "minimum", min);
            Set(affix, "maximum", max);
            Set(affix, "exclusiveGroup", group);
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
        public void AnAffixDescribesItselfByHowItApplies()
        {
            Assert.AreEqual("+3 Strength", Affix("k", ModifierLayer.Add, 1, 3).Describe(3f));
            Assert.AreEqual("-2 Strength", Affix("k", ModifierLayer.Add, 1, 3).Describe(-2f));
            Assert.AreEqual("+10% Strength", Affix("k", ModifierLayer.PercentAdd, 0.05f, 0.2f).Describe(0.1f));
            Assert.AreEqual("x1.5 Strength", Affix("k", ModifierLayer.Multiply, 1f, 2f).Describe(1.5f));
        }

        [Test]
        public void AnAffixBecomesTheGrantTheEngineApplies()
        {
            GrantSpec grant = Affix("simplerpg.attr.strength", ModifierLayer.PercentAdd, 0.05f, 0.2f).ToGrant(0.15f);

            Assert.AreEqual("simplerpg.attr.strength", grant.AttributeKey);
            Assert.AreEqual(ModifierLayer.PercentAdd, grant.Layer);
            Assert.AreEqual(0.15f, grant.Value, 1e-6f);
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
            Assert.AreEqual(2000, instance.Affixes[0].ValueMilli);
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
