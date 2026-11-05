using NUnit.Framework;
using SimpleRPG.Abilities;
using SimpleRPG.Gameplay;
using SimpleRPG.InventorySystem;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace SimpleRPG.Tests.EditMode
{
    /// <summary>
    /// What a weapon blow does before the combo scalar and defences: the weapon's damage plus flat bonuses, times the multiplier. Item-local Enhanced
    /// Damage is a percentage of that one weapon's base; global bonuses come from every worn item.
    /// </summary>
    public class WeaponDamageTests
    {
        private const string PlayerStats = "Assets/Stats/PlayerStats.asset";

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

        private WeaponData Sword(int damage)
        {
            WeaponData sword = ScriptableObject.CreateInstance<WeaponData>();
            created.Add(sword);
            sword.EnsureId();
            sword.damage = new DamageInfo { Damage = damage };
            return sword;
        }

        private AffixDefinition EnhancedDamage(float percent)
        {
            AffixDefinition affix = ScriptableObject.CreateInstance<AffixDefinition>();
            created.Add(affix);
            affix.EnsureId();
            Set(affix, "effects", new List<AffixEffect>
            {
                new StatModifierEffect("Weapon Damage", DerivedStats.WeaponDamage.Key, ModifierLayer.PercentAdd, percent, percent, AffixScope.ThisItem),
            });
            return affix;
        }

        private Item Rolled(WeaponData sword, AffixDefinition affix)
        {
            DefinitionRegistry registry = ScriptableObject.CreateInstance<DefinitionRegistry>();
            created.Add(registry);
            Assert.IsTrue(registry.Rebuild(new GameplayDefinition[] { affix }).Success);
            DefinitionRegistry.Instance = registry;
            AffixPool pool = ScriptableObject.CreateInstance<AffixPool>();
            created.Add(pool);
            Set(pool, "affixes", new List<AffixDefinition> { affix });
            Set(pool, "minimumCount", 1);
            Set(pool, "maximumCount", 1);
            Set(sword, "affixPool", pool);
            Item instance = sword.CloneInstance(sword.RollAffixes(new SeededRandom(1)));
            created.Add(instance);
            return instance;
        }

        [Test]
        public void ABlow_IsTheWeaponsDamagePlusFlatBonuses_TimesTheMultiplier()
        {
            Assert.AreEqual(10f, WeaponDamageCalculator.Compute(10f, 0d, 1d), 1e-4f, "Nothing changes a bare weapon.");
            Assert.AreEqual(18f, WeaponDamageCalculator.Compute(10f, 5d, 1.2d), 1e-4f, "(10 + 5) x 1.2");
            Assert.AreEqual(0f, WeaponDamageCalculator.Compute(10f, -50d, 1d), "Never negative.");
            Assert.AreEqual(0f, WeaponDamageCalculator.Compute(10f, 0d, -1d));
        }

        [Test]
        public void AStartingCharacter_DealsTheWeaponsDamageUnchanged()
        {
            StatProfile profile = AssetDatabase.LoadAssetAtPath<StatProfile>(PlayerStats);
            AttributeSet set = profile.CreateSet();
            foreach (AttributeId primary in PrimaryAttributes.All) set.SetBase(primary, 10);

            Assert.AreEqual(0d, set.Get(DerivedStats.WeaponDamage), 1e-9);
            Assert.AreEqual(1d, set.Get(DerivedStats.WeaponDamageMultiplier), 1e-9);
            Assert.AreEqual(10f, WeaponDamageCalculator.Compute(10f, set.Get(DerivedStats.WeaponDamage), set.Get(DerivedStats.WeaponDamageMultiplier)), 1e-4f);
        }

        [Test]
        public void PercentageBonuses_AddTogether_BeforeTheyScale()
        {
            StatProfile profile = AssetDatabase.LoadAssetAtPath<StatProfile>(PlayerStats);
            AttributeSet set = profile.CreateSet();
            SourceHandle ring = SourceHandle.FromItem(new ItemInstanceId(1));
            SourceHandle belt = SourceHandle.FromItem(new ItemInstanceId(2));
            set.AddModifier(new AttributeModifier(DerivedStats.WeaponDamageMultiplier, ModifierLayer.PercentAdd, 0.10, ring));
            set.AddModifier(new AttributeModifier(DerivedStats.WeaponDamageMultiplier, ModifierLayer.PercentAdd, 0.20, belt));
            set.AddModifier(new AttributeModifier(DerivedStats.WeaponDamage, ModifierLayer.Add, 3d, ring));

            float damage = WeaponDamageCalculator.Compute(10f, set.Get(DerivedStats.WeaponDamage), set.Get(DerivedStats.WeaponDamageMultiplier));

            Assert.AreEqual((10 + 3) * 1.3f, damage, 1e-3f, "+10% and +20% make +30%, not 1.1 x 1.2.");
        }

        [Test]
        public void EnhancedDamage_IsAPercentageOfThatWeaponsBase_AndTheWeaponBaseIsNotAGrant()
        {
            WeaponData sword = Sword(10);
            Item instance = Rolled(sword, EnhancedDamage(0.5f));

            IReadOnlyList<GrantSpec> grants = instance.AllGrants;

            Assert.AreEqual(1, grants.Count, "Only the affix's flat amount is applied to the wearer; the weapon's base is added by the calculation.");
            Assert.AreEqual(DerivedStats.WeaponDamage.Key, grants[0].AttributeKey);
            Assert.AreEqual(ModifierLayer.Add, grants[0].Layer);
            Assert.AreEqual(5f, grants[0].Value, 1e-4f, "50% of this sword's 10.");
        }

        [Test]
        public void EnhancedDamage_DoesNotScaleFlatBonusesFromOtherItems()
        {
            WeaponData sword = Sword(10);
            Item instance = Rolled(sword, EnhancedDamage(0.5f));
            StatProfile profile = AssetDatabase.LoadAssetAtPath<StatProfile>(PlayerStats);
            AttributeSet set = profile.CreateSet();
            set.AddModifier(new AttributeModifier(DerivedStats.WeaponDamage, ModifierLayer.Add, instance.AllGrants[0].Value, SourceHandle.FromItem(new ItemInstanceId(1))));
            set.AddModifier(new AttributeModifier(DerivedStats.WeaponDamage, ModifierLayer.Add, 4d, SourceHandle.FromItem(new ItemInstanceId(2))));

            float damage = WeaponDamageCalculator.Compute(10f, set.Get(DerivedStats.WeaponDamage), set.Get(DerivedStats.WeaponDamageMultiplier));

            Assert.AreEqual(10f + 5f + 4f, damage, 1e-3f, "Enhanced scaled the sword's 10, not the other item's 4.");
        }

        [Test]
        public void TheMeleeAttackAbility_StillAppliesPhysicalPowerAndTheComboScalar_ToTheWeaponDamage()
        {
            // The calculation hands the ability a weapon damage; the ability multiplies in the combo scalar and Physical Power, as before.
            AbilityDefinition melee = AssetDatabase.LoadAssetAtPath<AbilityDefinition>("Assets/Abilities/Abilities/MeleeAttack.asset");

            Assert.IsTrue(melee.Nodes.Any(n => n.TypeId == NodeTypes.EventWeaponDamage));
            Assert.IsTrue(melee.Nodes.Any(n => n.TypeId == NodeTypes.EventComboScalar));
            Assert.IsTrue(melee.Nodes.Any(n => n.TypeId == NodeTypes.Attribute && ((AttributeConfig)n.Config).AttributeKey == DerivedStats.PhysicalPower.Key));
        }
    }
}
