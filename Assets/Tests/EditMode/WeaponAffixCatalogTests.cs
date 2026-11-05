using NUnit.Framework;
using SimpleRPG.Abilities;
using SimpleRPG.EffectTools;
using SimpleRPG.Gameplay;
using SimpleRPG.InventorySystem;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace SimpleRPG.Tests.EditMode
{
    /// <summary>
    /// The weapon affixes end to end, in memory: a weapon rolls a damage prefix and the bleed suffix, is named for them, brings the bleed's triggered
    /// effect, and its Enhanced Damage is a percentage of its own base damage.
    /// </summary>
    public class WeaponAffixCatalogTests
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

        private T Track<T>(T made) where T : Object
        {
            created.Add(made);
            return made;
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

        [Test]
        public void TheWeaponAffixes_AreSound_Live_AndOnlyThePrefixesOfTheirOwn()
        {
            var specs = WeaponAffixCatalog.Build();
            var affixes = specs.Select(AffixCatalogBuilder.BuildAffix).Select(a => Track(a)).ToList();

            for (int i = 0; i < affixes.Count; i++)
            {
                Assert.IsNull(affixes[i].Problem(), specs[i].AssetName);
                Assert.IsTrue(affixes[i].IsLive(out string reason), reason);
                Assert.AreEqual(AffixKind.Prefix, specs[i].Kind);
            }

            CollectionAssert.AreEquivalent(new[] { "enhanced_damage", "sharpness" }, specs.Select(s => s.Family).Distinct().ToArray());
        }

        [Test]
        public void ALongswordWithADamagePrefixAndTheBleed_IsNamedForThem_AndBringsTheBleedOnHit()
        {
            // The pieces of the bleed, built the way the tool builds them.
            AbilityDefinition tick = Track(BleedingSetup.BuildTickAbility());
            StatusDefinition status = Track(BleedingSetup.BuildStatus(tick));
            AbilityDefinition apply = Track(BleedingSetup.BuildApplyAbility(status));
            TriggeredEffect bleed = Track(BleedingSetup.BuildEffect(apply));
            AffixDefinition bleeding = Track(BleedingSetup.BuildAffix(bleed));
            bleeding.EnsureId();

            // A fixed Enhanced Damage of +50% so the result is exact, and the tier gate lowered out of the way.
            AffixDefinition jagged = Track(AffixCatalogBuilder.BuildAffix(WeaponAffixCatalog.Build().First(s => s.Family == "enhanced_damage")));
            jagged.EnsureId();
            Set(jagged, "effects", new List<AffixEffect>
            {
                new StatModifierEffect("Weapon Damage", DerivedStats.WeaponDamage.Key, ModifierLayer.PercentAdd, 0.5f, 0.5f, AffixScope.ThisItem),
            });

            DefinitionRegistry registry = Track(ScriptableObject.CreateInstance<DefinitionRegistry>());
            Assert.IsTrue(registry.Rebuild(new GameplayDefinition[] { jagged, bleeding }).Success);
            DefinitionRegistry.Instance = registry;

            WeaponData sword = Track(ScriptableObject.CreateInstance<WeaponData>());
            sword.EnsureId();
            sword.damage = new DamageInfo { Damage = 12 };
            Set(sword, "<ItemName>k__BackingField", "Longsword");
            AffixPool pool = AffixCatalogBuilder.BuildPool("Longsword", new[] { jagged, bleeding }, 2, 2);
            Track(pool);
            Set(sword, "affixPool", pool);

            Item instance = Track(sword.CloneInstance(sword.RollAffixes(new SeededRandom(1))));

            Assert.AreEqual("Jagged Longsword of Bleeding", instance.DisplayName);
            CollectionAssert.AreEqual(new[] { bleed }, instance.AllTriggeredEffects);
            Assert.AreEqual(6f, instance.AllGrants.Single(g => g.AttributeKey == DerivedStats.WeaponDamage.Key).Value, 1e-4f, "50% of the sword's own 12.");

            var lines = ItemTooltip.Build(instance).Select(l => l.Text).ToList();
            CollectionAssert.Contains(lines, "+50% Weapon Damage (this item)");
            CollectionAssert.Contains(lines, bleed.Description);
        }
    }
}
