using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace SimpleRPG.Abilities.Editor
{
    /// <summary>
    /// The window for editing one ability graph. Double-click an ability asset, or use the asset's context menu, to open it.
    /// </summary>
    public sealed class AbilityGraphWindow : EditorWindow
    {
        [SerializeField]
        private AbilityDefinition definition;

        private AbilityGraphView view;

        private AbilityNodeInspector inspector;

        private AbilityProblemsPanel problems;

        private AbilityTraceViewer traceViewer;

        private AbilityTracePanel tracePanel;

        private readonly RecordingTrace liveTrace = new RecordingTrace();

        private bool observeLive;

        /// <summary>The canvas, for tests and tools.</summary>
        public AbilityGraphView View => view;

        /// <summary>The settings panel, for tests and tools.</summary>
        public AbilityNodeInspector Inspector => inspector;

        /// <summary>Shows traces on the canvas, for tests and tools.</summary>
        public AbilityTraceViewer TraceViewer => traceViewer;

        /// <summary>The list of runs and their events, for tests and tools.</summary>
        public AbilityTracePanel TracePanel => tracePanel;

        /// <summary>
        /// Shows the runs recorded by a trace. A test world attaches the trace it gave its executor; the live game uses the window's own.
        /// </summary>
        public void AttachTrace(RecordingTrace trace) => traceViewer.Attach(trace);

        /// <summary>
        /// Runs the open ability in a fresh scripted test world and shows what it did on the canvas. Nothing in the game or the project changes.
        /// </summary>
        /// <returns>What happened. An ability with errors is not run.</returns>
        public TestWorldRun RunInTestWorld()
        {
            AbilityTestWorld world = new AbilityTestWorld();
            TestWorldRun run = world.Run(definition);
            traceViewer.Attach(world.Trace);
            traceViewer.ShowLatest();
            problems.ShowNotice(run.Summary);
            return run;
        }

        /// <summary>
        /// Starts or stops showing what the running game does with this ability. Needs play mode on a host or server.
        /// </summary>
        /// <returns>True if the game is now being observed (or, when turning off, always true).</returns>
        public bool SetObserveLive(bool observe)
        {
            if (!observe)
            {
                observeLive = false;
                AbilityTraceViewer.StopObservingLive(liveTrace);
                return true;
            }

            traceViewer.Attach(liveTrace);
            observeLive = AbilityTraceViewer.TryObserveLive(liveTrace);
            return observeLive;
        }

        /// <summary>The list of problems, for tests and tools.</summary>
        public AbilityProblemsPanel Problems => problems;

        /// <summary>
        /// Opens the editor for an ability, reusing the window if it already shows that ability.
        /// </summary>
        public static AbilityGraphWindow Open(AbilityDefinition definition)
        {
            foreach (AbilityGraphWindow existing in Resources.FindObjectsOfTypeAll<AbilityGraphWindow>())
            {
                if (existing.definition == definition)
                {
                    existing.Focus();
                    return existing;
                }
            }

            AbilityGraphWindow window = CreateInstance<AbilityGraphWindow>();
            window.definition = definition;
            window.Show();
            window.Build();
            return window;
        }

        [OnOpenAsset]
        private static bool OnOpenAsset(UnityEngine.EntityId entityId, int line)
        {
            if (EditorUtility.EntityIdToObject(entityId) is AbilityDefinition ability)
            {
                Open(ability);
                return true;
            }

            return false;
        }

        [MenuItem("CONTEXT/AbilityDefinition/Open Graph Editor")]
        private static void OpenFromContext(MenuCommand command) => Open((AbilityDefinition)command.context);

        private void OnEnable()
        {
            // After a domain reload the window comes back with its serialized ability and rebuilds around it.
            if (definition != null)
            {
                Build();
            }
        }

        private void Build()
        {
            rootVisualElement.Clear();
            if (definition == null)
            {
                return;
            }

            titleContent = new GUIContent(definition.name + " (Graph)");

            AbilityGraphDocument document = new AbilityGraphDocument(definition);
            view = new AbilityGraphView(document);
            view.nodeCreationRequest = context => view.OpenPalette(context.screenMousePosition, ScreenToGraph);

            Toolbar toolbar = new Toolbar();
            toolbar.Add(new ToolbarButton(document.Save) { text = "Save" });
            toolbar.Add(new ToolbarButton(document.AutoLayout) { text = "Auto Layout" });
            toolbar.Add(new ToolbarButton(view.FrameGraph) { text = "Frame All" });
            toolbar.Add(new ToolbarButton(() => RunInTestWorld()) { text = "Run in Test World" });
            toolbar.Add(new ToolbarSpacer());
            Label hint = new Label("Space or right-click: add a node. Ctrl+C, V, D: copy, paste, duplicate") { style = { unityTextAlign = TextAnchor.MiddleLeft, marginLeft = 6 } };
            toolbar.Add(hint);

            inspector = new AbilityNodeInspector(document);
            view.SelectionChanged += ShowSelection;

            VisualElement body = new VisualElement { style = { flexDirection = FlexDirection.Row, flexGrow = 1 } };
            body.Add(view);
            body.Add(inspector);

            Action<string> showNode = nodeId =>
            {
                view.SelectNodes(new[] { nodeId });
                view.FrameSelection();
            };

            traceViewer?.Dispose();
            traceViewer = new AbilityTraceViewer(view, definition);
            tracePanel = new AbilityTracePanel(traceViewer, showNode);
            problems = new AbilityProblemsPanel(showNode);
            view.ValidationChanged += problems.Show;
            problems.Show(view.Validation);
            document.Refused += problems.ShowNotice;
            document.Changed += () => problems.ShowNotice(null);

            rootVisualElement.Add(toolbar);
            rootVisualElement.Add(body);

            VisualElement bottom = new VisualElement { style = { flexDirection = FlexDirection.Row, flexShrink = 0 } };
            problems.style.flexGrow = 1;
            bottom.Add(problems);
            bottom.Add(tracePanel);
            rootVisualElement.Add(bottom);

            Toggle live = new Toggle("Observe live") { tooltip = "In play mode on a host, show what the running game does with this ability." };
            live.RegisterValueChangedCallback(change =>
            {
                if (!SetObserveLive(change.newValue)) live.SetValueWithoutNotify(false);
            });
            toolbar.Insert(3, live);

            // The game rebuilds its ability services when a session restarts, so a live trace is attached again while the toggle stays on.
            rootVisualElement.schedule.Execute(() =>
            {
                if (observeLive && !AbilityTraceViewer.IsObservingLive(liveTrace)) observeLive = AbilityTraceViewer.TryObserveLive(liveTrace);
            }).Every(500);

            // Ctrl+S saves the asset.
            rootVisualElement.RegisterCallback<KeyDownEvent>(evt =>
            {
                if (evt.actionKey && evt.keyCode == KeyCode.S)
                {
                    document.Save();
                    evt.StopPropagation();
                }
            });

            view.schedule.Execute(view.FrameGraph).ExecuteLater(50);
        }

        // One selected node shows its settings; anything else shows the ability's own details.
        private void ShowSelection()
        {
            IReadOnlyList<string> selected = view.SelectedNodeIds;
            inspector.Show(selected.Count == 1 ? selected[0] : null);
        }

        private Vector2 ScreenToGraph(Vector2 screenPosition)
        {
            // The window's root is at the window's origin, so a point relative to the window is already in the root's world space.
            return view.contentViewContainer.WorldToLocal(screenPosition - position.position);
        }
    }
}
