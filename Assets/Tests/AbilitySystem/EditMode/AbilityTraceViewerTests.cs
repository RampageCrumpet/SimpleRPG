using NUnit.Framework;
using SimpleRPG.Abilities.Editor;
using SimpleRPG.Gameplay;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;
using EntityId = SimpleRPG.Gameplay.EntityId;

namespace SimpleRPG.Abilities.Tests.EditMode
{
    /// <summary>
    /// The canvas showing a run of the real executor in a scripted test world: which nodes ran, what they computed and what they did.
    /// </summary>
    public class AbilityTraceViewerTests
    {
        private static readonly AttributeId MagicPower = new AttributeId(AbilityBuilder.MagicPower);

        private readonly List<Object> created = new List<Object>();

        private CombatantRegistry combatants;
        private ScriptedProjectiles projectiles;
        private ExecutionServices services;
        private AbilityExecutor executor;
        private RecordingTrace trace;
        private GameObject prefab;
        private TestCombatant wizard;
        private TestCombatant target;

        private AbilityDefinition firebolt;
        private AbilityGraphView view;
        private AbilityTraceViewer viewer;

        [SetUp]
        public void SetUp()
        {
            GameplayEventDispatcher events = new GameplayEventDispatcher();
            combatants = new CombatantRegistry();
            CombatResolver combat = new CombatResolver(events, combatants);
            AttributeSetRegistry attributes = new AttributeSetRegistry();
            projectiles = new ScriptedProjectiles();
            services = new ExecutionServices(attributes, combat, projectiles, new ScriptedMelee());
            executor = new AbilityExecutor(services);
            trace = new RecordingTrace();
            services.Trace = trace;
            prefab = new GameObject("ProjectilePrefab");
            created.Add(prefab);

            wizard = new TestCombatant(1, 100);
            target = new TestCombatant(3, 500);
            combatants.Register(wizard);
            combatants.Register(target);
            AttributeSet set = new AttributeSet();
            set.SetBase(MagicPower, 30);
            attributes.Register(SourceHandle.FromEntity(wizard.Id), set);

            firebolt = AbilityBuilder.Firebolt(prefab);
            firebolt.AddNode(NodeTypes.Constant, new ConstantConfig { Value = 99f }, "unused");
            created.Add(firebolt);
            view = new AbilityGraphView(new AbilityGraphDocument(firebolt));
            viewer = new AbilityTraceViewer(view, firebolt);
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

        // A node is dimmed when its opacity was set below full; a node that was never touched has no opacity set at all.
        private static bool Dimmed(AbilityNodeView node) => node.style.opacity.keyword == StyleKeyword.Undefined && node.style.opacity.value < 1f;

        private ExecutionResult Cast(AbilityDefinition ability)
        {
            CompileResult result = ability.Compile();
            Assert.IsTrue(result.Success, result.Report.ToString());
            return executor.Execute(result.Ability, new ExecutionRequest(SourceHandle.FromEntity(wizard.Id), wizard.Id, new Position3(0, 1, 0), new Position3(0, 0, 1)));
        }

        // ----- The canvas -----

        [Test]
        public void NothingAttached_ShowsNothing()
        {
            Assert.IsEmpty(viewer.Executions());
            Assert.IsTrue(view.NodeViews.Values.All(n => !n.Visited && n.TraceSummary == string.Empty && !Dimmed(n)));
        }

        [Test]
        public void ARun_MarksTheNodesItRan_AndDimsTheOnesItNeverReached()
        {
            // "unused" is a Constant that nothing reads, so no run ever evaluates it.
            viewer.Attach(trace);

            Cast(firebolt);
            projectiles.Hit(0, target.Id);
            viewer.Refresh();

            Assert.IsTrue(view.NodeViews["activate"].Visited);
            Assert.IsTrue(view.NodeViews["launch"].Visited);
            Assert.IsTrue(view.NodeViews["damage"].Visited);
            Assert.IsFalse(view.NodeViews["unused"].Visited);
            Assert.AreEqual(1f, view.NodeViews["damage"].style.opacity.value);
            Assert.AreEqual(0.4f, view.NodeViews["unused"].style.opacity.value, 1e-6, "A node the run never reached is dimmed.");
        }

        [Test]
        public void ADataNode_ShowsWhatItComputed()
        {
            viewer.Attach(trace);

            Cast(firebolt);
            projectiles.Hit(0, target.Id);
            viewer.Refresh();

            Assert.AreEqual("= 12", view.NodeViews["base"].TraceSummary);
            Assert.AreEqual("= 36", view.NodeViews["total"].TraceSummary);
            Assert.AreEqual("= 30", view.NodeViews["magicPower"].TraceSummary);
            Assert.IsFalse(view.NodeViews["total"].Visited, "A data node is not an operation, so it has values and no step.");
        }

        [Test]
        public void AnOperation_ShowsWhatItDid()
        {
            viewer.Attach(trace);

            Cast(firebolt);
            projectiles.Hit(0, target.Id);
            viewer.Refresh();

            StringAssert.Contains("Damage 36", view.NodeViews["damage"].TraceSummary);
            StringAssert.Contains("Projectile launched", view.NodeViews["launch"].TraceSummary);
        }

        [Test]
        public void TheTooltip_ListsTheLastRun_AlongsideTheNodesDescription()
        {
            viewer.Attach(trace);

            Cast(firebolt);
            projectiles.Hit(0, target.Id);
            viewer.Refresh();

            string tooltip = view.NodeViews["damage"].tooltip;
            StringAssert.Contains("Last run", tooltip);
            StringAssert.Contains("Damage 36", tooltip);
            StringAssert.Contains("step", tooltip);
        }

        [Test]
        public void ATraceAndAValidationProblem_ShareTheTooltip()
        {
            firebolt.AddNode(NodeTypes.Activate, null, "second");
            view.Rebuild();
            viewer.Attach(trace);

            Cast(AbilityBuilder.Firebolt(prefab));
            viewer.Refresh();

            AbilityNodeView node = view.NodeViews.Values.First(n => n.Problems.Any(p => p.Severity == ValidationSeverity.Error));
            StringAssert.Contains("Error", node.tooltip);
        }

        [Test]
        public void TheNewestRun_IsShownAsItStarts()
        {
            viewer.Attach(trace);

            ExecutionResult first = Cast(firebolt);
            ExecutionResult second = Cast(firebolt);
            projectiles.Hit(1, target.Id);
            viewer.Refresh();

            Assert.AreEqual(second.Id, viewer.Selected);
            Assert.IsTrue(viewer.FollowLatest);
            CollectionAssert.AreEqual(new[] { first.Id, second.Id }, viewer.Executions());
            Assert.IsTrue(view.NodeViews["damage"].Visited, "The second run's projectile hit.");
        }

        [Test]
        public void PickingAnEarlierRun_ShowsIt_AndStopsFollowing()
        {
            viewer.Attach(trace);
            ExecutionResult first = Cast(firebolt);
            ExecutionResult second = Cast(firebolt);
            projectiles.Hit(1, target.Id);
            viewer.Refresh();

            viewer.Select(first.Id);

            Assert.AreEqual(first.Id, viewer.Selected);
            Assert.IsFalse(viewer.FollowLatest);
            Assert.IsFalse(view.NodeViews["damage"].Visited, "The first run is still waiting for its projectile.");

            Cast(firebolt);
            viewer.Refresh();
            Assert.AreEqual(first.Id, viewer.Selected, "A new run does not take the view away from the one picked.");

            viewer.ShowLatest();
            Assert.IsTrue(viewer.FollowLatest);
            Assert.AreNotEqual(first.Id, viewer.Selected);
            Assert.AreNotEqual(second.Id, viewer.Selected);
        }

        [Test]
        public void Clearing_EmptiesTheTraceAndTheCanvas()
        {
            viewer.Attach(trace);
            Cast(firebolt);
            viewer.Refresh();
            Assert.IsTrue(view.NodeViews["launch"].Visited);

            viewer.Clear();

            Assert.AreEqual(0, trace.Count);
            Assert.IsEmpty(viewer.Executions());
            Assert.IsTrue(view.NodeViews.Values.All(n => !n.Visited && n.TraceSummary == string.Empty && !Dimmed(n)));
        }

        [Test]
        public void OnlyRunsOfTheOpenAbility_AreShown()
        {
            viewer.Attach(trace);
            AbilityDefinition other = AbilityBuilder.MeleeAttack();
            created.Add(other);

            Cast(other);
            ExecutionResult mine = Cast(firebolt);
            viewer.Refresh();

            CollectionAssert.AreEqual(new[] { mine.Id }, viewer.Executions());
            Assert.AreEqual(mine.Id, viewer.Selected);
        }

        [Test]
        public void AfterTheCanvasIsRedrawn_TheTraceIsStillShown()
        {
            viewer.Attach(trace);
            Cast(firebolt);
            projectiles.Hit(0, target.Id);
            viewer.Refresh();

            view.Document.AddNode(NodeTypes.Constant, new Vector2(50f, 50f));

            Assert.IsTrue(view.NodeViews["damage"].Visited);
            StringAssert.Contains("Damage 36", view.NodeViews["damage"].TraceSummary);
        }

        [Test]
        public void Events_ReachTheCanvasWithoutBeingAskedFor_OnTheNextTick()
        {
            viewer.Attach(trace);
            int changed = 0;
            viewer.Changed += () => changed++;

            Cast(firebolt);
            viewer.Refresh();

            Assert.Greater(changed, 0);
            Assert.AreEqual(1, viewer.Executions().Count);
        }

        [Test]
        public void ATraceCanBeReplaced()
        {
            viewer.Attach(trace);
            Cast(firebolt);
            viewer.Refresh();
            RecordingTrace another = new RecordingTrace();

            viewer.Attach(another);

            Assert.AreSame(another, viewer.Trace);
            Assert.IsEmpty(viewer.Executions());
            Assert.IsFalse(view.NodeViews["launch"].Visited);
        }

        // ----- The panel -----

        [Test]
        public void ThePanel_ListsTheRuns_AndTheEventsOfTheOneShown()
        {
            AbilityTracePanel panel = new AbilityTracePanel(viewer, _ => { });
            StringAssert.Contains("nothing attached", panel.Heading);
            viewer.Attach(trace);
            StringAssert.Contains("no runs", panel.Heading);

            ExecutionResult run = Cast(firebolt);
            projectiles.Hit(0, target.Id);
            viewer.Refresh();

            StringAssert.Contains("1 run", panel.Heading);
            Assert.AreEqual(1, panel.RunButtons.Count);
            Assert.AreEqual(viewer.EventsOf(run.Id).Count, panel.Rows.Count);
        }

        [Test]
        public void ClickingAnEvent_SelectsItsNode()
        {
            string selected = null;
            AbilityTracePanel panel = new AbilityTracePanel(viewer, id => selected = id);
            viewer.Attach(trace);
            ExecutionResult run = Cast(firebolt);
            projectiles.Hit(0, target.Id);
            viewer.Refresh();
            TraceEvent damage = viewer.EventsOf(run.Id).First(e => e.Kind == TraceEventKind.NodeRan && e.NodeId == "damage");

            panel.Choose(damage);

            Assert.AreEqual("damage", selected);
            selected = null;
            panel.Choose(viewer.EventsOf(run.Id).First(e => e.Kind == TraceEventKind.ExecutionStarted));
            Assert.IsNull(selected, "An event about the whole run has no node.");
        }

        // ----- Live observation -----

        [Test]
        public void OutsidePlayMode_ThereIsNoGameToObserve()
        {
            Assert.IsFalse(AbilityTraceViewer.TryObserveLive(trace));
            Assert.IsFalse(AbilityTraceViewer.IsObservingLive(trace));
            Assert.DoesNotThrow(() => AbilityTraceViewer.StopObservingLive(trace));
        }
    }
}
