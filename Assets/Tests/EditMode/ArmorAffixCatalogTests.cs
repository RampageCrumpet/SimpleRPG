using NUnit.Framework;
using SimpleRPG.Abilities;
using SimpleRPG.EffectTools;
using SimpleRPG.Gameplay;
using SimpleRPG.InventorySystem;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace SimpleRPG.Tests.EditMode
{
    /// <summary>
    /// The armor affix catalog, built in memory the way the tool builds it: every affix is sound and does something the game reads, each family
    /// climbs in three tiers with the item level, and a pool of all of it is fit to ship. The numbers are provisional and not tested.
    /// </summary>
    public class ArmorAffixCatalogTests
    {
        private readonly List<Object> created = new List<Object>();

        [TearDown]
        public void TearDown()
        {
            foreach (Object o in created)
            {
                if (o != null) Object.DestroyImmediate(o);
            }

            created.Clear();
        }

        private List<AffixDefinition> BuildAll(List<ArmorAffixCatalog.Spec> specs)
        {
            var built = specs.Select(ArmorAffixCatalog.BuildAffix).ToList();
            created.AddRange(built);
            return built;
        }

        [Test]
        public void EveryAffix_IsSound_AndDoesSomethingTheGameReads()
        {
            var specs = ArmorAffixCatalog.Build();
            var affixes = BuildAll(specs);

            for (int i = 0; i < affixes.Count; i++)
            {
                Assert.IsNull(affixes[i].Problem(), specs[i].AssetName);
                Assert.IsTrue(affixes[i].IsLive(out string reason), reason);
                Assert.IsNotEmpty(specs[i].DisplayName);
            }
        }

        [Test]
        public void NamesAreUnique_AndAFamilyHasThreeTiersClimbingWithItemLevel()
        {
            var specs = ArmorAffixCatalog.Build();

            CollectionAssert.AllItemsAreUnique(specs.Select(s => s.AssetName));
            CollectionAssert.AllItemsAreUnique(specs.Select(s => s.DisplayName));
            foreach (var family in specs.GroupBy(s => s.Family))
            {
                var tiers = family.OrderBy(s => s.Tier).ToList();
                CollectionAssert.AreEqual(new[] { 1, 2, 3 }, tiers.Select(t => t.Tier).ToArray(), family.Key);
                CollectionAssert.AreEqual(tiers.Select(t => t.MinimumItemLevel).OrderBy(x => x).ToArray(), tiers.Select(t => t.MinimumItemLevel).ToArray(), family.Key + " climbs");
                Assert.AreEqual(1, tiers.Select(t => t.Kind).Distinct().Count(), family.Key + " is all prefixes or all suffixes");
                Assert.AreEqual(ItemLevel.Minimum, tiers[0].MinimumItemLevel, "The first tier is open from the start.");
            }
        }

        [Test]
        public void ARollAtTheLowestLevel_OnlyOffersFirstTiers_AndAHighOneOffersTheRest()
        {
            var specs = ArmorAffixCatalog.Build();
            var affixes = BuildAll(specs);
            AffixPool pool = ArmorAffixCatalog.BuildPool("Test", affixes);
            created.Add(pool);
            var ids = new Dictionary<AffixDefinition, uint>();
            uint next = 1;
            foreach (AffixDefinition affix in affixes) ids[affix] = next++;
            var byId = ids.ToDictionary(p => p.Value, p => p.Key);

            var lowTiers = new HashSet<int>();
            var highTiers = new HashSet<int>();
            for (int seed = 1; seed <= 300; seed++)
            {
                ItemAffixes low = AffixRoller.Roll(pool, EquipmentSlot.chest, ItemLevel.Minimum, new SeededRandom(seed), a => ids[a]);
                for (int i = 0; i < low.Count; i++) lowTiers.Add(byId[low[i].AffixId].Tier);

                ItemAffixes high = AffixRoller.Roll(pool, EquipmentSlot.chest, 20, new SeededRandom(seed), a => ids[a]);
                for (int i = 0; i < high.Count; i++) highTiers.Add(byId[high[i].AffixId].Tier);
            }

            CollectionAssert.AreEquivalent(new[] { 1 }, lowTiers);
            CollectionAssert.AreEquivalent(new[] { 1, 2, 3 }, highTiers);
        }

        [Test]
        public void ThePoolOfAll_IsFitToShip_AndGivesAtMostOnePrefixAndOneSuffix()
        {
            var affixes = BuildAll(ArmorAffixCatalog.Build());
            AffixPool pool = ArmorAffixCatalog.BuildPool("Test", affixes);
            created.Add(pool);

            Assert.IsEmpty(pool.Problems());
            Assert.AreEqual(1, pool.MaximumPrefixes);
            Assert.AreEqual(1, pool.MaximumSuffixes);
            Assert.AreEqual(0, pool.MinimumCount, "A plain piece is possible.");
        }

        [Test]
        public void TheHandsAffix_OnlyRollsOnHands()
        {
            var specs = ArmorAffixCatalog.Build();
            var affixes = BuildAll(specs);

            AffixDefinition nimble = affixes[specs.FindIndex(s => s.Family == "hands")];

            Assert.IsTrue(nimble.AllowsSlot(EquipmentSlot.hands));
            Assert.IsFalse(nimble.AllowsSlot(EquipmentSlot.chest));
        }

        [Test]
        public void AnArmorPercentage_IsOfTheItemItself_NotTheWearersWholeArmor()
        {
            var specs = ArmorAffixCatalog.Build();
            var enhanced = specs.Where(s => s.Family == "armor_enhanced").SelectMany(s => s.Effects).OfType<StatModifierEffect>().ToList();

            Assert.AreEqual(3, enhanced.Count);
            Assert.IsTrue(enhanced.All(e => e.Scope == AffixScope.ThisItem && e.Layer == ModifierLayer.PercentAdd));
        }
    }
}
