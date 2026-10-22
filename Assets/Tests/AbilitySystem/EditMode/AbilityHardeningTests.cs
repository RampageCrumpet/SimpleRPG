using NUnit.Framework;
using SimpleRPG.Abilities.Editor;
using SimpleRPG.Gameplay;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Unity.Profiling;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using EntityId = SimpleRPG.Gameplay.EntityId;

namespace SimpleRPG.Abilities.Tests.EditMode
{
    /// <summary>
    /// Fixes from the review against the architecture spec: the build checks ability graphs, death cleanup survives an overloaded event queue,
    /// dropped work is reported, tracing costs nothing when it is off, and assets stay unchanged while the game runs.
    /// </summary>
    public class AbilityHardeningTests
    {
        private const string TempFolder = "Assets/_HardeningTests";

        private readonly List<UnityEngine.Object> created = new List<UnityEngine.Object>();

        [TearDown]
        public void TearDown()
        {
            foreach (UnityEngine.Object o in created)
            {
                if (o != null) UnityEngine.Object.DestroyImmediate(o);
            }

            created.Clear();
            if (AssetDatabase.IsValidFolder(TempFolder)) AssetDatabase.DeleteAsset(TempFolder);
        }

        private AbilityDefinition NewAbility()
        {
            AbilityDefinition ability = AbilityBuilder.Empty("Hardening");
            created.Add(ability);
            return ability;
        }

        // ----- The build checks ability graphs -----

        [Test]
        public void TheBuildGate_ReportsAnAbilitysCompileErrors_WithAssetNodeAndPort()
        {
            if (!AssetDatabase.IsValidFolder(TempFolder)) AssetDatabase.CreateFolder("Assets", "_HardeningTests");
            AbilityDefinition broken = ScriptableObject.CreateInstance<AbilityDefinition>();
            AssetDatabase.CreateAsset(broken, TempFolder + "/Broken.asset");
            broken.AddNode(NodeTypes.Damage, null, "damage");
            broken.Connect("activate", BuiltInNodes.Next, "damage", BuiltInNodes.In);

            List<string> problems = DefinitionRegistryTools.AbilityGraphProblems(new[] { broken });

            Assert.IsNotEmpty(problems);
            Assert.IsTrue(problems.All(p => p.StartsWith(TempFolder + "/Broken.asset")), "Every problem names the asset.");
            Assert.IsTrue(problems.Any(p => p.Contains("node 'damage'") && p.Contains("port '" + BuiltInNodes.Amount + "'")), string.Join("\n", problems));
        }

        [Test]
        public void TheBuildGate_IgnoresWarnings_AndAcceptsAValidAbility()
        {
            AbilityDefinition ability = NewAbility();
            ability.AddNode(NodeTypes.Constant, null, "unused");

            Assert.IsTrue(ability.Compile().Report.Warnings.Any(), "The unused node is a warning.");
            Assert.IsEmpty(DefinitionRegistryTools.AbilityGraphProblems(new[] { ability }));
        }

        [Test]
        public void EveryAbilityInTheProject_Compiles()
        {
            List<string> problems = DefinitionRegistryTools.AbilityGraphProblems(DefinitionRegistryTools.FindAllDefinitions());

            Assert.IsEmpty(problems, string.Join("\n", problems));
        }

        // ----- An overloaded event queue still finishes deaths -----

        private sealed class Flood : GameplayEvent
        {
        }

        private static DeathContext Death(ulong entity) =>
            new DeathContext(new DeathTransitionId(new EntityId(entity), 1), default, default, DamageProvenance.None, default, new List<SourceHandle>().AsReadOnly());

        [Test]
        public void WhenTheBudgetRunsOut_DeathCleanupStillRuns_AndOtherEventsAreDropped()
        {
            GameplayEventDispatcher events = new GameplayEventDispatcher(maxEventsPerDrain: 20);
            List<string> reported = new List<string>();
            events.Diagnostic += reported.Add;
            int finalized = 0;
            int flooded = 0;
            events.Subscribe<DeathFinalizeEvent>(_ => finalized++);
            events.Subscribe<Flood>(_ =>
            {
                flooded++;
                events.Enqueue(new Flood());
                events.Enqueue(new Flood());
            });

            events.Enqueue(new Flood());
            events.Enqueue(new DeathFinalizeEvent(Death(7)));
            DrainResult result = events.Drain();

            Assert.IsTrue(result.BudgetExceeded);
            Assert.AreEqual(1, finalized, "The death was cleaned up even though the queue was over budget.");
            Assert.LessOrEqual(flooded, 20, "The runaway events stopped at the budget.");
            Assert.IsTrue(reported.Any(m => m.Contains("budget")));
            Assert.AreEqual(0, events.PendingCount);
        }

        [Test]
        public void ADrainThatRanOverBudget_DoesNotStopTheNextOneWorking()
        {
            GameplayEventDispatcher events = new GameplayEventDispatcher(maxEventsPerDrain: 5);
            events.Subscribe<Flood>(_ =>
            {
                events.Enqueue(new Flood());
                events.Enqueue(new Flood());
            });
            events.Enqueue(new Flood());
            Assert.IsTrue(events.Drain().BudgetExceeded);

            int seen = 0;
            GameplayEventDispatcher fresh = events;
            fresh.Subscribe<DamageResolvedEvent>(_ => seen++);
            fresh.Enqueue(new DamageResolvedEvent(new DamageRequest(new EntityId(1), 1), 10, 9, false));
            fresh.Drain();

            Assert.AreEqual(1, seen, "Events queued after an overloaded drain are delivered normally.");
        }

        [Test]
        public void DamageDroppedForDepth_IsReported()
        {
            GameplayEventDispatcher events = new GameplayEventDispatcher(maxDepth: 1);
            CombatantRegistry combatants = new CombatantRegistry();
            CombatResolver combat = new CombatResolver(events, combatants);
            TestCombatant target = new TestCombatant(2, 100);
            combatants.Register(target);
            List<string> reported = new List<string>();
            events.Diagnostic += reported.Add;

            // Inside a handler two events deep, any further damage is too deep to process.
            events.Subscribe<Flood>(_ => events.Enqueue(new Flood()));
            DamageOutcome outcome = default;
            int hits = 0;
            events.Subscribe<Flood>(e =>
            {
                if (e.Depth >= 1 && hits++ == 0) outcome = combat.Apply(new DamageRequest(target.Id, 5));
            });
            events.Enqueue(new Flood());
            events.Drain();

            Assert.AreEqual(DamageResult.RejectedTooDeep, outcome.Result);
            Assert.AreEqual(100, target.Health);
            Assert.IsTrue(reported.Any(m => m.Contains("Damage of 5") && m.Contains("dropped")), string.Join("\n", reported));
        }

        // ----- Tracing costs nothing when it is off -----

        [UnityTest]
        public IEnumerator WithNoTraceAttached_AnAbilityRunsWithFarLessGarbage()
        {
            GameObject prefab = new GameObject("Projectile");
            created.Add(prefab);
            GameplayEventDispatcher events = new GameplayEventDispatcher();
            CombatantRegistry combatants = new CombatantRegistry();
            CombatResolver combat = new CombatResolver(events, combatants);
            AttributeSetRegistry attributes = new AttributeSetRegistry();
            ScriptedProjectiles projectiles = new ScriptedProjectiles();
            ExecutionServices services = new ExecutionServices(attributes, combat, projectiles);
            AbilityExecutor executor = new AbilityExecutor(services);
            TestCombatant caster = new TestCombatant(1, 100);
            TestCombatant target = new TestCombatant(2, int.MaxValue);
            combatants.Register(caster);
            combatants.Register(target);
            AbilityDefinition firebolt = AbilityBuilder.Firebolt(prefab);
            created.Add(firebolt);
            CompiledAbility compiled = firebolt.Compile().Ability;
            ExecutionRequest request = new ExecutionRequest(SourceHandle.FromEntity(caster.Id), caster.Id, default, default);

            void RunMany(int count)
            {
                projectiles.Launches.Clear();
                for (int i = 0; i < count; i++)
                {
                    executor.Execute(compiled, request);
                    projectiles.Hit(i, target.Id);
                }
            }

            // The profiler reports the garbage made in the last frame, so each measurement is a frame of its own.
            using (ProfilerRecorder recorder = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "GC Allocated In Frame"))
            {
                services.Trace = null;
                RunMany(50);
                yield return null;

                RunMany(400);
                yield return null;
                long untraced = recorder.LastValue;

                services.Trace = new RecordingTrace(16);
                RunMany(50);
                yield return null;

                RunMany(400);
                yield return null;
                long traced = recorder.LastValue;

                Assert.Greater(traced, 0, "The profiler reported nothing, so this test cannot tell.");
                Assert.Less(untraced, traced * 0.85, "Untraced " + untraced + " bytes in a frame, traced " + traced + ": tracing must not build text when it is off.");
            }
        }

        // ----- Assets stay unchanged while the game runs -----

        [Test]
        public void AnAbilityAsset_HoldsNoCompiledState()
        {
            FieldInfo[] fields = typeof(AbilityDefinition).GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);

            Assert.IsFalse(fields.Any(f => typeof(CompileResult).IsAssignableFrom(f.FieldType)), "The compiled result lives beside the asset, not in it.");
        }

        [Test]
        public void CompilingWithAnotherRegistry_GivesThatRegistrysAnswer_NotAStaleOne()
        {
            AbilityDefinition ability = NewAbility();
            ability.AddNode(NodeTypes.Constant, null, "c");

            CompileResult normal = ability.Compile();
            CompileResult withNoNodeTypes = ability.Compile(new AbilityNodeRegistry());
            CompileResult normalAgain = ability.Compile();

            Assert.IsTrue(normal.Success);
            Assert.IsFalse(withNoNodeTypes.Success, "With no node types registered, nothing is known.");
            Assert.IsTrue(normalAgain.Success);
        }

        [Test]
        public void ChangingTheGraph_MakesItCompileAgain()
        {
            AbilityDefinition ability = NewAbility();
            CompileResult before = ability.Compile();
            Assert.AreSame(before, ability.Compile(), "Unchanged, the result is reused.");

            ability.AddNode(NodeTypes.Constant, null, "c");

            Assert.AreNotSame(before, ability.Compile());
        }

        [Test]
        public void TheStatusMagnitudePolicies_AreOnlyTheOnesThatDoSomething()
        {
            CollectionAssert.AreEquivalent(new[] { "Additive", "StrongestOnly" }, Enum.GetNames(typeof(MagnitudePolicy)));
        }

        // ----- The editor lets go of what it was listening to -----

        [Test]
        public void ACanvasWhoseAbilityIsGone_StopsListeningToUndo()
        {
            AbilityDefinition ability = NewAbility();
            AbilityGraphDocument document = new AbilityGraphDocument(ability);
            AbilityGraphView view = new AbilityGraphView(document);
            int redraws = 0;
            view.Redrawn += () => redraws++;

            UnityEngine.Object.DestroyImmediate(ability);
            created.Remove(ability);
            Undo.IncrementCurrentGroup();
            Assert.DoesNotThrow(() => document.NotifyChanged());

            Assert.AreEqual(0, redraws, "A canvas for a destroyed ability does not redraw.");
        }

        [Test]
        public void TheNodePalette_IsReusedRatherThanCreatedOverAndOver()
        {
            NodePalette first = NodePalette.Obtain();
            NodePalette second = NodePalette.Obtain();

            Assert.AreSame(first, second);
        }
    }
}
