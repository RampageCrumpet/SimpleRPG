using NUnit.Framework;
using SimpleRPG.Gameplay;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using EntityId = SimpleRPG.Gameplay.EntityId;

namespace SimpleRPG.Abilities.Tests.EditMode
{
    /// <summary>
    /// Runs triggered effects with the real engine, item host, executor, combat pipeline, equipment set and event dispatcher. <see cref="Provider"/>
    /// stands in only for the networked equipment component: it says which effects each worn item brings.
    /// </summary>
    public class TriggeredEffectEngineTests
    {
        private sealed class Provider : IItemEffectProvider
        {
            private readonly Dictionary<ItemInstanceId, IReadOnlyList<TriggeredEffect>> rulesByItem = new Dictionary<ItemInstanceId, IReadOnlyList<TriggeredEffect>>();

            private readonly Dictionary<EntityId, List<ItemInstanceId>> equipped = new Dictionary<EntityId, List<ItemInstanceId>>();

            public int Lookups { get; private set; }

            public void Define(ItemInstanceId item, params TriggeredEffect[] rules) => rulesByItem[item] = rules;

            public void MarkEquipped(EntityId wearer, ItemInstanceId item)
            {
                if (!equipped.TryGetValue(wearer, out List<ItemInstanceId> items)) equipped[wearer] = items = new List<ItemInstanceId>();
                items.Add(item);
            }

            public bool TryGetEffects(EntityId wearer, ItemInstanceId item, out IReadOnlyList<TriggeredEffect> rules)
            {
                Lookups++;
                return rulesByItem.TryGetValue(item, out rules) && rules.Count > 0;
            }

            public IEnumerable<KeyValuePair<EntityId, ItemInstanceId>> EnumerateEquipped() =>
                equipped.SelectMany(p => p.Value.Select(i => new KeyValuePair<EntityId, ItemInstanceId>(p.Key, i)));
        }

        private sealed class CountingRandom : IGameplayRandom
        {
            private readonly IGameplayRandom inner;

            public CountingRandom(IGameplayRandom inner) => this.inner = inner;

            public int Draws { get; private set; }

            public double NextDouble()
            {
                Draws++;
                return inner.NextDouble();
            }
        }

        private static readonly ItemInstanceId Sword = new ItemInstanceId(100);
        private static readonly ItemInstanceId Dagger = new ItemInstanceId(101);
        private static readonly ItemInstanceId Staff = new ItemInstanceId(102);

        private readonly List<Object> created = new List<Object>();

        private GameplayEventDispatcher events;
        private CombatantRegistry combatants;
        private CombatResolver combat;
        private ScriptedProjectiles projectiles;
        private ExecutionServices services;
        private AbilityExecutor executor;
        private Provider provider;
        private CountingRandom random;
        private TriggeredEffectEngine engine;
        private ItemEffectHost itemHost;
        private EquipmentSet equipment;
        private GameObject prefab;
        private AbilityDefinition fireball;
        private List<string> diagnostics;

        private TestCombatant wielder;
        private TestCombatant victim;
        private TestCombatant bystander;

        [SetUp]
        public void SetUp()
        {
            events = new GameplayEventDispatcher();
            combatants = new CombatantRegistry();
            combat = new CombatResolver(events, combatants);
            projectiles = new ScriptedProjectiles();
            random = new CountingRandom(new SeededRandom(11));
            services = new ExecutionServices(new AttributeSetRegistry(), combat, projectiles)
            {
                Targets = new CombatantTargetQuery(combatants),
                Random = random,
            };
            executor = new AbilityExecutor(services);
            provider = new Provider();
            diagnostics = new List<string>();
            services.Diagnostic += diagnostics.Add;
            events.Diagnostic += diagnostics.Add;
            prefab = new GameObject("ProjectilePrefab");
            created.Add(prefab);
            fireball = AbilityBuilder.Fireball(prefab, radius: 3f, damageAmount: 15f);
            created.Add(fireball);

            wielder = Add(1, 0, 0, 0);
            victim = Add(2, 2, 0, 0);
            bystander = Add(3, 3, 0, 0);
            equipment = new EquipmentSet(wielder.Id, events);
            engine = NewEngine();
        }

        [TearDown]
        public void TearDown()
        {
            itemHost?.Dispose();
            engine?.Dispose();
            foreach (Object o in created)
            {
                if (o != null) Object.DestroyImmediate(o);
            }

            created.Clear();
        }

        private TriggeredEffectEngine NewEngine()
        {
            itemHost?.Dispose();
            TriggeredEffectEngine created = new TriggeredEffectEngine(events, executor, random, combatants);
            created.Diagnostic += diagnostics.Add;
            itemHost = new ItemEffectHost(events, created, provider);
            return created;
        }

        private TestCombatant Add(ulong id, float x, float y, float z, int health = 100)
        {
            TestCombatant combatant = new TestCombatant(id, health) { Position = new Position3(x, y, z) };
            combatants.Register(combatant);
            return combatant;
        }

        private TriggeredEffect Rule(float chance = 1f, AbilityDefinition ability = null, EffectScope scope = EffectScope.Owner, params EffectCondition[] conditions)
        {
            TriggeredEffect rule = ScriptableObject.CreateInstance<TriggeredEffect>();
            rule.name = "SwordProc";
            rule.Configure(ability ?? fireball, chance, EffectTrigger.OnHit, scope, EffectTarget.Other, conditions);
            created.Add(rule);
            return rule;
        }

        // A blow as the combat pipeline reports it once its damage is resolved, which is what hit effects react to.
        private static DamageResolvedEvent Blow(TestCombatant attacker, TestCombatant target, SourceHandle source = default, int location = 1,
            DamageProvenance provenance = DamageProvenance.DirectWeaponHit, bool fatal = false, int amount = 10)
        {
            var request = new DamageRequest(target.Id, amount, attacker.Id, source, location, provenance);
            return new DamageResolvedEvent(request, 100, fatal ? 0 : 100 - amount, fatal);
        }

        private void LandBlow(TestCombatant attacker = null, TestCombatant target = null, SourceHandle source = default, int location = 1,
            DamageProvenance provenance = DamageProvenance.DirectWeaponHit, bool fatal = false)
        {
            events.Enqueue(Blow(attacker ?? wielder, target ?? victim, source, location, provenance, fatal));
            events.Drain();
        }

        // ----- Subscribing and unsubscribing -----

        [Test]
        public void EquippingAnItemWithARule_SubscribesIt()
        {
            provider.Define(Sword, Rule());

            equipment.Equip(6, Sword);

            Assert.AreEqual(1, engine.ActiveEffectCount(wielder.Id));
        }

        [Test]
        public void AnItemWithNoRules_SubscribesNothing()
        {
            provider.Define(Dagger);

            equipment.Equip(6, Dagger);

            Assert.AreEqual(0, engine.ActiveEffectCount(wielder.Id));
        }

        [Test]
        public void ABlow_RunsTheRulesAbility_WithTheItemAsSourceAndTheWielderAsInstigator()
        {
            provider.Define(Sword, Rule());
            equipment.Equip(6, Sword);

            LandBlow();

            Assert.AreEqual(1, projectiles.Launches.Count);
            ProjectileLaunch launch = projectiles.Launches[0].Launch;
            Assert.AreEqual(SourceHandle.FromItem(Sword), launch.Source);
            Assert.AreEqual(wielder.Id, launch.Instigator);
        }

        [Test]
        public void TheProc_IsAimedFromTheWielderAtTheVictim()
        {
            provider.Define(Sword, Rule());
            equipment.Equip(6, Sword);

            LandBlow();

            ProjectileLaunch launch = projectiles.Launches[0].Launch;
            Assert.Greater(launch.Origin.X, 0f, "It starts a little in front of the wielder, towards the victim.");
            Assert.AreEqual(1.2f, launch.Origin.Y, 0.001f);
            Assert.Greater(launch.Direction.X, 0f, "It points at the victim.");
        }

        [Test]
        public void TakingTheItemOff_StopsTheRule()
        {
            provider.Define(Sword, Rule());
            equipment.Equip(6, Sword);
            LandBlow();

            equipment.Unequip(6);
            LandBlow();

            Assert.AreEqual(1, projectiles.Launches.Count, "No further proc once the sword is off.");
            Assert.AreEqual(0, engine.ActiveEffectCount(wielder.Id));
        }

        [Test]
        public void PuttingTheItemBackOn_StartsTheRuleAgain()
        {
            provider.Define(Sword, Rule());
            equipment.Equip(6, Sword);
            equipment.Unequip(6);

            equipment.Equip(6, Sword);
            LandBlow();

            Assert.AreEqual(1, projectiles.Launches.Count);
        }

        [Test]
        public void ReplacingTheItem_SwapsTheRules()
        {
            TriggeredEffect swordRule = Rule();
            TriggeredEffect staffRule = Rule();
            provider.Define(Sword, swordRule);
            provider.Define(Staff, staffRule);
            equipment.Equip(6, Sword);

            equipment.Equip(6, Staff);

            Assert.AreEqual(1, engine.ActiveEffectCount(wielder.Id), "Only the staff's rule is active now.");
            LandBlow();
            Assert.AreEqual(1, projectiles.Launches.Count);
            Assert.AreEqual(SourceHandle.FromItem(Staff), projectiles.Launches[0].Launch.Source);
        }

        [Test]
        public void TwoItemsWithRules_BothFire_AndOneComingOffLeavesTheOther()
        {
            provider.Define(Sword, Rule());
            provider.Define(Staff, Rule());
            equipment.Equip(6, Sword);
            equipment.Equip(7, Staff);

            LandBlow();
            Assert.AreEqual(2, projectiles.Launches.Count);

            equipment.Unequip(6);
            LandBlow();
            Assert.AreEqual(3, projectiles.Launches.Count);
            Assert.AreEqual(SourceHandle.FromItem(Staff), projectiles.Launches[2].Launch.Source);
        }

        [Test]
        public void ARuleIsOnlyForItsWearer()
        {
            TestCombatant other = Add(4, 10, 0, 0);
            provider.Define(Sword, Rule());
            equipment.Equip(6, Sword);

            LandBlow(attacker: other);

            Assert.AreEqual(0, projectiles.Launches.Count, "Another character's blows do not trigger the wielder's sword.");
        }

        [Test]
        public void TheSameItemEquippedTwice_IsNotSubscribedTwice()
        {
            provider.Define(Sword, Rule());
            equipment.Equip(6, Sword);

            events.Enqueue(new ItemEquippedEvent(wielder.Id, 6, Sword));
            events.Drain();

            Assert.AreEqual(1, engine.ActiveEffectCount(wielder.Id));
            LandBlow();
            Assert.AreEqual(1, projectiles.Launches.Count);
        }

        [Test]
        public void ARemovedEntity_LosesItsRules()
        {
            provider.Define(Sword, Rule());
            equipment.Equip(6, Sword);

            events.Enqueue(new EntityRemovedEvent(wielder.Id));
            events.Drain();
            LandBlow();

            Assert.AreEqual(0, engine.ActiveEffectCount(wielder.Id));
            Assert.AreEqual(0, projectiles.Launches.Count);
        }

        [Test]
        public void AnEngineCreatedLate_PicksUpEquipmentAlreadyWorn()
        {
            provider.Define(Sword, Rule());
            provider.MarkEquipped(wielder.Id, Sword);
            engine.Dispose();

            engine = NewEngine();

            Assert.AreEqual(1, engine.ActiveEffectCount(wielder.Id));
            LandBlow();
            Assert.AreEqual(1, projectiles.Launches.Count);
        }

        [Test]
        public void ABlow_DoesNotAskTheProviderAnything_OnlyEquippingDoes()
        {
            provider.Define(Sword, Rule());
            equipment.Equip(6, Sword);
            int afterEquip = provider.Lookups;

            for (int i = 0; i < 50; i++) LandBlow();

            Assert.AreEqual(afterEquip, provider.Lookups, "A blow looks only at the wielder's subscribed rules, not at every item.");
        }

        [Test]
        public void ADisposedEngine_StopsListening()
        {
            provider.Define(Sword, Rule());
            equipment.Equip(6, Sword);

            engine.Dispose();
            LandBlow();

            Assert.AreEqual(0, projectiles.Launches.Count);
        }

        // ----- Chance and the seeded stream -----

        [Test]
        public void ACertainRule_NeverRolls_AndAnImpossibleOneNeverFires()
        {
            provider.Define(Sword, Rule(chance: 1f));
            provider.Define(Staff, Rule(chance: 0f));
            equipment.Equip(6, Sword);
            equipment.Equip(7, Staff);

            for (int i = 0; i < 10; i++) LandBlow();

            Assert.AreEqual(10, projectiles.Launches.Count);
            Assert.AreEqual(0, random.Draws, "A rule whose outcome is certain never consumes a random number.");
        }

        [Test]
        public void ARuleWithAChance_RollsOncePerBlow()
        {
            provider.Define(Sword, Rule(chance: 0.1f));
            equipment.Equip(6, Sword);

            for (int i = 0; i < 25; i++) LandBlow();

            Assert.AreEqual(25, random.Draws);
        }

        [Test]
        public void TenPercent_FiresExactlyWhenTheSeededStreamSaysSo()
        {
            provider.Define(Sword, Rule(chance: 0.1f));
            equipment.Equip(6, Sword);

            for (int i = 0; i < 1000; i++) LandBlow();

            // Replay the same seed independently to find how many of the first 1000 rolls come in under 0.1.
            SeededRandom replay = new SeededRandom(11);
            int expected = 0;
            for (int i = 0; i < 1000; i++)
            {
                if (replay.NextDouble() < 0.1) expected++;
            }

            Assert.AreEqual(expected, projectiles.Launches.Count);
            Assert.AreEqual(100, projectiles.Launches.Count, 25, "Roughly one blow in ten.");
        }

        [Test]
        public void TwoRuns_WithTheSameSeed_ProcOnTheSameBlows()
        {
            List<int> Run()
            {
                events = new GameplayEventDispatcher();
                combatants = new CombatantRegistry();
                combat = new CombatResolver(events, combatants);
                projectiles = new ScriptedProjectiles();
                random = new CountingRandom(new SeededRandom(5));
                services = new ExecutionServices(new AttributeSetRegistry(), combat, projectiles) { Targets = new CombatantTargetQuery(combatants), Random = random };
                executor = new AbilityExecutor(services);
                wielder = Add(1, 0, 0, 0);
                victim = Add(2, 2, 0, 0);
                equipment = new EquipmentSet(wielder.Id, events);
                engine.Dispose();
                engine = NewEngine();
                provider.Define(Sword, Rule(chance: 0.3f));
                equipment.Equip(6, Sword);

                List<int> procs = new List<int>();
                for (int i = 0; i < 100; i++)
                {
                    int before = projectiles.Launches.Count;
                    LandBlow();
                    if (projectiles.Launches.Count > before) procs.Add(i);
                }

                return procs;
            }

            CollectionAssert.AreEqual(Run(), Run());
        }

        // ----- Provenance and recursion -----

        [Test]
        public void OnlyAWeaponBlow_IsAHit_NotSpellDamageOrAnythingElse()
        {
            provider.Define(Sword, Rule());
            equipment.Equip(6, Sword);

            LandBlow(provenance: DamageProvenance.Spell);
            LandBlow(provenance: DamageProvenance.Proc);
            LandBlow(provenance: DamageProvenance.Periodic);

            Assert.AreEqual(0, projectiles.Launches.Count);
        }

        [Test]
        public void TheProcsExplosion_DealsSpellDamage_AndNeverCountsAsAWeaponHit()
        {
            provider.Define(Sword, Rule());
            equipment.Equip(6, Sword);
            List<DamageProvenance> damage = new List<DamageProvenance>();
            events.Subscribe<DamageResolvedEvent>(e => damage.Add(e.Request.Provenance));

            LandBlow();
            projectiles.Hit(0, victim.Id, at: new Position3(2, 0, 0));

            Assert.AreEqual(1, damage.Count(p => p == DamageProvenance.DirectWeaponHit), "Only the original blow is a weapon hit; the explosion is not.");
            Assert.IsNotEmpty(damage.Where(p => p != DamageProvenance.DirectWeaponHit));
            Assert.IsTrue(damage.Where(p => p != DamageProvenance.DirectWeaponHit).All(p => p == DamageProvenance.Spell));
            Assert.AreEqual(1, projectiles.Launches.Count, "The explosion did not trigger the sword again.");
        }

        [Test]
        public void TheExplosion_HitsTheVictimAndNeighbours_ButNotTheWielder()
        {
            provider.Define(Sword, Rule());
            equipment.Equip(6, Sword);

            LandBlow();
            projectiles.Hit(0, victim.Id, at: new Position3(2, 0, 0));

            Assert.AreEqual(85, victim.Health);
            Assert.AreEqual(85, bystander.Health);
            Assert.AreEqual(100, wielder.Health);
        }

        [Test]
        public void TheExplosionsDamage_IsCreditedToTheWielder_AndTheKillToo()
        {
            victim = Add(9, 2, 0, 0, health: 10);
            provider.Define(Sword, Rule());
            equipment.Equip(6, Sword);
            DeathContext death = null;
            events.Subscribe<EntityDiedEvent>(e => death = e.Context);

            LandBlow(target: victim);
            projectiles.Hit(0, victim.Id, at: new Position3(2, 0, 0));

            Assert.IsNotNull(death);
            Assert.AreEqual(wielder.Id, death.Killer);
            Assert.AreEqual(SourceHandle.FromItem(Sword), death.KillingSource);
            Assert.AreEqual(DamageProvenance.Spell, death.KillingProvenance);
        }

        [Test]
        public void ARuleWhoseAbilitySwingsAgain_CannotLoopForever()
        {
            // The worst case: the proc's own ability is a melee swing, whose hit confirms another weapon hit, which triggers the rule again.
            ScriptedMelee melee = new ScriptedMelee();
            services = new ExecutionServices(new AttributeSetRegistry(), combat, projectiles, melee) { Targets = new CombatantTargetQuery(combatants), Random = random };
            executor = new AbilityExecutor(services);
            services.Diagnostic += diagnostics.Add;
            engine.Dispose();
            engine = NewEngine();
            // The swing's hit goes through the real combat pipeline, so its damage is itself the next weapon hit; queuing another here would double every level.
            melee.OnBegin = (launch, swingEvents) => swingEvents.Hit(launch.Token, new MeleeHit(victim.Id, 1, 1, 1f, 1f));

            AbilityDefinition swing = AbilityBuilder.MeleeAttack();
            created.Add(swing);
            provider.Define(Sword, Rule(ability: swing));
            equipment.Equip(6, Sword);

            LandBlow();

            Assert.LessOrEqual(melee.Swings.Count, events.MaxDepth + 1, "The cause chain is cut off at the dispatcher's depth limit.");
            Assert.IsTrue(diagnostics.Any(d => d.Contains("deep")), string.Join("\n", diagnostics));
            Assert.AreEqual(0, events.PendingCount);
        }

        // ----- Broken rules -----

        [Test]
        public void ARuleWithNoAbility_IsReportedOnce_NotThrown()
        {
            TriggeredEffect empty = ScriptableObject.CreateInstance<TriggeredEffect>();
            created.Add(empty);
            provider.Define(Sword, empty);
            equipment.Equip(6, Sword);

            Assert.DoesNotThrow(() =>
            {
                LandBlow();
                LandBlow();
            });

            Assert.AreEqual(1, diagnostics.Count(d => d.Contains("no ability")), "A broken rule reports once, not on every blow.");
        }

        [Test]
        public void ARuleWhoseAbilityHasErrors_IsReported_AndNothingRuns()
        {
            AbilityDefinition broken = AbilityBuilder.Empty("Broken");
            created.Add(broken);
            broken.AddNode("simplerpg.nonexistent.v1", null, "mystery");
            provider.Define(Sword, Rule(ability: broken));
            equipment.Equip(6, Sword);

            LandBlow();

            Assert.AreEqual(0, projectiles.Launches.Count);
            Assert.IsTrue(diagnostics.Any(d => d.Contains("has errors")), string.Join("\n", diagnostics));
        }

        [Test]
        public void ANullRuleInTheList_IsSkipped()
        {
            provider.Define(Sword, new TriggeredEffect[] { null, Rule() });
            equipment.Equip(6, Sword);

            LandBlow();

            Assert.AreEqual(1, projectiles.Launches.Count);
        }

        // ----- Triggers, conditions, scope, targets, hosts -----

        // A condition on the body location, as the game's own BodyLocationCondition does (that one lives in the integration assembly).
        private sealed class LocationIs : EffectCondition
        {
            private readonly int location;

            public LocationIs(int location) => this.location = location;

            public override bool Passes(in TriggerContext context) => context.BodyLocation == location;
        }

        private const int Head = 0;
        private const int Arms = 3;

        // An ability a triggered effect runs: it damages whoever the effect is aimed at by 7.
        private AbilityDefinition StrikeTarget()
        {
            AbilityDefinition ability = AbilityBuilder.Empty("StrikeTarget");
            ability.RemoveNode("activate");
            string entry = ability.AddNode(NodeTypes.TriggerEntry, null, "entry");
            string target = ability.AddNode(NodeTypes.TriggerTarget, null, "target");
            string amount = ability.AddNode(NodeTypes.Constant, new ConstantConfig { Value = 7f }, "amount");
            string damage = ability.AddNode(NodeTypes.Damage, new DamageConfig { Provenance = DamageProvenance.Proc }, "damage");
            ability.Connect(entry, BuiltInNodes.Next, damage, BuiltInNodes.In);
            ability.Connect(target, BuiltInNodes.Value, damage, BuiltInNodes.Target);
            ability.Connect(amount, BuiltInNodes.Value, damage, BuiltInNodes.Amount);
            created.Add(ability);
            return ability;
        }

        private TriggeredEffect Effect(EffectTrigger trigger, EffectTarget target, EffectScope scope = EffectScope.Owner, params EffectCondition[] conditions)
        {
            TriggeredEffect effect = ScriptableObject.CreateInstance<TriggeredEffect>();
            effect.name = "Effect";
            effect.Configure(StrikeTarget(), 1f, trigger, scope, target, conditions);
            created.Add(effect);
            return effect;
        }

        [Test]
        public void TheAbility_IsAimedAtTheOtherPartyOrTheOwner_AsTheEffectSays()
        {
            provider.Define(Sword, Effect(EffectTrigger.OnHit, EffectTarget.Other));
            provider.Define(Staff, Effect(EffectTrigger.OnHit, EffectTarget.Owner));
            equipment.Equip(6, Sword);
            equipment.Equip(7, Staff);

            LandBlow();

            Assert.AreEqual(93, victim.Health, "The sword's effect struck the one that was hit.");
            Assert.AreEqual(93, wielder.Health, "The staff's effect struck its owner.");
            Assert.IsEmpty(diagnostics);
        }

        [Test]
        public void SeveralConditions_MustAllHold_AndEachEffectDecidesForItself()
        {
            // A great deal extra on a killing blow to the head; a penalty to the wielder on a blow to the arm. Two separate effects.
            provider.Define(Sword, Effect(EffectTrigger.OnHit, EffectTarget.Other, EffectScope.Owner, new FatalCondition(true), new LocationIs(Head)),
                Effect(EffectTrigger.OnHit, EffectTarget.Owner, EffectScope.Owner, new LocationIs(Arms)));
            equipment.Equip(6, Sword);

            LandBlow(location: Head, fatal: false);
            Assert.AreEqual(100, victim.Health, "A head blow that does not kill is not enough.");

            LandBlow(location: 1, fatal: true);
            Assert.AreEqual(100, victim.Health, "A killing blow to the torso is not enough.");

            LandBlow(location: Head, fatal: true);
            Assert.AreEqual(93, victim.Health);
            Assert.AreEqual(100, wielder.Health);

            LandBlow(location: Arms, fatal: false);
            Assert.AreEqual(93, wielder.Health, "The limb effect costs the wielder.");
            Assert.AreEqual(93, victim.Health);
        }

        [Test]
        public void AHostScopedEffect_ReactsOnlyToItsOwnHostsBlows_AnOwnerScopedOneToAny()
        {
            provider.Define(Sword, Effect(EffectTrigger.OnHit, EffectTarget.Other, EffectScope.Host));
            provider.Define(Staff, Effect(EffectTrigger.OnHit, EffectTarget.Owner, EffectScope.Owner));
            equipment.Equip(6, Sword);
            equipment.Equip(7, Staff);

            LandBlow(source: SourceHandle.FromItem(Staff));
            Assert.AreEqual(100, victim.Health, "The sword did not make this blow.");
            Assert.AreEqual(93, wielder.Health, "The staff's owner-scoped effect reacts to any blow.");

            LandBlow(source: SourceHandle.FromItem(Sword));
            Assert.AreEqual(93, victim.Health, "The sword made this one.");
        }

        [Test]
        public void OnTakeDamage_ReactsForTheOneStruck_WithTheAttackerAsTheOtherParty()
        {
            provider.Define(Sword, Effect(EffectTrigger.OnTakeDamage, EffectTarget.Other));
            equipment = new EquipmentSet(victim.Id, events);
            equipment.Equip(6, Sword);

            LandBlow();

            Assert.AreEqual(93, wielder.Health, "The attacker, as the other party, was struck back.");
        }

        [Test]
        public void OnKill_ReactsForTheKiller_ByAnyMeans()
        {
            provider.Define(Sword, Effect(EffectTrigger.OnKill, EffectTarget.Owner));
            equipment.Equip(6, Sword);

            combat.Apply(new DamageRequest(victim.Id, 1000, wielder.Id, provenance: DamageProvenance.Spell));

            Assert.IsFalse(victim.IsAlive);
            Assert.AreEqual(93, wielder.Health);
        }

        [Test]
        public void AnyHostCanBringEffects_AndTakingThemAwayStopsThem()
        {
            SourceHandle spell = SourceHandle.FromStatus(new StatusInstanceId(77));
            engine.Activate(wielder.Id, spell, new[] { Effect(EffectTrigger.OnHit, EffectTarget.Other) });
            Assert.AreEqual(1, engine.ActiveEffectCount(wielder.Id));
            LandBlow();
            Assert.AreEqual(93, victim.Health);

            engine.Deactivate(wielder.Id, spell);
            LandBlow();

            Assert.AreEqual(0, engine.ActiveEffectCount(wielder.Id));
            Assert.AreEqual(93, victim.Health);
        }

        [Test]
        public void TwoHostsOfTheSameEffect_EachHaveTheirOwn_NotShared()
        {
            TriggeredEffect effect = Effect(EffectTrigger.OnHit, EffectTarget.Other);
            SourceHandle first = SourceHandle.FromStatus(new StatusInstanceId(1));
            SourceHandle second = SourceHandle.FromStatus(new StatusInstanceId(2));
            engine.Activate(wielder.Id, first, new[] { effect });
            engine.Activate(wielder.Id, second, new[] { effect });

            LandBlow();
            Assert.AreEqual(86, victim.Health, "Each host's copy ran.");

            engine.Deactivate(wielder.Id, first);
            LandBlow();
            Assert.AreEqual(79, victim.Health, "Only the second host's remains.");
        }

        private sealed class StatusProvider : IStatusEffectProvider
        {
            public IReadOnlyList<TriggeredEffect> Effects;

            public bool TryGetEffects(DefinitionId status, out IReadOnlyList<TriggeredEffect> effects)
            {
                effects = Effects;
                return Effects != null;
            }
        }

        [Test]
        public void AStatusBringsItsEffectsWhileItLasts_AsASpellsLastingEffect()
        {
            var statuses = new StatusProvider { Effects = new[] { Effect(EffectTrigger.OnHit, EffectTarget.Other) } };
            using (new StatusEffectHost(events, engine, statuses))
            {
                DefinitionId rage = DefinitionId.NewId();
                StatusInstanceId group = new StatusInstanceId(5);

                events.Enqueue(new StatusAppliedEvent(wielder.Id, rage, group, default, wielder.Id));
                events.Drain();
                LandBlow();
                Assert.AreEqual(93, victim.Health, "While the status is on the holder.");

                events.Enqueue(new StatusRemovedEvent(wielder.Id, rage, group, RemovalReason.Expired));
                events.Drain();
                LandBlow();
                Assert.AreEqual(93, victim.Health, "Gone with the status.");
                Assert.AreEqual(0, engine.ActiveEffectCount(wielder.Id));
            }
        }
    }
}
