using NUnit.Framework;
using SimpleRPG.Gameplay;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using EntityId = SimpleRPG.Gameplay.EntityId;

namespace SimpleRPG.Abilities.Tests.EditMode
{
    /// <summary>
    /// The optional trace on the real executor: what it records, that it changes nothing, and that it costs nothing when it is not attached.
    /// </summary>
    public class AbilityTraceTests
    {
        private static readonly AttributeId MagicPower = new AttributeId(AbilityBuilder.MagicPower);

        private readonly List<Object> created = new List<Object>();

        private CombatantRegistry combatants;
        private CombatResolver combat;
        private AttributeSetRegistry attributes;
        private ScriptedProjectiles projectiles;
        private ScriptedMelee melee;
        private ExecutionServices services;
        private AbilityExecutor executor;
        private RecordingTrace trace;
        private GameObject prefab;
        private List<string> diagnostics;

        private TestCombatant wizard;
        private TestCombatant target;

        [SetUp]
        public void SetUp()
        {
            GameplayEventDispatcher events = new GameplayEventDispatcher();
            combatants = new CombatantRegistry();
            combat = new CombatResolver(events, combatants);
            attributes = new AttributeSetRegistry();
            projectiles = new ScriptedProjectiles();
            melee = new ScriptedMelee();
            services = new ExecutionServices(attributes, combat, projectiles, melee);
            executor = new AbilityExecutor(services);
            trace = new RecordingTrace();
            services.Trace = trace;
            diagnostics = new List<string>();
            services.Diagnostic += diagnostics.Add;
            prefab = new GameObject("ProjectilePrefab");
            created.Add(prefab);

            wizard = Add(1, 100);
            target = Add(3, 500);
            AttributeSet set = new AttributeSet();
            set.SetBase(MagicPower, 30);
            attributes.Register(SourceHandle.FromEntity(wizard.Id), set);
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

        private CompiledAbility Compile(AbilityDefinition ability)
        {
            created.Add(ability);
            CompileResult result = ability.Compile();
            Assert.IsTrue(result.Success, result.Report.ToString());
            return result.Ability;
        }

        private ExecutionResult Cast(CompiledAbility ability) =>
            executor.Execute(ability, new ExecutionRequest(SourceHandle.FromEntity(wizard.Id), wizard.Id, new Position3(0, 1, 0), new Position3(0, 0, 1)));

        private List<TraceEvent> Of(ExecutionResult run) => trace.ForExecution(run.Id);

        // ----- What is recorded -----

        [Test]
        public void ARun_IsRecordedInTheOrderItHappened()
        {
            CompiledAbility firebolt = Compile(AbilityBuilder.Firebolt(prefab));

            ExecutionResult run = Cast(firebolt);
            projectiles.Hit(0, target.Id);

            List<TraceEvent> events = Of(run);
            Assert.AreEqual(TraceEventKind.ExecutionStarted, events[0].Kind);
            CollectionAssert.AreEqual(new[] { "activate", "launch", "damage" }, events.Where(e => e.Kind == TraceEventKind.NodeRan).Select(e => e.NodeId));
            CollectionAssert.AreEqual(events.Select(e => e.Sequence).OrderBy(s => s), events.Select(e => e.Sequence), "Events are in sequence order.");
            Assert.AreEqual(events.Count, events.Select(e => e.Sequence).Distinct().Count(), "No two events share a sequence number.");
            Assert.IsTrue(events.All(e => e.Execution == run.Id && e.AbilityName == "Firebolt"));
        }

        [Test]
        public void TheRun_PausesAtTheProjectile_AndResumesWhenItHits()
        {
            CompiledAbility firebolt = Compile(AbilityBuilder.Firebolt(prefab));

            ExecutionResult run = Cast(firebolt);

            List<TraceEvent> beforeHit = Of(run);
            Assert.IsTrue(beforeHit.Any(e => e.Kind == TraceEventKind.Outcome && e.NodeId == "launch" && e.Text.Contains("launched")));
            TraceEvent suspended = beforeHit.Single(e => e.Kind == TraceEventKind.Suspended);
            Assert.AreEqual("launch", suspended.NodeId, "The run waits at the node that launched the projectile.");
            Assert.IsFalse(beforeHit.Any(e => e.NodeId == "damage"), "The damage has not happened yet.");

            projectiles.Hit(0, target.Id);

            List<TraceEvent> afterHit = Of(run);
            TraceEvent resumed = afterHit.Single(e => e.Kind == TraceEventKind.Resumed);
            Assert.AreEqual("damage", resumed.NodeId);
            StringAssert.Contains(target.Id.ToString(), resumed.Text);
            Assert.Greater(afterHit.Single(e => e.Kind == TraceEventKind.NodeRan && e.NodeId == "damage").Sequence, resumed.Sequence);
        }

        [Test]
        public void EveryNumberThatWasComputed_IsRecordedAgainstItsNode()
        {
            CompiledAbility firebolt = Compile(AbilityBuilder.Firebolt(prefab));

            ExecutionResult run = Cast(firebolt);
            projectiles.Hit(0, target.Id);

            List<TraceEvent> values = Of(run).Where(e => e.Kind == TraceEventKind.Value).ToList();
            Assert.AreEqual(30d, values.Single(e => e.NodeId == "magicPower").Number);
            Assert.AreEqual(0.8d, values.Single(e => e.NodeId == "scale").Number, 1e-6);
            Assert.AreEqual(12d, values.Single(e => e.NodeId == "base").Number);
            Assert.AreEqual(24d, values.Single(e => e.NodeId == "scaled").Number, 1e-6);
            Assert.AreEqual(36d, values.Single(e => e.NodeId == "total").Number, 1e-6);
            Assert.AreEqual(target.Id.ToString(), values.Single(e => e.NodeId == "victim").Text);
        }

        [Test]
        public void Damage_RecordsWhoWasHurtAndTheHealthBeforeAndAfter()
        {
            CompiledAbility firebolt = Compile(AbilityBuilder.Firebolt(prefab));

            ExecutionResult run = Cast(firebolt);
            projectiles.Hit(0, target.Id);

            TraceEvent damage = Of(run).Single(e => e.Kind == TraceEventKind.Outcome && e.NodeId == "damage");
            Assert.AreEqual(36d, damage.Number);
            StringAssert.Contains("Damage 36", damage.Text);
            StringAssert.Contains(target.Id.ToString(), damage.Text);
            StringAssert.Contains("500 to 464", damage.Text);
            Assert.AreEqual(464, target.Health);
        }

        [Test]
        public void ADamageNodeWithNoTarget_SaysWhyNothingHappened()
        {
            AbilityDefinition ability = AbilityBuilder.Empty("NoTarget");
            string victim = ability.AddNode(NodeTypes.EventVictim, null, "victim");
            string damage = ability.AddNode(NodeTypes.Damage, null, "damage");
            string amount = ability.AddNode(NodeTypes.Constant, new ConstantConfig { Value = 5f }, "amount");
            ability.Connect("activate", BuiltInNodes.Next, damage, BuiltInNodes.In);
            ability.Connect(victim, BuiltInNodes.Value, damage, BuiltInNodes.Target);
            ability.Connect(amount, BuiltInNodes.Value, damage, BuiltInNodes.Amount);
            created.Add(ability);
            // Damage must follow an impact to compile, so reach it through a projectile that hits nothing but the world.
            AbilityDefinition viaProjectile = AbilityBuilder.Firebolt(prefab);
            CompiledAbility compiled = Compile(viaProjectile);

            ExecutionResult run = Cast(compiled);
            projectiles.HitWorld(0);

            TraceEvent outcome = Of(run).Single(e => e.Kind == TraceEventKind.Outcome && e.NodeId == "damage");
            StringAssert.Contains("no target", outcome.Text);
        }

        [Test]
        public void ARunThatEndsWithoutAHit_SaysSo()
        {
            CompiledAbility firebolt = Compile(AbilityBuilder.Firebolt(prefab));

            ExecutionResult run = Cast(firebolt);
            projectiles.Expire(0);

            TraceEvent ended = Of(run).Single(e => e.Kind == TraceEventKind.Outcome && e.Text.Contains("without hitting"));
            Assert.AreEqual("launch", ended.NodeId, "The note belongs to the node that fired the projectile.");
            Assert.IsFalse(Of(run).Any(e => e.NodeId == "damage"));
        }

        [Test]
        public void AChance_RecordsTheRollAndWhichWayItWent()
        {
            AbilityDefinition ability = AbilityBuilder.Empty("Gamble");
            ability.AddNode(NodeTypes.Chance, new ChanceConfig { Probability = 0.5f }, "chance");
            ability.Connect("activate", BuiltInNodes.Next, "chance", BuiltInNodes.In);
            services.Random = new SeededRandom(1);
            double expectedRoll = new SeededRandom(1).NextDouble();

            ExecutionResult run = Cast(Compile(ability));

            TraceEvent outcome = Of(run).Single(e => e.Kind == TraceEventKind.Outcome && e.NodeId == "chance");
            Assert.AreEqual(0.5d, outcome.Number);
            StringAssert.Contains("rolled " + expectedRoll.ToString("0.###"), outcome.Text);
            StringAssert.StartsWith(expectedRoll < 0.5 ? "Success" : "Failure", outcome.Text);
        }

        [Test]
        public void ACertainChance_RollsNothing()
        {
            AbilityDefinition ability = AbilityBuilder.Empty("Sure");
            ability.AddNode(NodeTypes.Chance, new ChanceConfig { Probability = 1f }, "chance");
            ability.Connect("activate", BuiltInNodes.Next, "chance", BuiltInNodes.In);

            ExecutionResult run = Cast(Compile(ability));

            TraceEvent outcome = Of(run).Single(e => e.Kind == TraceEventKind.Outcome && e.NodeId == "chance");
            StringAssert.StartsWith("Success", outcome.Text);
            StringAssert.DoesNotContain("rolled", outcome.Text);
        }

        [Test]
        public void AMeleeSwing_RecordsTheSwingTheHitAndTheEnd()
        {
            CompiledAbility attack = Compile(AbilityBuilder.MeleeAttack());

            ExecutionResult run = Cast(attack);
            melee.Hit(0, target.Id, attackNumber: 2, comboScalar: 1.5f, weaponDamage: 10f);
            melee.Complete(0);

            List<TraceEvent> events = Of(run);
            Assert.IsTrue(events.Any(e => e.Kind == TraceEventKind.Outcome && e.NodeId == "swing" && e.Text.Contains("Swing started")));
            Assert.IsTrue(events.Any(e => e.Kind == TraceEventKind.Resumed && e.Text.Contains("attack 2")));
            Assert.AreEqual(15d, events.Single(e => e.Kind == TraceEventKind.Value && e.NodeId == "scaledDamage").Number, 1e-6);
            Assert.IsTrue(events.Any(e => e.Kind == TraceEventKind.Outcome && e.NodeId == "damage" && e.Text.Contains("Damage 15")));
        }

        // ----- Problems -----

        [Test]
        public void AnOperationThatCannotDoItsJob_IsRecordedAsAProblemAtItsNode()
        {
            ExecutionServices bare = new ExecutionServices(attributes, combat, null);
            bare.Trace = trace;
            AbilityExecutor noProjectiles = new AbilityExecutor(bare);
            CompiledAbility firebolt = Compile(AbilityBuilder.Firebolt(prefab));
            List<string> reported = new List<string>();
            bare.Diagnostic += reported.Add;

            ExecutionResult run = noProjectiles.Execute(firebolt, new ExecutionRequest(SourceHandle.FromEntity(wizard.Id), wizard.Id, default, default));

            TraceEvent problem = trace.ForExecution(run.Id).Single(e => e.Kind == TraceEventKind.Diagnostic);
            Assert.AreEqual("launch", problem.NodeId);
            Assert.AreEqual(reported.Single(), problem.Text, "The trace carries the same message the executor reports.");
        }

        [Test]
        public void ARefusedLaunch_IsRecorded()
        {
            projectiles.Accepts = false;

            ExecutionResult run = Cast(Compile(AbilityBuilder.Firebolt(prefab)));

            TraceEvent problem = Of(run).Single(e => e.Kind == TraceEventKind.Diagnostic);
            StringAssert.Contains("could not be launched", problem.Text);
            Assert.AreEqual(1, diagnostics.Count);
        }

        [Test]
        public void ARunStoppedByItsLimit_IsRecordedAsStopped()
        {
            services.MaxOperationsPerExecution = 1;

            ExecutionResult run = Cast(Compile(AbilityBuilder.Firebolt(prefab)));

            Assert.IsFalse(run.Completed);
            TraceEvent stopped = Of(run).Single(e => e.Kind == TraceEventKind.ExecutionStopped);
            StringAssert.Contains("limit", stopped.Text);
        }

        // ----- It changes nothing -----

        [Test]
        public void WithATraceAttached_TheAbilityDoesExactlyWhatItDoesWithout()
        {
            CompiledAbility firebolt = Compile(AbilityBuilder.Firebolt(prefab));
            TestCombatant control = Add(9, 500);

            // Without a trace.
            services.Trace = null;
            Cast(firebolt);
            projectiles.Hit(0, control.Id);

            // With one.
            services.Trace = trace;
            Cast(firebolt);
            projectiles.Hit(1, target.Id);

            Assert.AreEqual(control.Health, target.Health);
            Assert.AreEqual(464, target.Health);
            Assert.IsEmpty(diagnostics);
        }

        [Test]
        public void WithoutATrace_NothingIsRecorded_AndNothingBreaks()
        {
            services.Trace = null;

            ExecutionResult run = Cast(Compile(AbilityBuilder.Firebolt(prefab)));
            projectiles.Hit(0, target.Id);

            Assert.IsTrue(run.Completed);
            Assert.AreEqual(0, trace.Count);
            Assert.AreEqual(464, target.Health);
        }

        // ----- The recording -----

        [Test]
        public void Runs_AreKeptApart()
        {
            CompiledAbility firebolt = Compile(AbilityBuilder.Firebolt(prefab));

            ExecutionResult first = Cast(firebolt);
            ExecutionResult second = Cast(firebolt);
            projectiles.Hit(1, target.Id);

            Assert.AreNotEqual(first.Id, second.Id);
            CollectionAssert.AreEqual(new[] { first.Id, second.Id }, trace.Executions());
            Assert.IsFalse(Of(first).Any(e => e.NodeId == "damage"), "Only the second run's projectile hit.");
            Assert.IsTrue(Of(second).Any(e => e.NodeId == "damage"));
        }

        [Test]
        public void TheRecording_KeepsOnlyTheNewestEvents()
        {
            RecordingTrace small = new RecordingTrace(5);

            for (int i = 0; i < 8; i++)
            {
                small.Record(new TraceEvent(i, TraceEventKind.NodeRan, default, default, "A", "n" + i, null, i, null));
            }

            Assert.AreEqual(5, small.Count);
            CollectionAssert.AreEqual(new long[] { 3, 4, 5, 6, 7 }, small.Events.Select(e => e.Sequence));
        }

        [Test]
        public void ARecordingNeedsRoomForAtLeastOneEvent()
        {
            Assert.Throws<System.ArgumentOutOfRangeException>(() => new RecordingTrace(0));
        }

        [Test]
        public void TheRecording_TellsListenersAboutEachEvent_AndCanBeCleared()
        {
            int heard = 0;
            trace.Recorded += _ => heard++;

            Cast(Compile(AbilityBuilder.Firebolt(prefab)));

            Assert.AreEqual(trace.Count, heard);
            Assert.Greater(heard, 0);
            trace.Clear();
            Assert.AreEqual(0, trace.Count);
        }
    }
}
