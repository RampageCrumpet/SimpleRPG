using NUnit.Framework;
using SimpleRPG.Gameplay;
using System.Linq;
using UnityEditor;

namespace SimpleRPG.Abilities.Tests.EditMode
{
    /// <summary>
    /// Armor Rating: a derived stat (not a seventh core attribute) that equipment supplies through the ordinary modifier layers, and that the
    /// player profile turns into Physical Damage Reduction through a contribution curve of its own, beside Constitution's and tuned separately.
    /// The numbers in the first group are the shipped profile's; the second group uses sheets built here to show the contributions are independent.
    /// </summary>
    public class ArmorRatingTests
    {
        private const string PlayerStats = "Assets/Stats/PlayerStats.asset";

        private static readonly SourceHandle Chestplate = SourceHandle.FromItem(new ItemInstanceId(1));
        private static readonly SourceHandle Helmet = SourceHandle.FromItem(new ItemInstanceId(2));

        private static AttributeSet Player(double core = 10)
        {
            StatProfile profile = AssetDatabase.LoadAssetAtPath<StatProfile>(PlayerStats);
            Assert.IsNotNull(profile, PlayerStats);
            AttributeSet set = profile.CreateSet();
            foreach (AttributeId primary in PrimaryAttributes.All) set.SetBase(primary, core);
            return set;
        }

        // What a Constitution of 10 contributes in the shipped profile: 0.9 * 10 / (10 + 100).
        private const double ConstitutionShare = 0.9d * 10d / 110d;

        // ---- It is a derived stat, and the core attributes are untouched ----

        [Test]
        public void ArmorRating_IsADerivedStat_NotASeventhCoreAttribute()
        {
            Assert.AreEqual(6, PrimaryAttributes.All.Length);
            Assert.IsFalse(PrimaryAttributes.All.Contains(DerivedStats.ArmorRating));
            Assert.IsTrue(DerivedStats.All.Contains(DerivedStats.ArmorRating));
            Assert.IsTrue(DerivedStats.IsCatalogued(DerivedStats.ArmorRating));
            Assert.AreEqual("simplerpg.stat.armor_rating", DerivedStats.ArmorRating.Key);
        }

        [Test]
        public void ThePlayerProfile_HasTheStat_StartingAtZero()
        {
            StatProfile profile = AssetDatabase.LoadAssetAtPath<StatProfile>(PlayerStats);

            Assert.IsTrue(profile.Sheet.Rules.Any(rule => rule.Stat == DerivedStats.ArmorRating));
            Assert.AreEqual(0d, Player().Get(DerivedStats.ArmorRating), "Nobody has armor until equipment supplies it.");
        }

        [Test]
        public void WithoutEquipment_PhysicalReductionIsConstitutionsAlone()
        {
            Assert.AreEqual(ConstitutionShare, Player().Get(DerivedStats.PhysicalDamageReduction), 1e-9);
        }

        // ---- Equipment supplies it through the existing modifier layers ----

        [Test]
        public void WornPiecesAddUp()
        {
            AttributeSet set = Player();

            set.AddModifier(AttributeModifier.Flat(DerivedStats.ArmorRating, 20, Chestplate));
            set.AddModifier(AttributeModifier.Flat(DerivedStats.ArmorRating, 8, Helmet));

            Assert.AreEqual(28d, set.Get(DerivedStats.ArmorRating), 1e-9);
        }

        [Test]
        public void ArmorRatingRaisesPhysicalReduction_OnItsOwnCurve()
        {
            AttributeSet set = Player();

            set.AddModifier(AttributeModifier.Flat(DerivedStats.ArmorRating, 20, Chestplate));

            double armorShare = 0.9d * 20d / (20d + 100d);
            Assert.AreEqual(ConstitutionShare + armorShare, set.Get(DerivedStats.PhysicalDamageReduction), 1e-9);
        }

        [Test]
        public void ArmorRating_DoesNotChangeMagicalReduction()
        {
            AttributeSet set = Player();
            double before = set.Get(DerivedStats.MagicalDamageReduction);

            set.AddModifier(AttributeModifier.Flat(DerivedStats.ArmorRating, 50, Chestplate));

            Assert.AreEqual(before, set.Get(DerivedStats.MagicalDamageReduction), 1e-12);
        }

        [Test]
        public void AnAffixCanAddToArmorRating_OrScaleIt()
        {
            AttributeSet set = Player();
            set.AddModifier(AttributeModifier.Flat(DerivedStats.ArmorRating, 20, Chestplate));

            // "+10 armor" and "+50% armor" are ordinary modifiers on the stat: the flat ones add first, then the percentage applies.
            set.AddModifier(AttributeModifier.Flat(DerivedStats.ArmorRating, 10, Chestplate));
            set.AddModifier(AttributeModifier.Percent(DerivedStats.ArmorRating, 0.5, Chestplate));

            Assert.AreEqual((20d + 10d) * 1.5d, set.Get(DerivedStats.ArmorRating), 1e-9);
        }

        [Test]
        public void TakingThePieceOffTakesItsArmorOff()
        {
            AttributeSet set = Player();
            set.AddModifier(AttributeModifier.Flat(DerivedStats.ArmorRating, 20, Chestplate));
            set.AddModifier(AttributeModifier.Flat(DerivedStats.ArmorRating, 8, Helmet));

            int removed = set.RemoveAllFromSource(Chestplate);

            Assert.AreEqual(1, removed);
            Assert.AreEqual(8d, set.Get(DerivedStats.ArmorRating), 1e-9);
        }

        [Test]
        public void ArmorRatingStacksWithDiminishingReturns_AndNeverReachesTheCap()
        {
            AttributeSet set = Player();
            set.AddModifier(AttributeModifier.Flat(DerivedStats.ArmorRating, 100000, Chestplate));

            double reduction = set.Get(DerivedStats.PhysicalDamageReduction);

            // The two contributions together can pass the cap before the resolver holds them to it.
            Assert.Greater(reduction, DamageMitigation.MaxReduction);
            Assert.AreEqual(DamageMitigation.MaxReduction, DamageMitigation.Clamp(reduction), 1e-12);
        }

        // ---- The contributions are independent ----

        // A sheet built here with deliberately unlike curves, to show nothing assumes an armor point is worth a Constitution point.
        private static AttributeSet Sheet(ContributionCurve constitution, ContributionCurve armor)
        {
            var sheet = new StatSheet(StatOwnership.Derived, new[]
            {
                new StatRule(DerivedStats.ArmorRating, 0),
                new StatRule(DerivedStats.PhysicalDamageReduction, 0, new[]
                {
                    new StatContribution(PrimaryAttributes.Constitution, constitution),
                    new StatContribution(DerivedStats.ArmorRating, armor),
                }),
            });
            return sheet.CreateSet();
        }

        [Test]
        public void EachContributionHasItsOwnScaleAndHalfPoint()
        {
            AttributeSet set = Sheet(ContributionCurve.Diminishing(0.4, 200), ContributionCurve.Diminishing(0.7, 30));
            set.SetBase(PrimaryAttributes.Constitution, 20);
            set.AddModifier(AttributeModifier.Flat(DerivedStats.ArmorRating, 30, Chestplate));

            double expected = 0.4d * 20d / (20d + 200d) + 0.7d * 30d / (30d + 30d);
            Assert.AreEqual(expected, set.Get(DerivedStats.PhysicalDamageReduction), 1e-9);
        }

        [Test]
        public void RetuningTheArmorCurveLeavesConstitutionsShareAlone()
        {
            AttributeSet gentle = Sheet(ContributionCurve.Diminishing(0.9, 100), ContributionCurve.Diminishing(0.3, 200));
            AttributeSet steep = Sheet(ContributionCurve.Diminishing(0.9, 100), ContributionCurve.Diminishing(0.8, 20));
            foreach (AttributeSet set in new[] { gentle, steep })
            {
                set.SetBase(PrimaryAttributes.Constitution, 10);
            }

            // With no armor worn the two sheets agree, because only the armor curve differs.
            Assert.AreEqual(gentle.Get(DerivedStats.PhysicalDamageReduction), steep.Get(DerivedStats.PhysicalDamageReduction), 1e-12);

            gentle.AddModifier(AttributeModifier.Flat(DerivedStats.ArmorRating, 40, Chestplate));
            steep.AddModifier(AttributeModifier.Flat(DerivedStats.ArmorRating, 40, Chestplate));
            Assert.Less(gentle.Get(DerivedStats.PhysicalDamageReduction), steep.Get(DerivedStats.PhysicalDamageReduction));
        }

        [Test]
        public void ConstitutionAndArmorPointsAreNotInterchangeable()
        {
            AttributeSet byConstitution = Sheet(ContributionCurve.Diminishing(0.9, 100), ContributionCurve.Diminishing(0.9, 100));
            AttributeSet byArmor = Sheet(ContributionCurve.Diminishing(0.9, 100), ContributionCurve.Diminishing(0.9, 400));
            byConstitution.SetBase(PrimaryAttributes.Constitution, 40);
            byArmor.AddModifier(AttributeModifier.Flat(DerivedStats.ArmorRating, 40, Chestplate));

            Assert.That(byConstitution.Get(DerivedStats.PhysicalDamageReduction), Is.Not.EqualTo(byArmor.Get(DerivedStats.PhysicalDamageReduction)).Within(1e-6),
                "Forty points of each are only worth the same if the curves are tuned that way.");
        }

        // ---- Armor lowers the damage that lands ----

        private sealed class Wearer : ICombatant, IDamageMitigator
        {
            private readonly AttributeSet set;

            public Wearer(ulong id, int health, AttributeSet set)
            {
                Id = new EntityId(id);
                Health = health;
                IsAlive = true;
                LifeId = 1;
                this.set = set;
            }

            public EntityId Id { get; }

            public int Health { get; private set; }

            public bool IsAlive { get; private set; }

            public int LifeId { get; private set; }

            public Position3 Position => default;

            public void CommitHealth(int health) => Health = health;

            public void CommitDeath() => IsAlive = false;

            public void CommitRevive(int health) => Health = health;

            // What CharacterStats does: the reduction stat for the type, held to the range a defence can have.
            public double Reduction(DamageType type) =>
                DamageMitigation.Clamp(set.Get(type == DamageType.Magical ? DerivedStats.MagicalDamageReduction : DerivedStats.PhysicalDamageReduction));
        }

        [Test]
        public void ArmorTurnsAsideMoreOfAWeaponBlow_ButNotOfASpell()
        {
            AttributeSet set = Player();
            set.AddModifier(AttributeModifier.Flat(DerivedStats.ArmorRating, 100, Chestplate));
            var registry = new CombatantRegistry();
            var combat = new CombatResolver(new GameplayEventDispatcher(), registry);
            var bare = new Wearer(1, 1000, Player());
            var armoured = new Wearer(2, 1000, set);
            registry.Register(bare);
            registry.Register(armoured);

            combat.Apply(new DamageRequest(bare.Id, 100, type: DamageType.Physical));
            combat.Apply(new DamageRequest(armoured.Id, 100, type: DamageType.Physical));
            Assert.AreEqual(1000 - 92, bare.Health, "Constitution alone turns aside about 8%.");
            Assert.AreEqual(1000 - 47, armoured.Health, "100 armor adds 0.45 on top of Constitution's 0.082.");

            int bareBefore = bare.Health;
            int armouredBefore = armoured.Health;
            combat.Apply(new DamageRequest(bare.Id, 100, type: DamageType.Magical));
            combat.Apply(new DamageRequest(armoured.Id, 100, type: DamageType.Magical));

            // Willpower alone turns aside the same 8% of a spell on both; armor is not a magical defence.
            Assert.AreEqual(bareBefore - 92, bare.Health);
            Assert.AreEqual(armouredBefore - 92, armoured.Health);
        }
    }
}
