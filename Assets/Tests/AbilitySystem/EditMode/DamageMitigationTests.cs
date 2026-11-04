using NUnit.Framework;
using SimpleRPG.Gameplay;

namespace SimpleRPG.Abilities.Tests.EditMode
{
    /// <summary>
    /// How defences change a blow: which defence a blow meets, the rules of the arithmetic, and the real resolver applying them.
    /// <see cref="Body"/> stands in only for the networked character, which hands its stats to the resolver the same way.
    /// </summary>
    public class DamageMitigationTests
    {
        private sealed class Body : ICombatant, IDamageMitigator, IRegionDamageModifier
        {
            private readonly double physical;
            private readonly double magical;
            private readonly float regionFactor;

            public Body(ulong id, int health, double physical = 0d, double magical = 0d, float regionFactor = 1f)
            {
                Id = new EntityId(id);
                Health = health;
                IsAlive = true;
                LifeId = 1;
                this.physical = physical;
                this.magical = magical;
                this.regionFactor = regionFactor;
            }

            public EntityId Id { get; }

            public int Health { get; private set; }

            public bool IsAlive { get; private set; }

            public int LifeId { get; private set; }

            public Position3 Position => default;

            public void CommitHealth(int health) => Health = health;

            public void CommitDeath() => IsAlive = false;

            public void CommitRevive(int health) => Health = health;

            public double Reduction(DamageType type) => type == DamageType.Magical ? magical : physical;

            public float DamageMultiplier(int bodyLocation) => regionFactor;
        }

        private sealed class PlainBody : ICombatant
        {
            public PlainBody(ulong id, int health)
            {
                Id = new EntityId(id);
                Health = health;
                IsAlive = true;
                LifeId = 1;
            }

            public EntityId Id { get; }

            public int Health { get; private set; }

            public bool IsAlive { get; private set; }

            public int LifeId { get; private set; }

            public Position3 Position => default;

            public void CommitHealth(int health) => Health = health;

            public void CommitDeath() => IsAlive = false;

            public void CommitRevive(int health) => Health = health;
        }

        private CombatantRegistry registry;
        private CombatResolver combat;

        [SetUp]
        public void SetUp()
        {
            registry = new CombatantRegistry();
            combat = new CombatResolver(new GameplayEventDispatcher(), registry);
        }

        private static DamageRequest Hit(ICombatant target, int amount, DamageType type = DamageType.Physical, bool bypasses = false,
            DamageProvenance provenance = DamageProvenance.None, int bodyLocation = DamageRequest.NoBodyLocation) =>
            new DamageRequest(target.Id, amount, bodyLocation: bodyLocation, provenance: provenance, type: type, bypassesMitigation: bypasses);

        // ---- Which defence a request meets: its type, and nothing else ----

        [Test]
        public void ARequestIsPhysicalAndMitigatedUnlessItSaysOtherwise()
        {
            var request = new DamageRequest(new EntityId(1), 10);

            Assert.AreEqual(DamageType.Physical, request.Type);
            Assert.IsFalse(request.BypassesMitigation);
        }

        [Test]
        public void ThereAreOnlyPhysicalAndMagicalDamage()
        {
            CollectionAssert.AreEquivalent(new[] { DamageType.Physical, DamageType.Magical }, System.Enum.GetValues(typeof(DamageType)));
        }

        [Test]
        public void ReductionForFollowsTheType_AndOnlyTheType()
        {
            var defences = new Body(1, 100, physical: 0.3d, magical: 0.6d);

            Assert.AreEqual(0.3d, DamageMitigation.ReductionFor(Hit(defences, 10, DamageType.Physical), defences), 1e-12);
            Assert.AreEqual(0.6d, DamageMitigation.ReductionFor(Hit(defences, 10, DamageType.Magical), defences), 1e-12);
        }

        [TestCase(DamageProvenance.None)]
        [TestCase(DamageProvenance.DirectWeaponHit)]
        [TestCase(DamageProvenance.Spell)]
        [TestCase(DamageProvenance.Proc)]
        [TestCase(DamageProvenance.Periodic)]
        [TestCase(DamageProvenance.Environmental)]
        public void HowDamageWasDelivered_NeverChoosesTheDefence(DamageProvenance provenance)
        {
            // The same physical damage meets physical reduction whether it was a swing, a spell's side effect or a periodic tick, and the same
            // goes for magical.
            var defences = new Body(1, 100, physical: 0.3d, magical: 0.6d);

            Assert.AreEqual(0.3d, DamageMitigation.ReductionFor(Hit(defences, 10, DamageType.Physical, provenance: provenance), defences), 1e-12);
            Assert.AreEqual(0.6d, DamageMitigation.ReductionFor(Hit(defences, 10, DamageType.Magical, provenance: provenance), defences), 1e-12);
        }

        [Test]
        public void ABypassingRequestMeetsNoDefence_OfEitherType()
        {
            var defences = new Body(1, 100, physical: 0.9d, magical: 0.9d);

            Assert.AreEqual(0d, DamageMitigation.ReductionFor(Hit(defences, 10, DamageType.Physical, bypasses: true), defences));
            Assert.AreEqual(0d, DamageMitigation.ReductionFor(Hit(defences, 10, DamageType.Magical, bypasses: true), defences));
        }

        [Test]
        public void ATargetWithNoDefences_MeetsNone()
        {
            Assert.AreEqual(0d, DamageMitigation.ReductionFor(new DamageRequest(new EntityId(1), 10), null));
        }

        [Test]
        public void ChangingTheAmountKeepsTheTypeAndTheBypass()
        {
            var request = new DamageRequest(new EntityId(1), 10, type: DamageType.Magical, bypassesMitigation: true);

            DamageRequest changed = request.WithAmount(3);

            Assert.AreEqual(3, changed.Amount);
            Assert.AreEqual(DamageType.Magical, changed.Type);
            Assert.IsTrue(changed.BypassesMitigation);
        }

        // ---- The arithmetic ----

        [TestCase(0d, 0d)]
        [TestCase(0.25d, 0.25d)]
        [TestCase(0.9d, 0.9d)]
        [TestCase(1.5d, 0.9d)]
        [TestCase(-0.2d, 0d)]
        [TestCase(double.NaN, 0d)]
        [TestCase(double.PositiveInfinity, 0.9d)]
        public void AReductionIsKeptBetweenNoneAndTheMaximum(double given, double expected)
        {
            Assert.AreEqual(expected, DamageMitigation.Clamp(given), 1e-12);
        }

        [Test]
        public void NoReduction_LeavesTheBlowAlone()
        {
            Assert.AreEqual(40, DamageMitigation.Final(40, 1d, 0d));
        }

        [Test]
        public void AReductionRemovesItsShare()
        {
            Assert.AreEqual(20, DamageMitigation.Final(40, 1d, 0.5d));
            Assert.AreEqual(36, DamageMitigation.Final(40, 1d, 0.1d));
        }

        [Test]
        public void RegionAndReductionAreAppliedTogether_AndRoundedOnce()
        {
            // 15 * 1.75 * 0.9 is 23.625, which rounds to 24. Rounding the region first (26.25 to 26) and then the reduction (23.4 to 23) would give 23.
            Assert.AreEqual(24, DamageMitigation.Final(15, 1.75d, 0.1d));
        }

        [Test]
        public void ABlowThatLandsDoesAtLeastOne_HoweverHighTheDefence()
        {
            Assert.AreEqual(1, DamageMitigation.Final(1, 1d, 0.9d));
            Assert.AreEqual(1, DamageMitigation.Final(3, 0.25d, 0.9d));
        }

        [Test]
        public void ARegionThatTakesNoDamage_TakesNone()
        {
            Assert.AreEqual(0, DamageMitigation.Final(100, 0d, 0d));
            Assert.AreEqual(0, DamageMitigation.Final(100, 0d, 0.5d));
        }

        [Test]
        public void NoDefenceRemovesMoreThanNinetyPercent()
        {
            Assert.AreEqual(DamageMitigation.Final(100, 1d, 0.9d), DamageMitigation.Final(100, 1d, 7d));
            Assert.AreEqual(10, DamageMitigation.Final(100, 1d, 0.9d));
        }

        [Test]
        public void NothingToDamage_DoesNothing()
        {
            Assert.AreEqual(0, DamageMitigation.Final(0, 1d, 0.5d));
            Assert.AreEqual(0, DamageMitigation.Final(-5, 1d, 0d));
        }

        // ---- Through the real resolver ----

        [Test]
        public void PhysicalDamage_MeetsPhysicalReduction()
        {
            Body target = new Body(1, 1000, physical: 0.5d, magical: 0d);
            registry.Register(target);

            combat.Apply(Hit(target, 40, DamageType.Physical));

            Assert.AreEqual(980, target.Health);
        }

        [Test]
        public void MagicalDamage_MeetsMagicalReduction_NotPhysical()
        {
            Body physicalOnly = new Body(1, 1000, physical: 0.5d, magical: 0d);
            Body magicalOnly = new Body(2, 1000, physical: 0d, magical: 0.5d);
            registry.Register(physicalOnly);
            registry.Register(magicalOnly);

            combat.Apply(Hit(physicalOnly, 40, DamageType.Magical));
            combat.Apply(Hit(magicalOnly, 40, DamageType.Magical));

            Assert.AreEqual(960, physicalOnly.Health, "Armour does not stop a spell.");
            Assert.AreEqual(980, magicalOnly.Health);
        }

        [TestCase(DamageProvenance.Periodic)]
        [TestCase(DamageProvenance.Environmental)]
        [TestCase(DamageProvenance.None)]
        public void DamageOverTimeIsReducedLikeAnyOther_ByItsType(DamageProvenance provenance)
        {
            Body target = new Body(1, 1000, physical: 0.5d, magical: 0.25d);
            registry.Register(target);

            combat.Apply(Hit(target, 40, DamageType.Physical, provenance: provenance));
            combat.Apply(Hit(target, 40, DamageType.Magical, provenance: provenance));

            Assert.AreEqual(1000 - 20 - 30, target.Health, "A physical tick meets physical reduction, a magical tick meets magical.");
        }

        [Test]
        public void ADeliberateBypass_SkipsTheDefences()
        {
            Body target = new Body(1, 1000, physical: 0.9d, magical: 0.9d);
            registry.Register(target);

            combat.Apply(Hit(target, 40, DamageType.Physical, bypasses: true));
            combat.Apply(Hit(target, 40, DamageType.Magical, bypasses: true));

            Assert.AreEqual(920, target.Health);
        }

        [Test]
        public void ABypassingBleed_IsPhysicalDamage_ThatIgnoresArmour_OnlyBecauseItSaysSo()
        {
            Body target = new Body(1, 1000, physical: 0.5d);
            registry.Register(target);

            combat.Apply(Hit(target, 10, DamageType.Physical, provenance: DamageProvenance.Periodic));
            combat.Apply(Hit(target, 10, DamageType.Physical, bypasses: true, provenance: DamageProvenance.Periodic));

            Assert.AreEqual(1000 - 5 - 10, target.Health);
        }

        [Test]
        public void ACombatantWithNoDefences_TakesTheDamageUnchanged()
        {
            PlainBody target = new PlainBody(1, 1000);
            registry.Register(target);

            combat.Apply(Hit(target, 40, DamageType.Physical));

            Assert.AreEqual(960, target.Health);
        }

        [Test]
        public void TheStruckRegionAndTheDefenceAreAppliedTogether()
        {
            Body target = new Body(1, 1000, physical: 0.1d, regionFactor: 1.75f);
            registry.Register(target);

            combat.Apply(Hit(target, 15, DamageType.Physical, provenance: DamageProvenance.DirectWeaponHit, bodyLocation: 0));

            Assert.AreEqual(1000 - 24, target.Health);
        }

        [Test]
        public void ADefenceCannotTurnAHitIntoAMiss()
        {
            Body target = new Body(1, 1000, physical: 0.9d);
            registry.Register(target);

            DamageOutcome outcome = combat.Apply(Hit(target, 1, DamageType.Physical));

            Assert.AreEqual(DamageResult.Applied, outcome.Result);
            Assert.AreEqual(999, target.Health);
        }

        [Test]
        public void WhetherADamageIsFatal_IsJudgedOnWhatLands()
        {
            // 30 would kill a character with 20 health, but half of it is turned aside.
            Body target = new Body(1, 20, physical: 0.5d);
            registry.Register(target);

            DamageOutcome outcome = combat.Apply(Hit(target, 30, DamageType.Physical));

            Assert.AreEqual(DamageResult.Applied, outcome.Result);
            Assert.AreEqual(5, target.Health);
            Assert.IsTrue(target.IsAlive);
        }

        [Test]
        public void TheEventsSeeTheDamageThatLanded()
        {
            var events = new GameplayEventDispatcher();
            var local = new CombatResolver(events, registry);
            Body target = new Body(1, 1000, physical: 0.5d);
            registry.Register(target);
            int reported = -1;
            events.Subscribe<DamageResolvedEvent>(e => reported = e.Request.Amount);

            local.Apply(Hit(target, 40, DamageType.Physical));

            Assert.AreEqual(20, reported);
        }
    }
}
