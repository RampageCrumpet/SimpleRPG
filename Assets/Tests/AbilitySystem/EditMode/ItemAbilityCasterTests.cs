using NUnit.Framework;
using SimpleRPG.Gameplay;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using EntityId = SimpleRPG.Gameplay.EntityId;

namespace SimpleRPG.Abilities.Tests.EditMode
{
    /// <summary>
    /// Charged items with the real caster, executor, charge store, cooldowns and combat pipeline on a manual clock. The staff holds three Firebolt
    /// charges for a wielder who has never learned Firebolt.
    /// </summary>
    public class ItemAbilityCasterTests
    {
        private static readonly AttributeId MagicPower = new AttributeId(AbilityBuilder.MagicPower);
        private static readonly AttributeId Intelligence = PrimaryAttributes.Intelligence;
        private static readonly ItemInstanceId StaffA = new ItemInstanceId(10);
        private static readonly ItemInstanceId StaffB = new ItemInstanceId(11);

        private readonly List<Object> created = new List<Object>();

        private ManualGameplayClock clock;
        private GameplayEventDispatcher events;
        private CombatantRegistry combatants;
        private CombatResolver combat;
        private AttributeSetRegistry attributes;
        private ScriptedProjectiles projectiles;
        private ExecutionServices services;
        private AbilityExecutor executor;
        private ItemStateStore states;
        private ItemAbilityCaster caster;
        private GameObject prefab;
        private AbilityDefinition firebolt;
        private AttributeSet wielderStats;

        private TestCombatant wielder;
        private TestCombatant target;

        [SetUp]
        public void SetUp()
        {
            clock = new ManualGameplayClock(30);
            events = new GameplayEventDispatcher();
            combatants = new CombatantRegistry();
            combat = new CombatResolver(events, combatants);
            attributes = new AttributeSetRegistry();
            projectiles = new ScriptedProjectiles();
            services = new ExecutionServices(attributes, combat, projectiles);
            executor = new AbilityExecutor(services);
            states = new ItemStateStore();
            caster = new ItemAbilityCaster(executor, states, new CooldownTracker(clock));
            prefab = new GameObject("Projectile");
            created.Add(prefab);
            firebolt = AbilityBuilder.Firebolt(prefab);
            created.Add(firebolt);

            wielder = Add(1, 100);
            target = Add(2, 1000);
            wielderStats = attributes.GetOrCreate(SourceHandle.FromEntity(wielder.Id));
            wielderStats.SetBase(MagicPower, 99);
            wielderStats.SetBase(Intelligence, 10);

            // The staff's own magic power: what its Firebolt reads, whoever holds it.
            SetStaffPower(StaffA, 20);
            SetStaffPower(StaffB, 20);
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

        private TestCombatant Add(ulong id, int health)
        {
            TestCombatant combatant = new TestCombatant(id, health);
            combatants.Register(combatant);
            return combatant;
        }

        private void SetStaffPower(ItemInstanceId staff, double value) =>
            attributes.GetOrCreate(SourceHandle.FromItem(staff)).SetBase(MagicPower, value);

        private static ItemAbilityGrant Grant(AbilityDefinition ability, int cost = 1, params AttributeRequirement[] requirements)
        {
            ItemAbilityGrant grant = new ItemAbilityGrant { Ability = ability, ChargeCost = cost };
            grant.Requirements.AddRange(requirements);
            return grant;
        }

        private ItemCastResult Cast(ItemInstanceId staff, ItemAbilityGrant grant, bool alive = true, bool noAttributes = false) =>
            caster.Cast(new ItemCastRequest(wielder.Id, staff, grant, noAttributes ? null : wielderStats, default, new Position3(0, 0, 1)), alive);

        /// <summary>
        /// Lets the ability's cooldown pass.
        /// </summary>
        private void WaitOutCooldown() => clock.AdvanceSeconds(1.1);

        private int Charges(ItemInstanceId staff) => ItemCharges.Remaining(states, staff);

        // ----- Charges -----

        [Test]
        public void AStaffWithThreeCharges_CastsThreeTimes_ThenIsRefused()
        {
            ItemCharges.Ensure(states, StaffA, 3);
            ItemAbilityGrant grant = Grant(firebolt);

            List<ItemCastResult> results = new List<ItemCastResult>();
            for (int i = 0; i < 4; i++)
            {
                results.Add(Cast(StaffA, grant));
                WaitOutCooldown();
            }

            CollectionAssert.AreEqual(new[] { ItemCastResult.Cast, ItemCastResult.Cast, ItemCastResult.Cast, ItemCastResult.NotEnoughCharges }, results);
            Assert.AreEqual(3, projectiles.Launches.Count, "Only three Firebolts were launched.");
            Assert.AreEqual(0, Charges(StaffA));
        }

        [Test]
        public void EachCast_SpendsItsCostExactlyOnce()
        {
            ItemCharges.Ensure(states, StaffA, 5);

            Cast(StaffA, Grant(firebolt, cost: 2));

            Assert.AreEqual(3, Charges(StaffA));
        }

        [Test]
        public void AnAbilityCostingMoreThanIsLeft_IsRefused_AndSpendsNothing()
        {
            ItemCharges.Ensure(states, StaffA, 2);

            ItemCastResult result = Cast(StaffA, Grant(firebolt, cost: 3));

            Assert.AreEqual(ItemCastResult.NotEnoughCharges, result);
            Assert.AreEqual(2, Charges(StaffA));
            Assert.AreEqual(0, projectiles.Launches.Count);
        }

        [Test]
        public void TwoStaffsFromOneAsset_HaveSeparateCharges()
        {
            ItemCharges.Ensure(states, StaffA, 3);
            ItemCharges.Ensure(states, StaffB, 3);
            ItemAbilityGrant grant = Grant(firebolt);

            Cast(StaffA, grant);
            WaitOutCooldown();
            Cast(StaffA, grant);

            Assert.AreEqual(1, Charges(StaffA));
            Assert.AreEqual(3, Charges(StaffB), "The other staff is untouched.");
        }

        [Test]
        public void AFreeAbility_NeedsNoChargePool()
        {
            ItemCastResult result = Cast(StaffA, Grant(firebolt, cost: 0));

            Assert.AreEqual(ItemCastResult.Cast, result);
            Assert.AreEqual(-1, Charges(StaffA), "There is still no pool.");
        }

        [Test]
        public void AnAbilityThatCostsChargesFromAnItemWithNoPool_IsRefused()
        {
            ItemCastResult result = Cast(StaffA, Grant(firebolt, cost: 1));

            Assert.AreEqual(ItemCastResult.NotEnoughCharges, result);
        }

        // ----- A refused use costs nothing -----

        [Test]
        public void ARefusalForCooldown_SpendsNothing()
        {
            ItemCharges.Ensure(states, StaffA, 3);
            ItemAbilityGrant grant = Grant(firebolt);
            Cast(StaffA, grant);

            ItemCastResult result = Cast(StaffA, grant);

            Assert.AreEqual(ItemCastResult.OnCooldown, result);
            Assert.AreEqual(2, Charges(StaffA), "Only the first cast was charged.");
        }

        [Test]
        public void TwoCastsInTheSameInstant_AreChargedOnce()
        {
            ItemCharges.Ensure(states, StaffA, 3);
            ItemAbilityGrant grant = Grant(firebolt);

            Cast(StaffA, grant);
            Cast(StaffA, grant);
            Cast(StaffA, grant);

            Assert.AreEqual(2, Charges(StaffA));
            Assert.AreEqual(1, projectiles.Launches.Count, "No double consumption.");
        }

        [Test]
        public void ADeadUser_CannotCast_AndSpendsNothing()
        {
            ItemCharges.Ensure(states, StaffA, 3);

            ItemCastResult result = Cast(StaffA, Grant(firebolt), alive: false);

            Assert.AreEqual(ItemCastResult.UserDead, result);
            Assert.AreEqual(3, Charges(StaffA));
        }

        [Test]
        public void ABrokenAbility_IsRefused_AndSpendsNothing()
        {
            AbilityDefinition broken = AbilityBuilder.Empty("Broken");
            created.Add(broken);
            broken.AddNode("simplerpg.nonexistent.v1", null, "mystery");
            ItemCharges.Ensure(states, StaffA, 3);

            ItemCastResult result = Cast(StaffA, Grant(broken));

            Assert.AreEqual(ItemCastResult.AbilityInvalid, result);
            Assert.AreEqual(3, Charges(StaffA));
        }

        [Test]
        public void AnAbilityOnlyAStatusCanRun_IsRefused()
        {
            AbilityDefinition tick = AbilityBuilder.StatusTick();
            created.Add(tick);
            ItemCharges.Ensure(states, StaffA, 3);

            Assert.AreEqual(ItemCastResult.AbilityInvalid, Cast(StaffA, Grant(tick)));
            Assert.AreEqual(3, Charges(StaffA));
        }

        [Test]
        public void NoAbility_IsRefused()
        {
            Assert.AreEqual(ItemCastResult.NoSuchAbility, Cast(StaffA, null));
            Assert.AreEqual(ItemCastResult.NoSuchAbility, Cast(StaffA, new ItemAbilityGrant()));
            Assert.AreEqual(ItemCastResult.NoSuchAbility, Cast(default, Grant(firebolt)));
        }

        // ----- Eligibility -----

        private static AttributeRequirement NeedsIntelligence(float minimum) =>
            new AttributeRequirement { AttributeKey = PrimaryAttributes.Intelligence.Key, Minimum = minimum };

        [Test]
        public void AnIneligibleUser_IsRefused_AndSpendsNothing()
        {
            ItemCharges.Ensure(states, StaffA, 3);

            ItemCastResult result = Cast(StaffA, Grant(firebolt, 1, NeedsIntelligence(12)));

            Assert.AreEqual(ItemCastResult.NotEligible, result);
            Assert.AreEqual(3, Charges(StaffA));
        }

        [Test]
        public void AnEligibleUser_Casts()
        {
            wielderStats.SetBase(Intelligence, 14);
            ItemCharges.Ensure(states, StaffA, 3);

            Assert.AreEqual(ItemCastResult.Cast, Cast(StaffA, Grant(firebolt, 1, NeedsIntelligence(12))));
        }

        [Test]
        public void EligibilityIsLive_AWornItemThatRaisesTheStatMakesTheUserEligible()
        {
            ItemCharges.Ensure(states, StaffA, 3);
            ItemAbilityGrant grant = Grant(firebolt, 1, NeedsIntelligence(12));
            Assert.AreEqual(ItemCastResult.NotEligible, Cast(StaffA, grant));

            ModifierHandle ring = wielderStats.AddModifier(AttributeModifier.Flat(Intelligence, 3, SourceHandle.FromItem(new ItemInstanceId(99))));
            Assert.AreEqual(ItemCastResult.Cast, Cast(StaffA, grant));

            WaitOutCooldown();
            wielderStats.RemoveModifier(ring);
            Assert.AreEqual(ItemCastResult.NotEligible, Cast(StaffA, grant), "Take the ring off and the staff is out of reach again.");
        }

        [Test]
        public void EveryRequirement_MustBeMet()
        {
            wielderStats.SetBase(Intelligence, 14);
            AttributeRequirement strength = new AttributeRequirement { AttributeKey = PrimaryAttributes.Strength.Key, Minimum = 5 };
            ItemCharges.Ensure(states, StaffA, 3);

            Assert.AreEqual(ItemCastResult.NotEligible, Cast(StaffA, Grant(firebolt, 1, NeedsIntelligence(12), strength)), "Strength is 0.");
        }

        [Test]
        public void AUserWithNoAttributes_FailsAnyRequirement_ButNeedsNoneToUseAnUnrestrictedAbility()
        {
            ItemCharges.Ensure(states, StaffA, 3);

            Assert.AreEqual(ItemCastResult.NotEligible, Cast(StaffA, Grant(firebolt, 1, NeedsIntelligence(1)), noAttributes: true));
        }

        // ----- The ability itself -----

        [Test]
        public void TheAbility_RunsWithTheStaffAsItsSource_AndTheWielderAsInstigator()
        {
            ItemCharges.Ensure(states, StaffA, 3);

            Cast(StaffA, Grant(firebolt));

            ProjectileLaunch launch = projectiles.Launches[0].Launch;
            Assert.AreEqual(SourceHandle.FromItem(StaffA), launch.Source);
            Assert.AreEqual(wielder.Id, launch.Instigator);
        }

        [Test]
        public void TheFirebolt_UsesTheStaffsOwnMagicPower_NotTheWielders()
        {
            ItemCharges.Ensure(states, StaffA, 3);
            Cast(StaffA, Grant(firebolt));

            projectiles.Hit(0, target.Id);

            Assert.AreEqual(1000 - 28, target.Health, "12 + 0.8 x 20, the staff's stat, not the wielder's 99.");
        }

        [Test]
        public void TheWielder_NeedsNotKnowTheSpell()
        {
            // The wielder has no abilities of their own and no entry for Firebolt anywhere; the staff alone provides it.
            ItemCharges.Ensure(states, StaffA, 1);

            Assert.AreEqual(ItemCastResult.Cast, Cast(StaffA, Grant(firebolt)));
        }

        [Test]
        public void TheCooldown_IsPerAbilityAndUser_SoTwoStaffsOfTheSameSpellShareIt()
        {
            ItemCharges.Ensure(states, StaffA, 3);
            ItemCharges.Ensure(states, StaffB, 3);
            Cast(StaffA, Grant(firebolt));

            ItemCastResult second = Cast(StaffB, Grant(firebolt));

            Assert.AreEqual(ItemCastResult.OnCooldown, second);
            Assert.AreEqual(3, Charges(StaffB));
        }

        // ----- No recharging -----

        [Test]
        public void EnsuringTheChargesAgain_NeverRefillsAStaff()
        {
            ItemCharges.Ensure(states, StaffA, 3);
            Cast(StaffA, Grant(firebolt));

            bool recreated = ItemCharges.Ensure(states, StaffA, 3);

            Assert.IsFalse(recreated);
            Assert.AreEqual(2, Charges(StaffA), "Equipping, trading or reloading a staff cannot refill it.");
        }

        [Test]
        public void AnotherUserWieldingTheSameStaff_FindsTheSameChargesLeft()
        {
            ItemCharges.Ensure(states, StaffA, 3);
            Cast(StaffA, Grant(firebolt));
            TestCombatant other = Add(5, 100);
            ItemAbilityCaster othersCaster = new ItemAbilityCaster(executor, states, new CooldownTracker(clock));

            ItemCastResult result = othersCaster.Cast(new ItemCastRequest(other.Id, StaffA, Grant(firebolt), wielderStats, default, default), true);

            Assert.AreEqual(ItemCastResult.Cast, result);
            Assert.AreEqual(1, Charges(StaffA), "The staff's charges belong to the staff, not to whoever holds it.");
        }

        [Test]
        public void TheChargeStore_OffersNoWayToAddCharges()
        {
            // Recharging is not built yet. Nothing on the store or the helper can raise a counter, so nothing can do it by accident.
            string[] names = typeof(ItemStateStore).GetMethods().Select(m => m.Name).Concat(typeof(ItemCharges).GetMethods().Select(m => m.Name)).ToArray();

            Assert.IsFalse(names.Any(n => n.Contains("Recharge") || n.Contains("Refill") || n.Contains("Restore") || n.Contains("Add") || n.StartsWith("Set")), string.Join(", ", names));
        }

        [Test]
        public void ChargesNeverGoBelowZero()
        {
            ItemCharges.Ensure(states, StaffA, 1);
            ItemAbilityGrant grant = Grant(firebolt);
            Cast(StaffA, grant);

            for (int i = 0; i < 5; i++)
            {
                WaitOutCooldown();
                Cast(StaffA, grant);
            }

            Assert.AreEqual(0, Charges(StaffA));
            Assert.AreEqual(1, projectiles.Launches.Count);
        }

        [Test]
        public void ThePoolIdentity_IsFixed_SoSavedAndSharedStateKeepsMeaning()
        {
            Assert.AreEqual("5f3a7c1e9b244d6a8e01c7a2b9d4f6e3", ItemCharges.PoolId.ToString());
        }
    }
}
