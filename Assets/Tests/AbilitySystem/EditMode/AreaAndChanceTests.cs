using NUnit.Framework;
using SimpleRPG.Gameplay;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using EntityId = SimpleRPG.Gameplay.EntityId;

namespace SimpleRPG.Abilities.Tests.EditMode
{
    public class SeededRandomTests
    {
        [Test]
        public void TheSameSeed_GivesTheSameSequence()
        {
            SeededRandom a = new SeededRandom(1234);
            SeededRandom b = new SeededRandom(1234);

            for (int i = 0; i < 100; i++)
            {
                Assert.AreEqual(a.NextDouble(), b.NextDouble());
            }
        }

        [Test]
        public void DifferentSeeds_GiveDifferentSequences()
        {
            SeededRandom a = new SeededRandom(1);
            SeededRandom b = new SeededRandom(2);

            Assert.AreNotEqual(a.NextDouble(), b.NextDouble());
        }

        [Test]
        public void Values_AreInRange_AndSpreadEvenly()
        {
            SeededRandom random = new SeededRandom(99);
            int below = 0;
            for (int i = 0; i < 10000; i++)
            {
                double value = random.NextDouble();
                Assert.GreaterOrEqual(value, 0d);
                Assert.Less(value, 1d);
                if (value < 0.1) below++;
            }

            Assert.AreEqual(1000, below, 120, "About a tenth of the values should fall below 0.1.");
        }

        [Test]
        public void TheSequence_IsPinned_SoSavedSeedsKeepMeaning()
        {
            SeededRandom random = new SeededRandom(1);

            // These are the first values for seed 1. If this changes, every recorded seed changes with it.
            Assert.AreEqual(0.5665615751722809, random.NextDouble(), 1e-12);
            Assert.AreEqual(0.7457817572627011, random.NextDouble(), 1e-12);
            Assert.AreEqual(0.9710027535867962, random.NextDouble(), 1e-12);
        }
    }

    /// <summary>
    /// Runs area abilities (Fireball) and chance nodes with the real executor, combat resolver and event dispatcher.
    /// </summary>
    public class AreaAndChanceTests
    {
        private readonly List<Object> created = new List<Object>();

        private GameplayEventDispatcher events;
        private CombatantRegistry combatants;
        private CombatResolver combat;
        private ScriptedProjectiles projectiles;
        private ExecutionServices services;
        private AbilityExecutor executor;
        private GameObject prefab;
        private List<string> diagnostics;

        [SetUp]
        public void SetUp()
        {
            events = new GameplayEventDispatcher();
            combatants = new CombatantRegistry();
            combat = new CombatResolver(events, combatants);
            projectiles = new ScriptedProjectiles();
            services = new ExecutionServices(new AttributeSetRegistry(), combat, projectiles)
            {
                Targets = new CombatantTargetQuery(combatants),
                Random = new SeededRandom(7),
            };
            executor = new AbilityExecutor(services);
            diagnostics = new List<string>();
            services.Diagnostic += diagnostics.Add;
            prefab = new GameObject("ProjectilePrefab");
            created.Add(prefab);
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

        private TestCombatant Add(ulong id, float x, float y, float z, int health = 100)
        {
            TestCombatant combatant = new TestCombatant(id, health) { Position = new Position3(x, y, z) };
            combatants.Register(combatant);
            return combatant;
        }

        private CompiledAbility Compile(AbilityDefinition definition)
        {
            created.Add(definition);
            CompileResult result = definition.Compile();
            Assert.IsTrue(result.Success, result.Report.ToString());
            return result.Ability;
        }

        private static ExecutionRequest Request(TestCombatant caster) =>
            new ExecutionRequest(SourceHandle.FromEntity(caster.Id), caster.Id, default, new Position3(0, 0, 1));

        // ----- Fireball: area damage -----

        [Test]
        public void Fireball_DamagesEveryoneInTheRadius_AndNoOneOutside()
        {
            TestCombatant caster = Add(1, -20, 0, 0);
            TestCombatant victim = Add(2, 0, 0, 0);
            TestCombatant near = Add(3, 2, 0, 0);
            TestCombatant far = Add(4, 10, 0, 0);
            executor.Execute(Compile(AbilityBuilder.Fireball(prefab, radius: 3f)), Request(caster));

            projectiles.Hit(0, victim.Id, at: new Position3(0, 0, 0));

            Assert.AreEqual(85, victim.Health);
            Assert.AreEqual(85, near.Health);
            Assert.AreEqual(100, far.Health);
        }

        [Test]
        public void TheRadiusEdge_IsInclusive()
        {
            TestCombatant caster = Add(1, -20, 0, 0);
            TestCombatant onTheEdge = Add(2, 3, 0, 0);
            TestCombatant justOutside = Add(3, 3.01f, 0, 0);
            executor.Execute(Compile(AbilityBuilder.Fireball(prefab, radius: 3f)), Request(caster));

            projectiles.HitWorld(0, new Position3(0, 0, 0));

            Assert.AreEqual(85, onTheEdge.Health);
            Assert.AreEqual(100, justOutside.Health);
        }

        [Test]
        public void TheCaster_IsNotHurtByItsOwnExplosion()
        {
            TestCombatant caster = Add(1, 0, 0, 0);
            TestCombatant victim = Add(2, 1, 0, 0);
            executor.Execute(Compile(AbilityBuilder.Fireball(prefab)), Request(caster));

            projectiles.Hit(0, victim.Id, at: new Position3(0, 0, 0));

            Assert.AreEqual(100, caster.Health);
            Assert.AreEqual(85, victim.Health);
        }

        [Test]
        public void TheCaster_IsHurt_WhenTheAbilitySaysSo()
        {
            TestCombatant caster = Add(1, 0, 0, 0);
            executor.Execute(Compile(AbilityBuilder.Fireball(prefab, excludeInstigator: false)), Request(caster));

            projectiles.HitWorld(0, new Position3(0, 0, 0));

            Assert.AreEqual(85, caster.Health);
        }

        [Test]
        public void TheVictim_CanBeLeftOut()
        {
            TestCombatant caster = Add(1, -20, 0, 0);
            TestCombatant victim = Add(2, 0, 0, 0);
            TestCombatant neighbour = Add(3, 1, 0, 0);
            executor.Execute(Compile(AbilityBuilder.Fireball(prefab, excludeVictim: true)), Request(caster));

            projectiles.Hit(0, victim.Id, at: new Position3(0, 0, 0));

            Assert.AreEqual(100, victim.Health);
            Assert.AreEqual(85, neighbour.Health);
        }

        [Test]
        public void TheDead_AreNotHurtAgain()
        {
            TestCombatant caster = Add(1, -20, 0, 0);
            TestCombatant corpse = Add(2, 0, 0, 0, health: 5);
            combat.Apply(new DamageRequest(corpse.Id, 100));
            int deaths = 0;
            events.Subscribe<EntityDiedEvent>(e => deaths++);
            executor.Execute(Compile(AbilityBuilder.Fireball(prefab)), Request(caster));

            projectiles.HitWorld(0, new Position3(0, 0, 0));

            Assert.AreEqual(0, deaths);
            Assert.IsEmpty(diagnostics);
        }

        [Test]
        public void EachTarget_IsDamagedOnce_InAFixedOrder()
        {
            TestCombatant caster = Add(1, -20, 0, 0);
            Add(30, 1, 0, 0);
            Add(10, -1, 0, 0);
            Add(20, 0, 1, 0);
            List<ulong> order = new List<ulong>();
            events.Subscribe<DamageResolvedEvent>(e => order.Add(e.Request.Target.Value));
            executor.Execute(Compile(AbilityBuilder.Fireball(prefab)), Request(caster));

            projectiles.HitWorld(0, new Position3(0, 0, 0));

            CollectionAssert.AreEqual(new ulong[] { 10, 20, 30 }, order, "Targets are handled in order of entity ID, however they were found.");
        }

        [Test]
        public void ExplosionDamage_IsSpellDamage_AttributedToTheCaster()
        {
            TestCombatant caster = Add(1, -20, 0, 0);
            TestCombatant victim = Add(2, 0, 0, 0);
            DamageRequest seen = default;
            events.Subscribe<DamageResolvedEvent>(e => seen = e.Request);
            executor.Execute(Compile(AbilityBuilder.Fireball(prefab)), Request(caster));

            projectiles.Hit(0, victim.Id, at: new Position3(0, 0, 0));

            Assert.AreEqual(DamageProvenance.Spell, seen.Provenance);
            Assert.AreEqual(caster.Id, seen.Instigator);
        }

        [Test]
        public void AnAreaThatSelectsTooMany_IsCappedAndReported()
        {
            services.MaxTargetsPerQuery = 3;
            TestCombatant caster = Add(1, -20, 0, 0);
            for (ulong i = 10; i < 20; i++) Add(i, 0, 0, 0);
            executor.Execute(Compile(AbilityBuilder.Fireball(prefab)), Request(caster));

            projectiles.HitWorld(0, new Position3(0, 0, 0));

            Assert.AreEqual(3, combatants.All.Count(c => c.Health < 100));
            Assert.IsTrue(diagnostics.Any(d => d.Contains("only the first 3")), string.Join("\n", diagnostics));
        }

        [Test]
        public void NoTargetQuery_SelectsNothing_AndSaysSo()
        {
            services.Targets = null;
            TestCombatant caster = Add(1, -20, 0, 0);
            TestCombatant victim = Add(2, 0, 0, 0);
            executor.Execute(Compile(AbilityBuilder.Fireball(prefab)), Request(caster));

            projectiles.Hit(0, victim.Id, at: new Position3(0, 0, 0));

            Assert.AreEqual(100, victim.Health);
            Assert.IsTrue(diagnostics.Any(d => d.Contains("no target query")));
        }

        // ----- Compile-time rules for the new nodes -----

        [Test]
        public void Fireball_Compiles()
        {
            AbilityDefinition fireball = AbilityBuilder.Fireball(prefab);
            created.Add(fireball);

            Assert.IsTrue(fireball.Compile().Success, fireball.Compile().Report.ToString());
        }

        [Test]
        public void CurrentTarget_OutsideAForEach_IsRejected()
        {
            AbilityDefinition ability = AbilityBuilder.Empty("A");
            created.Add(ability);
            string damage = ability.AddNode(NodeTypes.Damage, null, "damage");
            string current = ability.AddNode(NodeTypes.CurrentTarget, null, "current");
            string amount = ability.AddNode(NodeTypes.Constant, null, "amount");
            ability.Connect("activate", BuiltInNodes.Next, damage, BuiltInNodes.In);
            ability.Connect(current, BuiltInNodes.Value, damage, BuiltInNodes.Target);
            ability.Connect(amount, BuiltInNodes.Value, damage, BuiltInNodes.Amount);

            CompileResult result = ability.Compile();

            Assert.IsFalse(result.Success);
            Assert.IsTrue(result.Report.Errors.Any(e => e.NodeId == "current" && e.Text.Contains("inside a For Each Target branch")), result.Report.ToString());
        }

        [Test]
        public void AnAreaSelection_BeforeAnyImpact_IsRejected()
        {
            AbilityDefinition ability = AbilityBuilder.Empty("A");
            created.Add(ability);
            string each = ability.AddNode(NodeTypes.ForEachTarget, null, "each");
            string area = ability.AddNode(NodeTypes.EntitiesInRadius, null, "area");
            ability.Connect("activate", BuiltInNodes.Next, each, BuiltInNodes.In);
            ability.Connect(area, BuiltInNodes.Value, each, BuiltInNodes.Targets);

            CompileResult result = ability.Compile();

            Assert.IsTrue(result.Report.Errors.Any(e => e.NodeId == "area" && e.Text.Contains("only available after an impact")), result.Report.ToString());
        }

        [Test]
        public void ConnectingAnEntityToAnEntitySetInput_IsRejected()
        {
            AbilityDefinition ability = Track(AbilityBuilder.Fireball(prefab));
            ability.AddNode(NodeTypes.EventVictim, null, "victim");
            ability.Connect("victim", BuiltInNodes.Value, "each", BuiltInNodes.Targets);

            CompileResult result = ability.Compile();

            Assert.IsFalse(result.Success);
            Assert.IsTrue(result.Report.Errors.Any(e => e.NodeId == "each" && e.PortId == BuiltInNodes.Targets), result.Report.ToString());
        }

        private AbilityDefinition Track(AbilityDefinition definition)
        {
            created.Add(definition);
            return definition;
        }

        [TestCase(0f)]
        [TestCase(-3f)]
        [TestCase(float.NaN)]
        public void ARadius_ThatIsNotPositive_IsRejected(float radius)
        {
            AbilityDefinition ability = Track(AbilityBuilder.Fireball(prefab, radius: radius));

            Assert.IsTrue(ability.Compile().Report.Errors.Any(e => e.NodeId == "area" && e.Text.Contains("positive")), ability.Compile().Report.ToString());
        }

        [Test]
        public void AHugeRadius_IsOnlyAWarning()
        {
            AbilityDefinition ability = Track(AbilityBuilder.Fireball(prefab, radius: 500f));

            CompileResult result = ability.Compile();

            Assert.IsTrue(result.Success);
            Assert.IsTrue(result.Report.Warnings.Any(w => w.NodeId == "area"));
        }

        // ----- Chance -----

        [Test]
        public void ChanceNode_IsInThePalette()
        {
            Assert.IsTrue(AbilityNodeRegistry.Default.Search("chance").Any(d => d.TypeId == NodeTypes.Chance));
        }

        [TestCase(-0.1f)]
        [TestCase(1.5f)]
        [TestCase(float.NaN)]
        public void AProbabilityOutsideZeroToOne_IsRejected(float probability)
        {
            AbilityDefinition ability = Track(AbilityBuilder.Empty("A"));
            ability.AddNode(NodeTypes.Chance, new ChanceConfig { Probability = probability }, "chance");

            Assert.IsTrue(ability.Compile().Report.Errors.Any(e => e.NodeId == "chance" && e.Text.Contains("between 0 and 1")));
        }

        /// <summary>
        /// A chance ability that, on success, launches a projectile (visible as a recorded launch) and does nothing on failure.
        /// </summary>
        private CompiledAbility ChanceToLaunch(float probability)
        {
            AbilityDefinition ability = AbilityBuilder.Empty("ChanceToLaunch");
            string chance = ability.AddNode(NodeTypes.Chance, new ChanceConfig { Probability = probability }, "chance");
            string launch = ability.AddNode(NodeTypes.LaunchProjectile, new LaunchProjectileConfig { Projectile = prefab }, "launch");
            ability.Connect("activate", BuiltInNodes.Next, chance, BuiltInNodes.In);
            ability.Connect(chance, BuiltInNodes.Success, launch, BuiltInNodes.In);
            return Compile(ability);
        }

        [Test]
        public void ChanceOfOne_AlwaysSucceeds_AndChanceOfZero_NeverDoes()
        {
            TestCombatant caster = Add(1, 0, 0, 0);
            CompiledAbility always = ChanceToLaunch(1f);
            CompiledAbility never = ChanceToLaunch(0f);

            for (int i = 0; i < 20; i++)
            {
                executor.Execute(always, Request(caster));
                executor.Execute(never, Request(caster));
            }

            Assert.AreEqual(20, projectiles.Launches.Count);
        }

        [Test]
        public void ChanceOfOneOrZero_DoesNotConsumeRandomNumbers()
        {
            CountingRandom random = new CountingRandom(new SeededRandom(3));
            services.Random = random;
            TestCombatant caster = Add(1, 0, 0, 0);

            executor.Execute(ChanceToLaunch(1f), Request(caster));
            executor.Execute(ChanceToLaunch(0f), Request(caster));

            Assert.AreEqual(0, random.Draws);
        }

        [Test]
        public void AChance_UsesTheSeededStream_SoTheSameSeedRepeatsTheSameOutcomes()
        {
            TestCombatant caster = Add(1, 0, 0, 0);
            CompiledAbility tenPercent = ChanceToLaunch(0.1f);

            List<bool> Run(int seed)
            {
                projectiles.Launches.Clear();
                services.Random = new SeededRandom(seed);
                List<bool> outcomes = new List<bool>();
                for (int i = 0; i < 200; i++)
                {
                    int before = projectiles.Launches.Count;
                    executor.Execute(tenPercent, Request(caster));
                    outcomes.Add(projectiles.Launches.Count > before);
                }

                return outcomes;
            }

            List<bool> first = Run(42);
            List<bool> second = Run(42);
            List<bool> other = Run(43);

            CollectionAssert.AreEqual(first, second);
            CollectionAssert.AreNotEqual(first, other);
        }

        [Test]
        public void AChanceOfTenPercent_SucceedsAboutAtenthOfTheTime()
        {
            TestCombatant caster = Add(1, 0, 0, 0);
            services.Random = new SeededRandom(2024);
            CompiledAbility tenPercent = ChanceToLaunch(0.1f);
            services.MaxProjectilesPerExecution = 1;

            for (int i = 0; i < 2000; i++)
            {
                executor.Execute(tenPercent, Request(caster));
            }

            Assert.AreEqual(200, projectiles.Launches.Count, 40);
        }

        [Test]
        public void NoRandomStream_MeansTheChanceFails_AndSaysSo()
        {
            services.Random = null;
            TestCombatant caster = Add(1, 0, 0, 0);

            executor.Execute(ChanceToLaunch(0.5f), Request(caster));

            Assert.AreEqual(0, projectiles.Launches.Count);
            Assert.IsTrue(diagnostics.Any(d => d.Contains("no random stream")));
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
    }
}
