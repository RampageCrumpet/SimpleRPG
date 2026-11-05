using NUnit.Framework;
using SimpleRPG.Abilities;
using SimpleRPG.Gameplay;
using System.Collections.Generic;
using UnityEditor;
using EntityId = SimpleRPG.Gameplay.EntityId;

namespace SimpleRPG.Abilities.Tests.EditMode
{
    /// <summary>
    /// A soft cap: a stat counts in full up to a threshold and for a fraction of each further point after it, on its final value.
    /// </summary>
    public class SoftCapTests
    {
        [Test]
        public void BelowTheThreshold_NothingChanges()
        {
            var cap = new SoftCap(0.6, 0.25);

            Assert.AreEqual(0.3, cap.Apply(0.3), 1e-12);
            Assert.AreEqual(0.6, cap.Apply(0.6), 1e-12);
            Assert.AreEqual(-5d, cap.Apply(-5d), 1e-12);
        }

        [Test]
        public void PastTheThreshold_EachPointCountsForTheSlope()
        {
            var cap = new SoftCap(0.6, 0.25);

            Assert.AreEqual(0.6 + 0.4 * 0.25, cap.Apply(1.0), 1e-12);
            Assert.AreEqual(0.6, new SoftCap(0.6, 0d).Apply(5d), 1e-12, "A slope of zero is a hard cap.");
            Assert.AreEqual(5d, new SoftCap(0.6, 1d).Apply(5d), 1e-12, "A slope of one is no cap.");
        }

        [Test]
        public void ItNeverStopsGrowth_AndNeverReverses()
        {
            var cap = new SoftCap(10d, 0.1);
            double last = double.MinValue;
            for (double v = 0d; v < 1000d; v += 7.3)
            {
                double capped = cap.Apply(v);
                Assert.GreaterOrEqual(capped, last);
                last = capped;
            }

            Assert.Greater(cap.Apply(1000d), cap.Apply(100d));
        }

        [Test]
        public void ANoThreshold_MeansNoCap()
        {
            Assert.IsFalse(default(SoftCap).IsActive);
            Assert.AreEqual(123d, default(SoftCap).Apply(123d));
            Assert.IsFalse(new SoftCap(0d, 0.5).IsActive);
        }

        [Test]
        public void ABadCap_IsRefused()
        {
            Assert.Throws<System.ArgumentException>(() => new SoftCap(1d, 1.5));
            Assert.Throws<System.ArgumentException>(() => new SoftCap(1d, -0.1));
            Assert.Throws<System.ArgumentException>(() => new SoftCap(double.NaN, 0.5));
        }

        [Test]
        public void ADerivedStatWithACap_IsHeldToItAfterEveryModifier()
        {
            var stat = new AttributeId("test.stat.capped");
            var rule = new StatRule(stat, 0.5, null, new SoftCap(1d, 0.5));
            var sheet = new StatSheet(StatOwnership.Derived, new[] { rule });
            AttributeSet set = sheet.CreateSet();
            SourceHandle source = SourceHandle.FromEntity(new EntityId(1));

            Assert.AreEqual(0.5, set.Get(stat), 1e-12);

            set.AddModifier(AttributeModifier.Flat(stat, 2d, source));
            Assert.AreEqual(1d + 1.5 * 0.5, set.Get(stat), 1e-12, "0.5 + 2 = 2.5 resolves to 1 + 1.5 x 0.5.");

            set.AddModifier(new AttributeModifier(stat, ModifierLayer.Multiply, 2d, source));
            Assert.AreEqual(1d + 4d * 0.5, set.Get(stat), 1e-12, "A factor on top cannot get around the cap: (0.5 + 2) x 2 = 5.");
        }

        [Test]
        public void ThePlayersDamageReductions_AreSoftCapped_AndAStartingCharacterIsUnaffected()
        {
            StatProfile profile = AssetDatabase.LoadAssetAtPath<StatProfile>("Assets/Stats/PlayerStats.asset");
            AttributeSet set = profile.CreateSet();
            foreach (AttributeId primary in PrimaryAttributes.All) set.SetBase(primary, 10);

            double starting = set.Get(DerivedStats.PhysicalDamageReduction);
            Assert.AreEqual(0.9 * 10d / 110d, starting, 1e-9, "Well under the cap, so exactly what it was.");

            set.AddModifier(AttributeModifier.Flat(DerivedStats.ArmorRating, 100000, SourceHandle.FromItem(new ItemInstanceId(1))));
            double raw = 0.9 * 10d / 110d + 0.9 * 100000d / 100100d;
            Assert.AreEqual(0.6 + (raw - 0.6) * 0.25, set.Get(DerivedStats.PhysicalDamageReduction), 1e-9);
            Assert.Less(set.Get(DerivedStats.PhysicalDamageReduction), DamageMitigation.MaxReduction);
        }

        [Test]
        public void MovementAndInteractionSpeedAndWeaponDamage_AreSoftCappedInTheShippedProfile()
        {
            StatProfile profile = AssetDatabase.LoadAssetAtPath<StatProfile>("Assets/Stats/PlayerStats.asset");
            AttributeSet set = profile.CreateSet();
            foreach (AttributeId primary in PrimaryAttributes.All) set.SetBase(primary, 10);
            SourceHandle source = SourceHandle.FromItem(new ItemInstanceId(1));

            Assert.AreEqual(4d, set.Get(DerivedStats.MovementSpeed), 1e-9, "A starting character is unchanged.");
            Assert.AreEqual(1d, set.Get(DerivedStats.InteractionSpeed), 1e-9);
            Assert.AreEqual(1d, set.Get(DerivedStats.WeaponDamageMultiplier), 1e-9);

            set.AddModifier(new AttributeModifier(DerivedStats.MovementSpeed, ModifierLayer.PercentAdd, 2d, source));
            set.AddModifier(new AttributeModifier(DerivedStats.InteractionSpeed, ModifierLayer.PercentAdd, 4d, source));
            set.AddModifier(new AttributeModifier(DerivedStats.WeaponDamageMultiplier, ModifierLayer.PercentAdd, 2d, source));

            Assert.AreEqual(6d + (12d - 6d) * 0.5, set.Get(DerivedStats.MovementSpeed), 1e-9, "+200% would be 12; past 6 it counts half.");
            Assert.AreEqual(2d + (5d - 2d) * 0.5, set.Get(DerivedStats.InteractionSpeed), 1e-9);
            Assert.AreEqual(1.5d + (3d - 1.5d) * 0.5, set.Get(DerivedStats.WeaponDamageMultiplier), 1e-9);
        }
    }
}
