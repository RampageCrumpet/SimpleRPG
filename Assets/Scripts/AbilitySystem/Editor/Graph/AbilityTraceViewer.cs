using SimpleRPG.Abilities.Integration;
using SimpleRPG.Gameplay;
using System;
using System.Collections.Generic;
using System.Linq;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UIElements;

namespace SimpleRPG.Abilities.Editor
{
    /// <summary>
    /// Shows what an ability did on its canvas. It reads a <see cref="RecordingTrace"/> that the real executor fills, so it never simulates
    /// anything itself: it can show a run in a scripted test world, or a run in the live game. It follows the newest run of the open ability
    /// unless the user picks an earlier one.
    /// </summary>
    public sealed class AbilityTraceViewer
    {
        private readonly AbilityGraphView view;

        private readonly AbilityDefinition ability;

        private RecordingTrace trace;

        private bool followLatest = true;

        private ExecutionId selected;

        public AbilityTraceViewer(AbilityGraphView view, AbilityDefinition ability)
        {
            this.view = view;
            this.ability = ability;
            view.Redrawn += Apply;
        }

        /// <summary>
        /// Stops listening to the trace and the canvas. Called when the window builds a new viewer to replace this one.
        /// </summary>
        public void Dispose()
        {
            if (trace != null) trace.Recorded -= OnRecorded;
            view.Redrawn -= Apply;
        }

        /// <summary>Raised when the runs on offer or the one shown change.</summary>
        public event Action Changed;

        /// <summary>The trace being read, or null.</summary>
        public RecordingTrace Trace => trace;

        /// <summary>The run being shown, or an invalid ID if none.</summary>
        public ExecutionId Selected => selected;

        /// <summary>True if the newest run is shown as soon as it starts.</summary>
        public bool FollowLatest => followLatest;

        /// <summary>
        /// Starts reading a trace. Anything already shown is replaced.
        /// </summary>
        public void Attach(RecordingTrace newTrace)
        {
            if (ReferenceEquals(trace, newTrace)) return;

            if (trace != null) trace.Recorded -= OnRecorded;
            trace = newTrace;
            if (trace != null) trace.Recorded += OnRecorded;

            followLatest = true;
            selected = default;
            Refresh();
        }

        /// <summary>
        /// The runs of this ability in the trace, oldest first.
        /// </summary>
        public IReadOnlyList<ExecutionId> Executions()
        {
            if (trace == null) return new List<ExecutionId>();

            return trace.Events.Where(Matches).Select(e => e.Execution).Distinct().ToList();
        }

        /// <summary>
        /// The kept events of one run of this ability, in order.
        /// </summary>
        public List<TraceEvent> EventsOf(ExecutionId execution) => trace == null
            ? new List<TraceEvent>()
            : trace.ForExecution(execution).Where(Matches).ToList();

        /// <summary>
        /// Shows one run, and stops following the newest.
        /// </summary>
        public void Select(ExecutionId execution)
        {
            followLatest = false;
            selected = execution;
            Refresh();
        }

        /// <summary>
        /// Shows the newest run, and follows new ones.
        /// </summary>
        public void ShowLatest()
        {
            followLatest = true;
            Refresh();
        }

        /// <summary>
        /// Forgets the recorded events and clears the canvas.
        /// </summary>
        public void Clear()
        {
            trace?.Clear();
            selected = default;
            followLatest = true;
            Refresh();
        }

        // Several events arrive per run; the canvas is redrawn once, on the next tick.
        private bool refreshQueued;

        private void OnRecorded(TraceEvent traceEvent)
        {
            if (!Matches(traceEvent) || refreshQueued) return;

            refreshQueued = true;
            view.schedule.Execute(() =>
            {
                refreshQueued = false;
                Refresh();
            });
        }

        private bool Matches(TraceEvent traceEvent) =>
            ability.Id.IsNone ? traceEvent.AbilityName == ability.name : traceEvent.Ability == ability.Id;

        /// <summary>
        /// Brings the canvas and the list of runs up to date with the trace.
        /// </summary>
        public void Refresh()
        {
            IReadOnlyList<ExecutionId> executions = Executions();
            if (followLatest)
            {
                selected = executions.Count > 0 ? executions[executions.Count - 1] : default;
            }
            else if (selected.IsValid && !executions.Contains(selected))
            {
                // The run was pushed out of the trace by newer ones.
                selected = default;
            }

            Apply();
            Changed?.Invoke();
        }

        private void Apply()
        {
            List<TraceEvent> events = selected.IsValid ? EventsOf(selected) : new List<TraceEvent>();
            foreach (KeyValuePair<string, AbilityNodeView> pair in view.NodeViews)
            {
                pair.Value.SetTrace(events.Where(e => e.NodeId == pair.Key).ToList(), events.Count > 0);
            }
        }

        /// <summary>
        /// Points the trace of the running game at this viewer, so it shows real play. Only works in play mode on the server or host.
        /// </summary>
        /// <returns>True if the game's executor now reports to <paramref name="recording"/>.</returns>
        public static bool TryObserveLive(RecordingTrace recording)
        {
            ExecutionServices services = LiveServices();
            if (services == null) return false;

            services.Trace = recording;
            return true;
        }

        /// <summary>
        /// Stops the running game reporting to a trace, if it is the one attached.
        /// </summary>
        public static void StopObservingLive(RecordingTrace recording)
        {
            ExecutionServices services = LiveServices();
            if (services != null && ReferenceEquals(services.Trace, recording)) services.Trace = null;
        }

        /// <summary>True if the game's executor currently reports to this trace.</summary>
        public static bool IsObservingLive(RecordingTrace recording)
        {
            ExecutionServices services = LiveServices();
            return services != null && ReferenceEquals(services.Trace, recording);
        }

        // Asking for the services builds them if they do not exist, so only ask while a host or server is really running.
        private static ExecutionServices LiveServices()
        {
            NetworkManager manager = NetworkManager.Singleton;
            if (!Application.isPlaying || manager == null || !manager.IsListening || !manager.IsServer) return null;

            return AbilityServices.Current?.Execution;
        }
    }

    /// <summary>
    /// The list of runs of the open ability and the events of the one shown. Clicking an event selects its node.
    /// </summary>
    public sealed class AbilityTracePanel : VisualElement
    {
        private readonly AbilityTraceViewer viewer;

        private readonly Action<string> selectNode;

        private readonly Label heading = new Label("Trace");

        private readonly VisualElement runs = new VisualElement { style = { flexDirection = FlexDirection.Row, flexWrap = Wrap.Wrap } };

        private readonly VisualElement list = new VisualElement();

        public AbilityTracePanel(AbilityTraceViewer viewer, Action<string> selectNode)
        {
            this.viewer = viewer;
            this.selectNode = selectNode;
            style.maxHeight = 140;
            style.flexShrink = 0;
            style.flexGrow = 1;
            style.paddingLeft = 8;
            style.paddingRight = 8;
            style.paddingTop = 3;
            style.borderLeftWidth = 1;
            style.borderLeftColor = new Color(0f, 0f, 0f, 0.4f);

            heading.style.unityFontStyleAndWeight = FontStyle.Bold;
            Add(heading);
            Add(runs);
            ScrollView scroll = new ScrollView();
            scroll.Add(list);
            Add(scroll);

            viewer.Changed += Rebuild;
            Rebuild();
        }

        /// <summary>The heading, which says how many runs there are.</summary>
        public string Heading => heading.text;

        /// <summary>One button per run on offer.</summary>
        public IReadOnlyList<Button> RunButtons => runs.Query<Button>().ToList();

        /// <summary>One row per event of the run shown.</summary>
        public IReadOnlyList<Button> Rows => list.Query<Button>().ToList();

        /// <summary>
        /// What clicking an event does: select its node, when it has one.
        /// </summary>
        public void Choose(TraceEvent traceEvent)
        {
            if (traceEvent.NodeId != null) selectNode(traceEvent.NodeId);
        }

        private void Rebuild()
        {
            runs.Clear();
            list.Clear();

            IReadOnlyList<ExecutionId> executions = viewer.Executions();
            if (viewer.Trace == null)
            {
                heading.text = "Trace: nothing attached";
                return;
            }

            heading.text = executions.Count == 0 ? "Trace: no runs yet" : "Trace: " + executions.Count + (executions.Count == 1 ? " run" : " runs");

            foreach (ExecutionId execution in executions)
            {
                ExecutionId chosen = execution;
                Button button = new Button(() => viewer.Select(chosen)) { text = "#" + execution.Value };
                button.userData = execution;
                button.style.opacity = execution == viewer.Selected ? 1f : 0.6f;
                runs.Add(button);
            }

            if (!viewer.Selected.IsValid) return;

            foreach (TraceEvent traceEvent in viewer.EventsOf(viewer.Selected))
            {
                TraceEvent shown = traceEvent;
                string where = shown.NodeId == null ? string.Empty : "[" + shown.NodeId + "] ";
                Button row = new Button(() => Choose(shown)) { text = shown.Kind + " " + where + (shown.Text ?? (shown.Kind == TraceEventKind.Value ? shown.Number.ToString("0.###") : string.Empty)) };
                row.style.unityTextAlign = TextAnchor.MiddleLeft;
                row.style.marginLeft = 0;
                row.style.marginRight = 0;
                row.style.color = shown.Kind == TraceEventKind.Diagnostic || shown.Kind == TraceEventKind.ExecutionStopped ? new Color(0.9f, 0.55f, 0.35f) : shown.Kind == TraceEventKind.NodeRan ? new Color(0.5f, 0.85f, 0.55f) : new Color(0.8f, 0.8f, 0.8f);
                row.userData = shown;
                list.Add(row);
            }
        }
    }
}
