using NUnit.Framework;
using SimpleRPG.Gameplay;
using UnityEditor;
using UnityEngine;

namespace SimpleRPG.Abilities.Tests.EditMode
{
    /// <summary>
    /// Asserts the approved baselines recorded in the shipped player stat profile, so a balance change is a deliberate edit to the asset and these
    /// numbers, never a constant hidden in a consumer. A character with all core attributes at 10 keeps today's behavior: 100 health, speed 4.
    /// </summary>
    public class StatBaselineTests
    {
        private const string PlayerStats = "Assets/Stats/PlayerStats.asset";

        private static AttributeSet PlayerWith(double core)
        {
            StatProfile profile = AssetDatabase.LoadAssetAtPath<StatProfile>(PlayerStats);
            Assert.IsNotNull(profile, PlayerStats);
            AttributeSet set = profile.CreateSet();
            foreach (AttributeId primary in PrimaryAttributes.All) set.SetBase(primary, core);
            return set;
        }

        [Test]
        public void Profile_IsDerivedAndDefinesTheCurrentlyConsumedStats()
        {
            StatProfile profile = AssetDatabase.LoadAssetAtPath<StatProfile>(PlayerStats);

            Assert.AreEqual(StatOwnership.Derived, profile.Ownership);
            CollectionAssert.AreEquivalent(
                new[] { DerivedStats.MaximumHealth, DerivedStats.MovementSpeed, DerivedStats.PhysicalPower, DerivedStats.MagicalPower, DerivedStats.PhysicalDamageReduction, DerivedStats.MagicalDamageReduction },
                System.Linq.Enumerable.Select(profile.Sheet.Rules, x => x.Stat));
        }

        [Test]
        public void AtTen_PreservesTodaysHealthAndMovement()
        {
            AttributeSet set = PlayerWith(10);

            Assert.AreEqual(100d, set.Get(DerivedStats.MaximumHealth), 1e-9, "10 per point of Constitution.");
            Assert.AreEqual(4d, set.Get(DerivedStats.MovementSpeed), 1e-9, "0.4 per point of Agility.");
        }

        [Test]
        public void SkeletonFootman_HasADirectSheetAndNeedsNoCoreAttributes()
        {
            StatProfile profile = AssetDatabase.LoadAssetAtPath<StatProfile>("Assets/Stats/SkeletonStats.asset");
            Assert.IsNotNull(profile);
            Assert.AreEqual(StatOwnership.Direct, profile.Ownership);

            AttributeSet set = profile.CreateSet();

            Assert.AreEqual(75d, set.Get(DerivedStats.MaximumHealth), "Its former authored health.");
            Assert.AreEqual(33d, set.Get(DerivedStats.PhysicalPower), "Enough for about 40 per hit with a 15 damage weapon.");
            Assert.AreEqual(0d, set.Get(PrimaryAttributes.Strength));

            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Characters/Enemies/SkeletonFootman.prefab");
            Assert.AreSame(profile, new SerializedObject(prefab.GetComponent<Character>()).FindProperty("statProfile").objectReferenceValue);
        }

        [Test]
        public void PowerFollowsItsCoreAttribute()
        {
            AttributeSet set = PlayerWith(10);
            set.SetBase(PrimaryAttributes.Strength, 15);
            set.SetBase(PrimaryAttributes.Intelligence, 20);

            Assert.AreEqual(15d, set.Get(DerivedStats.PhysicalPower), 1e-9);
            Assert.AreEqual(20d, set.Get(DerivedStats.MagicalPower), 1e-9);
        }

        [Test]
        public void Reductions_AreDiminishingFractionsBelowTheirCap()
        {
            // Reduction is a fraction of damage removed, 0 to 0.9: 0.9 * x / (x + 100) of Constitution (physical) or Willpower (magical).
            // Not yet applied to damage; the semantics are recorded here for when combat consumes them.
            Assert.AreEqual(0d, PlayerWith(0).Get(DerivedStats.PhysicalDamageReduction), 1e-9);
            Assert.AreEqual(0.9d * 10 / 110, PlayerWith(10).Get(DerivedStats.PhysicalDamageReduction), 1e-9);
            Assert.AreEqual(0.45d, PlayerWith(100).Get(DerivedStats.MagicalDamageReduction), 1e-9);
            Assert.Less(PlayerWith(100000).Get(DerivedStats.PhysicalDamageReduction), 0.9d);
        }

        [Test]
        public void CataloguedButUnconsumedStats_AreNotAuthored()
        {
            AttributeSet set = PlayerWith(10);

            foreach (AttributeId stat in new[] { DerivedStats.AttackSpeed, DerivedStats.CastingSpeed, DerivedStats.InteractionSpeed, DerivedStats.EquipSpeed, DerivedStats.CarryingCapacity, DerivedStats.SpellPreparationCapacity })
            {
                Assert.AreEqual(0d, set.Get(stat), stat.Key);
            }
        }
    }
}
