using SimpleRPG.HitDetection;
using NUnit.Framework;
using SimpleRPG.Gameplay;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using EntityId = SimpleRPG.Gameplay.EntityId;

namespace SimpleRPG.Abilities.Tests.EditMode
{
    /// <summary>
    /// The anatomical damage rule with the real resolver and the real <see cref="HitRegionProfile"/>: what a struck region is worth, that every kind of
    /// character is resolved by the same code, and that attribution and the one-adjustment-per-blow guarantee hold.
    /// <see cref="Body"/> stands in only for the networked character, which hands the profile to the resolver exactly as it does.
    /// </summary>
    public class HitRegionDamageTests
    {
        // A body location outside the four the game starts with, standing for a nonhumanoid part (a spider's abdomen).
        private const BodyLocation Abdomen = (BodyLocation)4;

        private sealed class Body : ICombatant, IRegionDamageModifier
        {
            private readonly HitRegionProfile profile;

            public Body(ulong id, int health, HitRegionProfile profile)
            {
                Id = new EntityId(id);
                Health = health;
                IsAlive = true;
                LifeId = 1;
                this.profile = profile;
            }

            public EntityId Id { get; }

            public int Health { get; private set; }

            public bool IsAlive { get; private set; }

            public int LifeId { get; private set; }

            public Position3 Position => default;

            public int MultiplierQueries { get; private set; }

            public void CommitHealth(int health) => Health = health;

            public void CommitDeath() => IsAlive = false;

            public void CommitRevive(int health)
            {
                Health = health;
                IsAlive = true;
                LifeId++;
            }

            public float DamageMultiplier(int bodyLocation)
            {
                MultiplierQueries++;
                return profile != null ? profile.MultiplierFor(bodyLocation) : 1f;
            }
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

        private GameplayEventDispatcher events;
        private CombatantRegistry registry;
        private CombatResolver combat;
        private HitRegionProfile standard;
        private HitRegionProfile spider;
        private readonly List<Object> created = new List<Object>();

        [SetUp]
        public void SetUp()
        {
            events = new GameplayEventDispatcher();
            registry = new CombatantRegistry();
            combat = new CombatResolver(events, registry);

            standard = Profile(
                new HitRegionProfile.Entry(BodyLocation.Head, 1.75f),
                new HitRegionProfile.Entry(BodyLocation.Torso, 1f),
                new HitRegionProfile.Entry(BodyLocation.Arms, 0.75f),
                new HitRegionProfile.Entry(BodyLocation.Legs, 0.75f));

            spider = Profile(
                new HitRegionProfile.Entry(BodyLocation.Head, 2f),
                new HitRegionProfile.Entry(Abdomen, 0.5f),
                new HitRegionProfile.Entry(BodyLocation.Legs, 0.25f));
        }

        [TearDown]
        public void TearDown()
        {
            foreach (Object o in created)
            {
                if (o != null) Object.DestroyImmediate(o);
            }

            created.Clear();
        }

        private HitRegionProfile Profile(params HitRegionProfile.Entry[] entries)
        {
            HitRegionProfile profile = ScriptableObject.CreateInstance<HitRegionProfile>();
            profile.Configure(entries);
            created.Add(profile);
            return profile;
        }

        private Body Add(ulong id, HitRegionProfile profile, int health = 1000)
        {
            Body body = new Body(id, health, profile);
            registry.Register(body);
            return body;
        }

        private static DamageRequest Blow(ICombatant target, int amount, BodyLocation where, EntityId by = default, DamageProvenance provenance = DamageProvenance.DirectWeaponHit) =>
            new DamageRequest(target.Id, amount, by, bodyLocation: (int)where, provenance: provenance);

        // ----- Classification and multipliers -----

        [TestCase(BodyLocation.Head, 175)]
        [TestCase(BodyLocation.Torso, 100)]
        [TestCase(BodyLocation.Arms, 75)]
        [TestCase(BodyLocation.Legs, 75)]
        public void AWeaponBlow_IsScaledByTheStruckRegion(BodyLocation where, int expected)
        {
            Body target = Add(1, standard);

            DamageOutcome outcome = combat.Apply(Blow(target, 100, where));

            Assert.AreEqual(DamageResult.Applied, outcome.Result);
            Assert.AreEqual(1000 - expected, target.Health);
        }

        [Test]
        public void ARegionTheProfileDoesNotList_CountsAsOne()
        {
            Body target = Add(1, Profile(new HitRegionProfile.Entry(BodyLocation.Head, 3f)));

            combat.Apply(Blow(target, 100, BodyLocation.Legs));

            Assert.AreEqual(900, target.Health);
        }

        [Test]
        public void ACombatantWithNoAnatomy_TakesTheBlowUnchanged()
        {
            PlainBody target = new PlainBody(1, 1000);
            registry.Register(target);

            combat.Apply(Blow(target, 100, BodyLocation.Head));

            Assert.AreEqual(900, target.Health);
        }

        [Test]
        public void ACombatantWithAnAnatomyButNoProfile_TakesTheBlowUnchanged()
        {
            Body target = Add(1, null);

            combat.Apply(Blow(target, 100, BodyLocation.Head));

            Assert.AreEqual(900, target.Health);
        }

        // ----- One path for every kind of character -----

        [Test]
        public void PlayersAndMonsters_AreResolvedByTheSameCodeFromTheirOwnTables()
        {
            Body player = Add(1, standard);
            Body monster = Add(2, standard);
            Body arachnid = Add(3, spider);

            combat.Apply(Blow(player, 100, BodyLocation.Head));
            combat.Apply(Blow(monster, 100, BodyLocation.Head));
            combat.Apply(Blow(arachnid, 100, BodyLocation.Head));

            Assert.AreEqual(825, player.Health);
            Assert.AreEqual(player.Health, monster.Health, "the same table gives the same damage whoever wears it");
            Assert.AreEqual(800, arachnid.Health, "a different table gives its own damage through the same resolver");
        }

        [Test]
        public void ANonhumanoidAnatomy_ScalesItsOwnRegions()
        {
            Body arachnid = Add(1, spider);

            combat.Apply(Blow(arachnid, 100, Abdomen));
            Assert.AreEqual(950, arachnid.Health, "a region beyond the humanoid four is worth what its table says");

            combat.Apply(Blow(arachnid, 100, BodyLocation.Legs));
            Assert.AreEqual(925, arachnid.Health, "and the same location can be worth something else on another body");

            combat.Apply(Blow(arachnid, 100, BodyLocation.Torso));
            Assert.AreEqual(825, arachnid.Health, "a location it has no entry for counts as one");
        }

        // ----- Only weapon blows to a region are scaled, and only once -----

        [Test]
        public void ASpellOrATick_IsNotScaled_EvenWithALocation()
        {
            Body target = Add(1, standard);

            combat.Apply(Blow(target, 100, BodyLocation.Head, provenance: DamageProvenance.Spell));
            combat.Apply(Blow(target, 100, BodyLocation.Head, provenance: DamageProvenance.Periodic));
            combat.Apply(Blow(target, 100, BodyLocation.Head, provenance: DamageProvenance.None));

            Assert.AreEqual(700, target.Health);
        }

        [Test]
        public void ADamageRequestWithNoLocation_IsNotScaled()
        {
            Body target = Add(1, standard);

            combat.Apply(new DamageRequest(target.Id, 100, provenance: DamageProvenance.DirectWeaponHit));

            Assert.AreEqual(900, target.Health);
            Assert.AreEqual(0, target.MultiplierQueries, "a blow that struck no region does not even ask the body what its regions are worth");
        }

        [Test]
        public void ABlowIsAdjustedOnce_AndTheEventsCarryTheFinalAmount()
        {
            Body target = Add(1, standard);
            List<DamageResolvedEvent> seen = new List<DamageResolvedEvent>();
            events.Subscribe<DamageResolvedEvent>(seen.Add);

            combat.Apply(Blow(target, 100, BodyLocation.Head));

            Assert.AreEqual(1, target.MultiplierQueries);
            Assert.AreEqual(1, seen.Count);
            Assert.AreEqual(175, seen[0].Request.Amount);
            Assert.AreEqual(1000, seen[0].HealthBefore);
            Assert.AreEqual(825, seen[0].HealthAfter);
        }

        [Test]
        public void Attribution_SurvivesTheAdjustment()
        {
            Body target = Add(1, standard);
            Body attacker = Add(2, standard);
            SourceHandle source = SourceHandle.FromEntity(attacker.Id);
            List<DamageResolvedEvent> seen = new List<DamageResolvedEvent>();
            events.Subscribe<DamageResolvedEvent>(seen.Add);

            combat.Apply(new DamageRequest(target.Id, 100, attacker.Id, source, (int)BodyLocation.Head, DamageProvenance.DirectWeaponHit));

            DamageRequest request = seen.Single().Request;
            Assert.AreEqual(attacker.Id, request.Instigator);
            Assert.AreEqual(source, request.Source);
            Assert.AreEqual((int)BodyLocation.Head, request.BodyLocation);
            Assert.AreEqual(DamageProvenance.DirectWeaponHit, request.Provenance);
        }

        [Test]
        public void AKillingBlow_IsDecidedByTheScaledAmount()
        {
            Body target = Add(1, standard, health: 150);

            DamageOutcome torso = combat.Apply(Blow(target, 100, BodyLocation.Torso));
            Assert.AreEqual(DamageResult.Applied, torso.Result, "100 does not kill 150 health");

            Body second = Add(2, standard, health: 150);
            DamageOutcome head = combat.Apply(Blow(second, 100, BodyLocation.Head));
            Assert.AreEqual(DamageResult.Killed, head.Result, "a headshot of 175 does");
            Assert.AreEqual(0, second.Health);
        }

        [Test]
        public void ALandedBlow_NeverScalesDownToNothing()
        {
            Body target = Add(1, Profile(new HitRegionProfile.Entry(BodyLocation.Legs, 0.1f)));

            DamageOutcome outcome = combat.Apply(Blow(target, 2, BodyLocation.Legs));

            Assert.AreEqual(DamageResult.Applied, outcome.Result);
            Assert.AreEqual(999, target.Health);
        }

        [Test]
        public void ARegionWorthNothing_DealsNoDamage()
        {
            Body target = Add(1, Profile(new HitRegionProfile.Entry(BodyLocation.Arms, 0f)));

            DamageOutcome outcome = combat.Apply(Blow(target, 100, BodyLocation.Arms));

            Assert.AreEqual(DamageResult.RejectedNoDamage, outcome.Result);
            Assert.AreEqual(1000, target.Health);
        }

        // ----- The profile itself -----

        [Test]
        public void TheProfile_UsesTheFirstEntryForALocationListedTwice_AndRefusesNegatives()
        {
            HitRegionProfile profile = Profile(
                new HitRegionProfile.Entry(BodyLocation.Head, 2f),
                new HitRegionProfile.Entry(BodyLocation.Head, 9f),
                new HitRegionProfile.Entry(BodyLocation.Legs, -3f));

            Assert.AreEqual(2f, profile.MultiplierFor((int)BodyLocation.Head));
            Assert.AreEqual(0f, profile.MultiplierFor((int)BodyLocation.Legs));
            Assert.AreEqual(1f, profile.MultiplierFor(DamageRequest.NoBodyLocation));
        }

        // ----- Choosing between overlapping regions -----

        private static HitCandidate Candidate(BodyLocation where, float surface, float centre, int priority = 0, string key = "") =>
            new HitCandidate(null, where, priority, key, surface, centre);

        [Test]
        public void TheRegionTheBladeIsNearestIsChosen_NotTheOneWorthMore()
        {
            HitCandidate head = Candidate(BodyLocation.Head, surface: 0.04f, centre: 0.2f);
            HitCandidate torso = Candidate(BodyLocation.Torso, surface: 0f, centre: 0.1f);

            Assert.IsTrue(torso.IsPreferredTo(head), "the blade is inside the torso and only near the head");
            Assert.IsFalse(head.IsPreferredTo(torso));
        }

        [Test]
        public void WhenTheBladeIsInsideBothBoxes_TheNearerCentreWins_ThenPriority_ThenName()
        {
            HitCandidate nearHead = Candidate(BodyLocation.Head, 0f, 0.05f, key: "z");
            HitCandidate nearTorso = Candidate(BodyLocation.Torso, 0f, 0.15f, key: "a");
            Assert.IsTrue(nearHead.IsPreferredTo(nearTorso));

            HitCandidate lowPriority = Candidate(BodyLocation.Torso, 0f, 0.1f, priority: 0, key: "a");
            HitCandidate highPriority = Candidate(BodyLocation.Head, 0f, 0.1f, priority: 1, key: "z");
            Assert.IsTrue(highPriority.IsPreferredTo(lowPriority));

            HitCandidate first = Candidate(BodyLocation.Torso, 0f, 0.1f, key: "Chest");
            HitCandidate second = Candidate(BodyLocation.Torso, 0f, 0.1f, key: "Spine");
            Assert.IsTrue(first.IsPreferredTo(second));
            Assert.IsFalse(second.IsPreferredTo(first));
        }

        [Test]
        public void NoiseBelowATenthOfAMillimetre_CannotFlipTheChoice()
        {
            HitCandidate a = Candidate(BodyLocation.Head, 0.00005f, 0.1f, priority: 1, key: "a");
            HitCandidate b = Candidate(BodyLocation.Torso, 0f, 0.1f, priority: 0, key: "b");

            Assert.IsTrue(a.IsPreferredTo(b), "equal within tolerance, so priority decides");
        }

        [Test]
        public void TheChoice_DoesNotDependOnTheOrderPhysicsListedTheColliders()
        {
            HitCandidate[] set =
            {
                Candidate(BodyLocation.Torso, 0f, 0.12f, key: "Chest"),
                Candidate(BodyLocation.Head, 0f, 0.12f, key: "Head"),
                Candidate(BodyLocation.Torso, 0f, 0.12f, key: "Spine"),
                Candidate(BodyLocation.Arms, 0.03f, 0.2f, key: "Arm"),
            };

            string expected = null;
            foreach (int[] order in Permutations(set.Length))
            {
                HitCandidate best = set[order[0]];
                for (int i = 1; i < order.Length; i++)
                {
                    if (set[order[i]].IsPreferredTo(best)) best = set[order[i]];
                }

                expected = expected ?? best.Key;
                Assert.AreEqual(expected, best.Key, "every ordering must pick the same contact");
            }

            Assert.AreEqual("Chest", expected);
        }

        private static IEnumerable<int[]> Permutations(int count)
        {
            int[] items = Enumerable.Range(0, count).ToArray();
            return Permute(items, 0);
        }

        private static IEnumerable<int[]> Permute(int[] items, int start)
        {
            if (start == items.Length - 1)
            {
                yield return (int[])items.Clone();
                yield break;
            }

            for (int i = start; i < items.Length; i++)
            {
                (items[start], items[i]) = (items[i], items[start]);
                foreach (int[] result in Permute(items, start + 1)) yield return result;
                (items[start], items[i]) = (items[i], items[start]);
            }
        }
    }
}
