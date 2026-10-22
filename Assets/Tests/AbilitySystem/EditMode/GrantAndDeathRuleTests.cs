using NUnit.Framework;
using SimpleRPG.Gameplay;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using EntityId = SimpleRPG.Gameplay.EntityId;

namespace SimpleRPG.Abilities.Tests.EditMode
{
    /// <summary>
    /// Worn items that change the wearer's attributes, with the real grant engine, attribute sets, equipment and event dispatcher.
    /// </summary>
    public class GrantEngineTests
    {
        private sealed class Provider : IItemGrantProvider
        {
            private readonly Dictionary<ItemInstanceId, IReadOnlyList<GrantSpec>> grantsByItem = new Dictionary<ItemInstanceId, IReadOnlyList<GrantSpec>>();

            private readonly List<KeyValuePair<EntityId, ItemInstanceId>> equipped = new List<KeyValuePair<EntityId, ItemInstanceId>>();

            public void Define(ItemInstanceId item, params GrantSpec[] grants) => grantsByItem[item] = grants;

            public void MarkEquipped(EntityId wearer, ItemInstanceId item) => equipped.Add(new KeyValuePair<EntityId, ItemInstanceId>(wearer, item));

            public bool TryGetGrants(EntityId wearer, ItemInstanceId item, out IReadOnlyList<GrantSpec> grants) =>
                grantsByItem.TryGetValue(item, out grants) && grants.Count > 0;

            private readonly Dictionary<ItemInstanceId, IReadOnlyList<AttributeValueSpec>> fixedByItem = new Dictionary<ItemInstanceId, IReadOnlyList<AttributeValueSpec>>();

            public void DefineFixed(ItemInstanceId item, params AttributeValueSpec[] values) => fixedByItem[item] = values;

            public bool TryGetFixedAttributes(EntityId wearer, ItemInstanceId item, out IReadOnlyList<AttributeValueSpec> fixedAttributes) =>
                fixedByItem.TryGetValue(item, out fixedAttributes) && fixedAttributes.Count > 0;

            public IEnumerable<KeyValuePair<EntityId, ItemInstanceId>> EnumerateEquipped() => equipped;
        }

        private static readonly ItemInstanceId Amulet = new ItemInstanceId(200);
        private static readonly ItemInstanceId Ring = new ItemInstanceId(201);
        private static readonly EntityId Wearer = new EntityId(1);
        private static readonly AttributeId Strength = PrimaryAttributes.Strength;
        private static readonly AttributeId Constitution = PrimaryAttributes.Constitution;

        private GameplayEventDispatcher events;
        private AttributeSetRegistry attributes;
        private AttributeSet stats;
        private Provider provider;
        private GrantEngine engine;
        private EquipmentSet equipment;
        private List<string> diagnostics;

        [SetUp]
        public void SetUp()
        {
            events = new GameplayEventDispatcher();
            attributes = new AttributeSetRegistry();
            stats = attributes.GetOrCreate(SourceHandle.FromEntity(Wearer));
            stats.SetBase(Strength, 10);
            stats.SetBase(Constitution, 10);
            provider = new Provider();
            diagnostics = new List<string>();
            engine = NewEngine();
            equipment = new EquipmentSet(Wearer, events);
        }

        [TearDown]
        public void TearDown()
        {
            engine?.Dispose();
        }

        private GrantEngine NewEngine()
        {
            GrantEngine created = new GrantEngine(events, attributes, provider);
            created.Diagnostic += diagnostics.Add;
            return created;
        }

        private static GrantSpec Grant(string key, float value, ModifierLayer layer = ModifierLayer.Add) =>
            new GrantSpec { AttributeKey = key, Value = value, Layer = layer };

        [Test]
        public void WearingTheAmulet_RaisesTheStats()
        {
            provider.Define(Amulet, Grant(Strength.Key, 3), Grant(Constitution.Key, 2));

            equipment.Equip(0, Amulet);

            Assert.AreEqual(13d, stats.Get(Strength));
            Assert.AreEqual(12d, stats.Get(Constitution));
            Assert.AreEqual(2, engine.ActiveGrantCount(Wearer));
        }

        [Test]
        public void TakingTheAmuletOff_RestoresTheStats_ExactlyAndForever()
        {
            provider.Define(Amulet, Grant(Strength.Key, 3, ModifierLayer.Add), Grant(Strength.Key, 0.1f, ModifierLayer.PercentAdd));

            for (int i = 0; i < 100; i++)
            {
                equipment.Equip(0, Amulet);
                equipment.Unequip(0);
            }

            Assert.AreEqual(10d, stats.Get(Strength), "Equip and unequip cycles must leave no drift.");
            Assert.AreEqual(0, stats.ModifierCount);
            Assert.AreEqual(0, engine.ActiveGrantCount(Wearer));
        }

        [Test]
        public void TwoItemsGrantingTheSameStat_AreIndependent()
        {
            provider.Define(Amulet, Grant(Strength.Key, 3));
            provider.Define(Ring, Grant(Strength.Key, 3));
            equipment.Equip(0, Amulet);
            equipment.Equip(1, Ring);
            Assert.AreEqual(16d, stats.Get(Strength));

            equipment.Unequip(0);

            Assert.AreEqual(13d, stats.Get(Strength), "Taking one off leaves the other's identical bonus.");
        }

        [Test]
        public void TakingAnItemOff_DoesNotRemoveABonusSomethingElseAdded()
        {
            provider.Define(Amulet, Grant(Strength.Key, 3));
            stats.AddModifier(AttributeModifier.Flat(Strength, 5, SourceHandle.FromWorldObject(9)));
            equipment.Equip(0, Amulet);

            equipment.Unequip(0);

            Assert.AreEqual(15d, stats.Get(Strength), "The unrelated +5 stays.");
        }

        [Test]
        public void TheLayers_ApplyInOrder()
        {
            provider.Define(Amulet, Grant(Strength.Key, 5, ModifierLayer.Add), Grant(Strength.Key, 0.2f, ModifierLayer.PercentAdd), Grant(Strength.Key, 2f, ModifierLayer.Multiply));

            equipment.Equip(0, Amulet);

            Assert.AreEqual((10 + 5) * 1.2 * 2, stats.Get(Strength), 1e-6);
        }

        [Test]
        public void ReplacingTheItem_SwapsTheGrants()
        {
            provider.Define(Amulet, Grant(Strength.Key, 3));
            provider.Define(Ring, Grant(Constitution.Key, 4));
            equipment.Equip(0, Amulet);

            equipment.Equip(0, Ring);

            Assert.AreEqual(10d, stats.Get(Strength));
            Assert.AreEqual(14d, stats.Get(Constitution));
        }

        [Test]
        public void GrantsAreLive_ALaterChangeToTheBaseStatShowsThrough()
        {
            provider.Define(Amulet, Grant(Strength.Key, 3));
            equipment.Equip(0, Amulet);

            stats.SetBase(Strength, 20);

            Assert.AreEqual(23d, stats.Get(Strength));
        }

        [Test]
        public void ADerivedStat_FollowsTheGrant()
        {
            AttributeId health = new AttributeId("test.attr.maxhealth");
            AttributeSchema schema = new AttributeSchema().Define(health, a => 50 + 10 * a.Get(Constitution));
            AttributeSet withDerived = new AttributeSet(schema);
            withDerived.SetBase(Constitution, 10);
            attributes.Register(SourceHandle.FromEntity(Wearer), withDerived);
            stats = withDerived;
            provider.Define(Amulet, Grant(Constitution.Key, 2));

            equipment.Equip(0, Amulet);
            Assert.AreEqual(170d, withDerived.Get(health));

            equipment.Unequip(0);
            Assert.AreEqual(150d, withDerived.Get(health));
        }

        [Test]
        public void AnItemsOwnAttributes_BelongToTheItem_WhileItIsWorn()
        {
            provider.DefineFixed(Amulet, new AttributeValueSpec { AttributeKey = "test.attr.magicpower", Value = 20 });

            equipment.Equip(0, Amulet);

            Assert.IsTrue(attributes.TryGetSet(SourceHandle.FromItem(Amulet), out AttributeSet itemStats));
            Assert.AreEqual(20d, itemStats.Get(new AttributeId("test.attr.magicpower")));
            Assert.AreEqual(0d, stats.Get(new AttributeId("test.attr.magicpower")), "They are the item's, not the wearer's.");

            equipment.Unequip(0);
            Assert.IsFalse(attributes.TryGetSet(SourceHandle.FromItem(Amulet), out _));
        }

        [Test]
        public void AnItemsInvalidOwnAttribute_IsSkippedAndReported()
        {
            provider.DefineFixed(Amulet, new AttributeValueSpec { AttributeKey = "", Value = 1 }, new AttributeValueSpec { AttributeKey = "test.attr.ok", Value = 2 });

            equipment.Equip(0, Amulet);

            Assert.IsTrue(diagnostics.Any(d => d.Contains("Skipped")));
            attributes.TryGetSet(SourceHandle.FromItem(Amulet), out AttributeSet itemStats);
            Assert.AreEqual(2d, itemStats.Get(new AttributeId("test.attr.ok")));
        }

        [Test]
        public void AnItemWithNoGrants_ChangesNothing()
        {
            provider.Define(Ring);

            equipment.Equip(0, Ring);

            Assert.AreEqual(0, stats.ModifierCount);
        }

        [TestCase("")]
        [TestCase("   ")]
        public void AGrantWithNoAttribute_IsSkippedAndReported(string key)
        {
            provider.Define(Amulet, Grant(key, 3), Grant(Strength.Key, 1));

            equipment.Equip(0, Amulet);

            Assert.AreEqual(11d, stats.Get(Strength), "The valid grant still applies.");
            Assert.IsTrue(diagnostics.Any(d => d.Contains("Skipped")));
        }

        [Test]
        public void AGrantWithAnInfiniteValue_IsSkippedAndReported()
        {
            provider.Define(Amulet, Grant(Strength.Key, float.PositiveInfinity));

            equipment.Equip(0, Amulet);

            Assert.AreEqual(10d, stats.Get(Strength));
            Assert.IsTrue(diagnostics.Any(d => d.Contains("Skipped")));
        }

        [Test]
        public void ARemovedWearer_LetsGoOfItsGrantRecords()
        {
            provider.Define(Amulet, Grant(Strength.Key, 3));
            equipment.Equip(0, Amulet);

            events.Enqueue(new EntityRemovedEvent(Wearer));
            events.Drain();

            Assert.AreEqual(0, engine.ActiveGrantCount(Wearer));
        }

        [Test]
        public void AnEngineCreatedLate_AppliesGrantsOfItemsAlreadyWorn()
        {
            provider.Define(Amulet, Grant(Strength.Key, 3));
            provider.MarkEquipped(Wearer, Amulet);

            engine.Dispose();
            engine = NewEngine();

            Assert.AreEqual(13d, stats.Get(Strength));
        }

        [Test]
        public void TheSameEquipEventTwice_GrantsOnce()
        {
            provider.Define(Amulet, Grant(Strength.Key, 3));
            equipment.Equip(0, Amulet);

            events.Enqueue(new ItemEquippedEvent(Wearer, 0, Amulet));
            events.Drain();

            Assert.AreEqual(13d, stats.Get(Strength));
        }

        [Test]
        public void AWearerWithNoAttributesYet_GetsASetForTheGrant()
        {
            EntityId stranger = new EntityId(77);
            EquipmentSet strangerEquipment = new EquipmentSet(stranger, events);
            provider.Define(Amulet, Grant(Strength.Key, 3));

            strangerEquipment.Equip(0, Amulet);

            Assert.IsTrue(attributes.TryGetSet(SourceHandle.FromEntity(stranger), out AttributeSet set));
            Assert.AreEqual(3d, set.Get(Strength));
        }
    }

    /// <summary>
    /// Spawning and death rules, with the real rule engine, executor, combat pipeline, equipment and event dispatcher. The cursed amulet:
    /// when its wearer dies it spawns one creature where they died.
    /// </summary>
    public class DeathRuleTests
    {
        private sealed class Provider : IItemRuleProvider
        {
            private readonly Dictionary<ItemInstanceId, IReadOnlyList<GameplayRuleDefinition>> rulesByItem = new Dictionary<ItemInstanceId, IReadOnlyList<GameplayRuleDefinition>>();

            public void Define(ItemInstanceId item, params GameplayRuleDefinition[] rules) => rulesByItem[item] = rules;

            public bool TryGetRules(EntityId wearer, ItemInstanceId item, out IReadOnlyList<GameplayRuleDefinition> rules) =>
                rulesByItem.TryGetValue(item, out rules) && rules.Count > 0;

            public IEnumerable<KeyValuePair<EntityId, ItemInstanceId>> EnumerateEquipped() => new KeyValuePair<EntityId, ItemInstanceId>[0];
        }

        private static readonly ItemInstanceId Amulet = new ItemInstanceId(300);
        private static readonly ItemInstanceId Sword = new ItemInstanceId(301);

        private readonly List<Object> created = new List<Object>();

        private GameplayEventDispatcher events;
        private CombatantRegistry combatants;
        private CombatResolver combat;
        private ScriptedSpawner spawner;
        private ExecutionServices services;
        private AbilityExecutor executor;
        private Provider provider;
        private RuleEngine engine;
        private EquipmentSet equipment;
        private GameObject demonPrefab;
        private AbilityDefinition summon;
        private List<string> diagnostics;

        private TestCombatant wearer;
        private TestCombatant killer;

        [SetUp]
        public void SetUp()
        {
            events = new GameplayEventDispatcher();
            combatants = new CombatantRegistry();
            combat = new CombatResolver(events, combatants);
            spawner = new ScriptedSpawner();
            services = new ExecutionServices(new AttributeSetRegistry(), combat, null) { Spawner = spawner, Random = new SeededRandom(5) };
            executor = new AbilityExecutor(services);
            provider = new Provider();
            diagnostics = new List<string>();
            services.Diagnostic += diagnostics.Add;
            events.Diagnostic += diagnostics.Add;
            demonPrefab = new GameObject("DemonPrefab");
            created.Add(demonPrefab);
            summon = AbilityBuilder.Summon(demonPrefab);
            created.Add(summon);

            wearer = Add(1, 100, new Position3(7, 0, 9));
            killer = Add(2, 100, new Position3(0, 0, 0));
            equipment = new EquipmentSet(wearer.Id, events);
            engine = new RuleEngine(events, executor, services.Random, combatants, provider);
            engine.Diagnostic += diagnostics.Add;
            combat.DeathSources = engine;
        }

        [TearDown]
        public void TearDown()
        {
            engine?.Dispose();
            foreach (Object o in created)
            {
                if (o != null) Object.DestroyImmediate(o);
            }

            created.Clear();
        }

        private TestCombatant Add(ulong id, int health, Position3 position)
        {
            TestCombatant combatant = new TestCombatant(id, health) { Position = position };
            combatants.Register(combatant);
            return combatant;
        }

        private GameplayRuleDefinition DeathRule(float chance = 1f, AbilityDefinition ability = null)
        {
            GameplayRuleDefinition rule = ScriptableObject.CreateInstance<GameplayRuleDefinition>();
            rule.name = "CursedAmulet";
            rule.Configure(ability ?? summon, chance, RuleTrigger.OwnerDeath);
            created.Add(rule);
            return rule;
        }

        private void Kill(TestCombatant victim = null, TestCombatant by = null)
        {
            victim ??= wearer;
            combat.Apply(new DamageRequest(victim.Id, 10000, by != null ? by.Id : default, provenance: DamageProvenance.DirectWeaponHit));
        }

        [Test]
        public void TheWearersDeath_SpawnsOneCreature_WhereTheyDied()
        {
            provider.Define(Amulet, DeathRule());
            equipment.Equip(2, Amulet);

            Kill(by: killer);

            Assert.AreEqual(1, spawner.Spawns.Count);
            SpawnRequest spawn = spawner.Spawns[0];
            Assert.AreEqual(7f, spawn.Position.X);
            Assert.AreEqual(9f, spawn.Position.Z);
            Assert.AreSame(demonPrefab, spawn.Prefab);
        }

        [Test]
        public void TheCreature_IsSourcedFromTheAmulet_AndTheDeadWearerIsCreditedAsSpawner()
        {
            provider.Define(Amulet, DeathRule());
            equipment.Equip(2, Amulet);

            Kill(by: killer);

            Assert.AreEqual(SourceHandle.FromItem(Amulet), spawner.Spawns[0].Source);
            Assert.AreEqual(wearer.Id, spawner.Spawns[0].Instigator);
        }

        [Test]
        public void WithoutTheAmulet_NothingIsSpawned()
        {
            provider.Define(Amulet, DeathRule());

            Kill();

            Assert.AreEqual(0, spawner.Spawns.Count);
        }

        [Test]
        public void Damage_ToTheCorpse_SpawnsNothingMore()
        {
            provider.Define(Amulet, DeathRule());
            equipment.Equip(2, Amulet);
            Kill();

            for (int i = 0; i < 10; i++)
            {
                combat.Apply(new DamageRequest(wearer.Id, 50));
                events.Drain();
            }

            Assert.AreEqual(1, spawner.Spawns.Count, "Hitting a corpse, or looking at its zero health, is not another death.");
        }

        [Test]
        public void AnnouncingTheSameDeathTwice_StillSpawnsOnce()
        {
            provider.Define(Amulet, DeathRule());
            equipment.Equip(2, Amulet);
            DeathContext context = null;
            events.Subscribe<EntityDiedEvent>(e => context = e.Context);
            Kill();

            events.Enqueue(new EntityDiedEvent(context));
            events.Drain();

            Assert.AreEqual(1, spawner.Spawns.Count);
        }

        [Test]
        public void AfterARevival_ASecondDeathSpawnsAgain()
        {
            provider.Define(Amulet, DeathRule());
            equipment.Equip(2, Amulet);
            Kill();

            combat.Revive(wearer.Id, 50);
            wearer.Position = new Position3(1, 0, 1);
            Kill();

            Assert.AreEqual(2, spawner.Spawns.Count);
            Assert.AreEqual(1f, spawner.Spawns[1].Position.X, "The second creature appears where the second death happened.");
        }

        [Test]
        public void TheReactionStillRuns_EvenIfTheAmuletComesOffDuringTheDeath()
        {
            provider.Define(Amulet, DeathRule());
            equipment.Equip(2, Amulet);

            // Inside one event the amulet is taken off and, in the same breath, the wearer takes a fatal blow. The removal is queued first, so the
            // engine forgets the amulet before the death is announced; the curse still stands because the amulet qualified when the death was committed.
            events.Subscribe<WeaponHitConfirmedEvent>(e =>
            {
                equipment.Unequip(2);
                Kill();
            });

            events.Enqueue(new WeaponHitConfirmedEvent(killer.Id, wearer.Id, 1, 1, default));
            events.Drain();

            Assert.AreEqual(0, engine.ActiveRuleCount(wearer.Id), "The amulet is unsubscribed.");
            Assert.AreEqual(1, spawner.Spawns.Count, "Yet its curse still ran.");
        }

        [Test]
        public void TheDeathContext_ListsTheAmuletAsAQualifyingSource()
        {
            provider.Define(Amulet, DeathRule());
            equipment.Equip(2, Amulet);
            DeathContext context = null;
            events.Subscribe<EntityDiedEvent>(e => context = e.Context);

            Kill();

            CollectionAssert.Contains(context.QualifyingSources, SourceHandle.FromItem(Amulet));
        }

        [Test]
        public void ADeathRule_DoesNotFireOnAWeaponHit_AndAWeaponRuleDoesNotFireOnDeath()
        {
            GameplayRuleDefinition weaponRule = ScriptableObject.CreateInstance<GameplayRuleDefinition>();
            created.Add(weaponRule);
            weaponRule.Configure(summon, 1f, RuleTrigger.WeaponHitConfirmed);
            provider.Define(Amulet, DeathRule());
            provider.Define(Sword, weaponRule);
            equipment.Equip(2, Amulet);
            equipment.Equip(6, Sword);

            events.Enqueue(new WeaponHitConfirmedEvent(wearer.Id, killer.Id, 1, 1, default));
            events.Drain();
            Assert.AreEqual(1, spawner.Spawns.Count, "Only the sword's rule answers a blow.");

            spawner.Spawns.Clear();
            Kill();
            Assert.AreEqual(1, spawner.Spawns.Count, "Only the amulet's rule answers a death.");
            Assert.AreEqual(SourceHandle.FromItem(Amulet), spawner.Spawns[0].Source);
        }

        [Test]
        public void ADeathRuleWithAChance_UsesTheSeededStream()
        {
            provider.Define(Amulet, DeathRule(chance: 0.5f));
            equipment.Equip(2, Amulet);
            SeededRandom replay = new SeededRandom(5);
            int expected = 0;
            for (int life = 0; life < 20; life++)
            {
                if (replay.NextDouble() < 0.5) expected++;
            }

            for (int life = 0; life < 20; life++)
            {
                Kill();
                combat.Revive(wearer.Id, 10);
            }

            Assert.AreEqual(expected, spawner.Spawns.Count);
        }

        [Test]
        public void ADeathRule_AttachedToAnItemWithTwoRules_RunsBoth()
        {
            provider.Define(Amulet, DeathRule(), DeathRule());
            equipment.Equip(2, Amulet);

            Kill();

            Assert.AreEqual(2, spawner.Spawns.Count);
        }

        [Test]
        public void TheSpawnedCreatures_AreNotKilledByTheAmuletComingOff_BecauseNothingTiesThemToIt()
        {
            provider.Define(Amulet, DeathRule());
            equipment.Equip(2, Amulet);
            Kill();

            equipment.Unequip(2);

            Assert.AreEqual(1, spawner.Spawns.Count);
            Assert.IsEmpty(diagnostics);
        }

        // ----- The spawn node -----

        [Test]
        public void SpawnEntity_Compiles_AndIsInThePalette()
        {
            Assert.IsTrue(summon.Compile().Success, summon.Compile().Report.ToString());
            Assert.IsTrue(AbilityNodeRegistry.Default.Search("spawn").Any(d => d.TypeId == NodeTypes.SpawnEntity));
        }

        [Test]
        public void SpawnEntity_WithNoPrefab_IsRejected()
        {
            AbilityDefinition broken = AbilityBuilder.Summon(null);
            created.Add(broken);

            Assert.IsTrue(broken.Compile().Report.Errors.Any(e => e.NodeId == "spawn" && e.Text.Contains("prefab to spawn")));
        }

        [Test]
        public void SpawnEntity_PastItsBudget_IsStoppedAndReported()
        {
            services.MaxSpawnsPerExecution = 0;
            AbilityDefinition many = AbilityBuilder.Summon(demonPrefab);
            created.Add(many);

            executor.Execute(many.Compile().Ability, new ExecutionRequest(SourceHandle.FromEntity(wearer.Id), wearer.Id, default, default));

            Assert.AreEqual(0, spawner.Spawns.Count);
            Assert.IsTrue(diagnostics.Any(d => d.Contains("spawn limit")));
        }

        [Test]
        public void SpawnEntity_WithNoSpawnService_IsReportedNotThrown()
        {
            services.Spawner = null;

            Assert.DoesNotThrow(() => executor.Execute(summon.Compile().Ability, new ExecutionRequest(SourceHandle.FromEntity(wearer.Id), wearer.Id, default, default)));

            Assert.IsTrue(diagnostics.Any(d => d.Contains("no spawn service")));
        }

        [Test]
        public void ARefusedSpawn_IsReported()
        {
            spawner.Accepts = false;

            executor.Execute(summon.Compile().Ability, new ExecutionRequest(SourceHandle.FromEntity(wearer.Id), wearer.Id, default, default));

            Assert.IsTrue(diagnostics.Any(d => d.Contains("could not be spawned")));
        }

        [Test]
        public void SpawnedAtTheAbilitysOrigin_NotAtTheCasters()
        {
            executor.Execute(summon.Compile().Ability, new ExecutionRequest(SourceHandle.FromEntity(wearer.Id), wearer.Id, new Position3(3, 4, 5), default));

            Assert.AreEqual(3f, spawner.Spawns[0].Position.X);
            Assert.AreEqual(4f, spawner.Spawns[0].Position.Y);
            Assert.AreEqual(5f, spawner.Spawns[0].Position.Z);
        }
    }
}
