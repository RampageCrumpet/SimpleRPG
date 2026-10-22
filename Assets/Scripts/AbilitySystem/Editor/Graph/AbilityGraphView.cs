using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Experimental.GraphView;
using UnityEngine;
using UnityEngine.UIElements;

namespace SimpleRPG.Abilities.Editor
{
    /// <summary>
    /// The canvas for one ability. It is a view over an <see cref="AbilityGraphDocument"/>: it draws the asset's nodes and connections, turns the
    /// user's gestures into document operations, and is rebuilt from the asset whenever the document changes. It keeps no graph data of its own.
    /// </summary>
    public sealed class AbilityGraphView : GraphView
    {
        private readonly AbilityGraphDocument document;

        private readonly Dictionary<string, AbilityNodeView> nodeViews = new Dictionary<string, AbilityNodeView>();

        private bool applyingGesture;

        private bool rebuilding;

        private CompileResult shownResult;

        public AbilityGraphView(AbilityGraphDocument document)
        {
            this.document = document;

            style.flexGrow = 1;
            SetupZoom(ContentZoomer.DefaultMinScale, ContentZoomer.DefaultMaxScale);
            this.AddManipulator(new ContentDragger());
            this.AddManipulator(new SelectionDragger());
            this.AddManipulator(new RectangleSelector());

            GridBackground grid = new GridBackground();
            Insert(0, grid);
            grid.StretchToParentSize();

            graphViewChanged = OnGraphViewChanged;
            serializeGraphElements = SerializeForClipboard;
            canPasteSerializedData = data => data == AbilityGraphClipboard.Marker && AbilityGraphClipboard.Current != null && !AbilityGraphClipboard.Current.IsEmpty;
            unserializeAndPaste = (operation, data) => PasteClipboard();
            document.Changed += OnDocumentChanged;
            Undo.undoRedoPerformed += OnUndoRedo;
            RegisterCallback<DetachFromPanelEvent>(_ =>
            {
                document.Changed -= OnDocumentChanged;
                Undo.undoRedoPerformed -= OnUndoRedo;
            });

            Rebuild();

            // Edits made in the settings panel change the asset without going through the document, so the check for new problems also runs on a timer.
            schedule.Execute(RefreshValidation).Every(250);
        }

        public AbilityGraphDocument Document => document;

        /// <summary>Raised after every redraw, when the node views are new and anything shown on them has to be put back.</summary>
        public event Action Redrawn;

        /// <summary>Raised when the ability's problems change, with the new result.</summary>
        public event Action<CompileResult> ValidationChanged;

        /// <summary>The problems currently shown.</summary>
        public CompileResult Validation => shownResult;

        /// <summary>
        /// Checks the ability and marks the nodes and ports that have problems. Cheap when nothing has changed, because the compiled result is kept
        /// until the asset changes.
        /// </summary>
        public void RefreshValidation()
        {
            CompileResult result = document.Definition.Compile(document.Registry);
            if (ReferenceEquals(result, shownResult)) return;

            shownResult = result;
            foreach (KeyValuePair<string, AbilityNodeView> pair in nodeViews)
            {
                pair.Value.SetProblems(result.Report.Messages.Where(m => m.NodeId == pair.Key).ToList());
            }

            ValidationChanged?.Invoke(result);
        }

        /// <summary>
        /// Selects exactly the given nodes.
        /// </summary>
        public void SelectNodes(IEnumerable<string> nodeIds)
        {
            ClearSelection();
            foreach (string id in nodeIds)
            {
                if (nodeViews.TryGetValue(id, out AbilityNodeView view)) AddToSelection(view);
            }
        }

        /// <summary>
        /// Copies the selected nodes to the clipboard.
        /// </summary>
        public void CopySelection() => document.Copy(SelectedNodeIds);

        /// <summary>
        /// Pastes the clipboard and selects what was pasted.
        /// </summary>
        public IReadOnlyList<string> PasteClipboard()
        {
            IReadOnlyList<string> pasted = document.PasteCurrent();
            SelectNodes(pasted);
            return pasted;
        }

        /// <summary>
        /// Duplicates the selected nodes and selects the duplicates.
        /// </summary>
        public IReadOnlyList<string> DuplicateSelection()
        {
            IReadOnlyList<string> duplicated = document.Duplicate(SelectedNodeIds);
            SelectNodes(duplicated);
            return duplicated;
        }

        private string SerializeForClipboard(IEnumerable<GraphElement> elements)
        {
            AbilityGraphClipboard copy = document.Copy(elements.OfType<AbilityNodeView>().Select(n => n.NodeId));
            return copy.IsEmpty ? string.Empty : AbilityGraphClipboard.Marker;
        }

        /// <summary>Raised when the set of selected nodes changes, and after every redraw.</summary>
        public event Action SelectionChanged;

        /// <summary>The IDs of the selected nodes.</summary>
        public IReadOnlyList<string> SelectedNodeIds => selection.OfType<AbilityNodeView>().Select(n => n.NodeId).ToList();

        public override void AddToSelection(ISelectable selectable)
        {
            base.AddToSelection(selectable);
            RaiseSelectionChanged();
        }

        public override void RemoveFromSelection(ISelectable selectable)
        {
            base.RemoveFromSelection(selectable);
            RaiseSelectionChanged();
        }

        public override void ClearSelection()
        {
            base.ClearSelection();
            RaiseSelectionChanged();
        }

        private void RaiseSelectionChanged()
        {
            if (!rebuilding) SelectionChanged?.Invoke();
        }

        /// <summary>The node views by node ID, for tests and tools.</summary>
        public IReadOnlyDictionary<string, AbilityNodeView> NodeViews => nodeViews;

        /// <summary>The connections currently drawn.</summary>
        public IEnumerable<Edge> DrawnEdges => edges.ToList();

        /// <summary>
        /// Draws the asset again from scratch, keeping the selection and the viewpoint.
        /// </summary>
        public void Rebuild()
        {
            HashSet<string> selected = new HashSet<string>(selection.OfType<AbilityNodeView>().Select(n => n.NodeId));

            rebuilding = true;
            graphViewChanged = null;

            // The old views are about to be thrown away, so they must not stay selected next to their replacements.
            base.ClearSelection();
            DeleteElements(graphElements.ToList());
            nodeViews.Clear();

            IReadOnlyDictionary<string, Vector2> positions = document.DisplayPositions();
            foreach (AbilityNodeRecord record in document.Definition.Nodes)
            {
                AbilityNodeView view = new AbilityNodeView(record, document.Registry);
                view.SetPosition(new Rect(positions[record.NodeId], Vector2.zero));
                AddElement(view);
                nodeViews[record.NodeId] = view;
                if (selected.Contains(record.NodeId)) AddToSelection(view);
            }

            foreach (AbilityEdge edge in document.Definition.Edges)
            {
                // A connection to a missing node or port is left undrawn; the compiler reports it.
                if (nodeViews.TryGetValue(edge.FromNodeId, out AbilityNodeView from) && nodeViews.TryGetValue(edge.ToNodeId, out AbilityNodeView to)
                    && from.TryGetPort(edge.FromPortId, out Port output) && to.TryGetPort(edge.ToPortId, out Port input))
                {
                    AddElement(output.ConnectTo(input));
                }
            }

            graphViewChanged = OnGraphViewChanged;
            rebuilding = false;
            shownResult = null;
            RefreshValidation();
            Redrawn?.Invoke();
            SelectionChanged?.Invoke();
        }

        /// <summary>
        /// Zooms and pans to show the whole graph.
        /// </summary>
        public void FrameGraph() => FrameAll();

        /// <inheritdoc/>
        public override List<Port> GetCompatiblePorts(Port startPort, NodeAdapter nodeAdapter)
        {
            List<Port> compatible = new List<Port>();
            if (!(startPort.node is AbilityNodeView startNode) || !(startPort.userData is PortDescriptor start))
            {
                return compatible;
            }

            foreach (AbilityNodeView other in nodeViews.Values)
            {
                if (other == startNode) continue;

                foreach (Port port in other.Ports)
                {
                    PortDescriptor candidate = (PortDescriptor)port.userData;
                    bool ok = start.Direction == PortDirection.Output
                        ? PortCompatibility.CanConnect(start, candidate, out _)
                        : PortCompatibility.CanConnect(candidate, start, out _);
                    if (ok) compatible.Add(port);
                }
            }

            return compatible;
        }

        /// <summary>
        /// Opens the palette at a screen position and adds the chosen node there.
        /// </summary>
        public void OpenPalette(Vector2 screenPosition, Func<Vector2, Vector2> screenToGraph)
        {
            NodePalette palette = NodePalette.Obtain();
            palette.Initialize(document.Registry, typeId =>
            {
                document.AddNode(typeId, screenToGraph(screenPosition));
            });
            SearchWindow.Open(new SearchWindowContext(screenPosition), palette);
        }

        // A canvas whose ability asset has been destroyed (a closed window, a finished test) lets go of the global and document events.
        private bool ReleaseIfAbandoned()
        {
            if (document.Definition != null) return false;

            document.Changed -= OnDocumentChanged;
            Undo.undoRedoPerformed -= OnUndoRedo;
            return true;
        }

        private void OnDocumentChanged()
        {
            if (ReleaseIfAbandoned()) return;

            if (applyingGesture)
            {
                // The view is in the middle of handling a gesture; rebuilding now would pull elements out from under it.
                schedule.Execute(Rebuild);
            }
            else
            {
                Rebuild();
            }
        }

        private void OnUndoRedo()
        {
            if (ReleaseIfAbandoned()) return;

            // Undo restores the asset directly, so the compiled graph and the canvas both need refreshing.
            document.NotifyChanged();
        }

        private GraphViewChange OnGraphViewChanged(GraphViewChange change)
        {
            applyingGesture = true;
            try
            {
                if (change.elementsToRemove != null)
                {
                    foreach (Edge edge in change.elementsToRemove.OfType<Edge>())
                    {
                        if (TryDescribe(edge, out string fromNode, out string fromPort, out string toNode, out string toPort))
                        {
                            document.Disconnect(fromNode, fromPort, toNode, toPort);
                        }
                    }

                    document.RemoveNodes(change.elementsToRemove.OfType<AbilityNodeView>().Select(n => n.NodeId));
                }

                if (change.edgesToCreate != null)
                {
                    foreach (Edge edge in change.edgesToCreate)
                    {
                        if (TryDescribe(edge, out string fromNode, out string fromPort, out string toNode, out string toPort))
                        {
                            document.Connect(fromNode, fromPort, toNode, toPort, out string reason);
                        }
                    }

                    // The document redraws the connection itself, so the view must not add its own copy.
                    change.edgesToCreate.Clear();
                }

                if (change.movedElements != null)
                {
                    Dictionary<string, Vector2> moved = new Dictionary<string, Vector2>();
                    foreach (AbilityNodeView node in change.movedElements.OfType<AbilityNodeView>())
                    {
                        moved[node.NodeId] = node.GetPosition().position;
                    }

                    document.MoveNodes(moved);
                }
            }
            finally
            {
                applyingGesture = false;
            }

            return change;
        }

        private static bool TryDescribe(Edge edge, out string fromNode, out string fromPort, out string toNode, out string toPort)
        {
            fromNode = fromPort = toNode = toPort = null;
            if (edge.output?.node is AbilityNodeView from && edge.input?.node is AbilityNodeView to
                && edge.output.userData is PortDescriptor output && edge.input.userData is PortDescriptor input)
            {
                fromNode = from.NodeId;
                fromPort = output.Id;
                toNode = to.NodeId;
                toPort = input.Id;
                return true;
            }

            return false;
        }
    }

    /// <summary>
    /// One node on the canvas. Its title, colour and ports all come from the node type's descriptor.
    /// </summary>
    public sealed class AbilityNodeView : Node
    {
        private readonly Dictionary<string, Port> ports = new Dictionary<string, Port>();

        private readonly Dictionary<string, Color> portColors = new Dictionary<string, Color>();

        private readonly Label badge = new Label();

        private readonly string baseTooltip;

        private readonly Label traceLabel = new Label();

        private string problemText = string.Empty;

        private string traceText = string.Empty;

        private Color? titleColor;

        private static readonly Color ErrorColor = new Color(0.85f, 0.2f, 0.2f);

        private static readonly Color WarningColor = new Color(0.9f, 0.65f, 0.15f);

        public AbilityNodeView(AbilityNodeRecord record, AbilityNodeRegistry registry)
        {
            NodeId = record.NodeId;
            TypeId = record.TypeId;
            viewDataKey = record.NodeId;

            if (registry.TryGet(record.TypeId, out IAbilityNodeDescriptor descriptor))
            {
                title = descriptor.DisplayName;
                tooltip = descriptor.Description;
                baseTooltip = descriptor.Description;
                AddToClassList("ability-node");
                foreach (PortDescriptor port in descriptor.Ports)
                {
                    AddPort(port);
                }
            }
            else
            {
                // Kept on the canvas so it can be seen and deleted; the compiler reports the unknown type.
                title = "Unknown: " + record.TypeId;
                tooltip = "No node type is registered with this ID.";
                baseTooltip = tooltip;
                titleContainer.style.backgroundColor = new Color(0.55f, 0.15f, 0.15f);
            }

            badge.style.display = DisplayStyle.None;
            badge.style.marginLeft = 6;
            badge.style.unityFontStyleAndWeight = FontStyle.Bold;
            titleContainer.Add(badge);

            traceLabel.style.display = DisplayStyle.None;
            extensionContainer.Add(traceLabel);

            RefreshExpandedState();
            RefreshPorts();
        }

        public string NodeId { get; }

        public string TypeId { get; }

        public IEnumerable<Port> Ports => ports.Values;

        /// <summary>The problems the compiler found with this node and its ports.</summary>
        public IReadOnlyList<ValidationMessage> Problems { get; private set; } = new List<ValidationMessage>();

        /// <summary>What a run did at this node, in order. Empty when no run is shown or the run never reached the node.</summary>
        public IReadOnlyList<TraceEvent> TraceEvents { get; private set; } = new List<TraceEvent>();

        /// <summary>True if the shown run reached this node.</summary>
        public bool Visited => TraceEvents.Any(e => e.Kind == TraceEventKind.NodeRan);

        /// <summary>The line shown under the title while a run is shown: the node's last value or outcome. Empty otherwise.</summary>
        public string TraceSummary => traceLabel.style.display == DisplayStyle.None ? string.Empty : traceLabel.text;

        /// <summary>
        /// Shows what a run did at this node: operations that ran are tinted and numbered, nodes the run never reached are dimmed, and the last
        /// value or outcome is written under the title. A run that did nothing here and no run at all both clear it.
        /// </summary>
        /// <param name="events">The run's events for this node.</param>
        /// <param name="runShown">True if a run is being shown, so nodes it skipped are dimmed.</param>
        public void SetTrace(IReadOnlyList<TraceEvent> events, bool runShown)
        {
            TraceEvents = events;
            if (!titleColor.HasValue) titleColor = titleContainer.resolvedStyle.backgroundColor;

            List<TraceEvent> ran = events.Where(e => e.Kind == TraceEventKind.NodeRan).ToList();
            TraceEvent? last = events.Where(e => e.Kind == TraceEventKind.Value || e.Kind == TraceEventKind.Outcome).Cast<TraceEvent?>().LastOrDefault();

            style.opacity = runShown && events.Count == 0 ? 0.4f : 1f;
            titleContainer.style.backgroundColor = ran.Count > 0 ? new Color(0.2f, 0.45f, 0.25f) : new StyleColor(StyleKeyword.Null);

            traceLabel.style.display = last.HasValue ? DisplayStyle.Flex : DisplayStyle.None;
            traceLabel.text = last.HasValue ? Describe(last.Value) : string.Empty;
            traceLabel.style.whiteSpace = WhiteSpace.Normal;
            traceLabel.style.maxWidth = 220;
            traceLabel.style.paddingLeft = traceLabel.style.paddingRight = 4;

            traceText = events.Count == 0
                ? string.Empty
                : (ran.Count > 0 ? "Ran " + (ran.Count == 1 ? "once (step " + ran[0].Number + ")" : ran.Count + " times (first at step " + ran[0].Number + ")") + "\n" : string.Empty)
                    + string.Join("\n", events.Where(e => e.Kind != TraceEventKind.NodeRan).Select(Describe));
            RefreshTooltip();
        }

        private static string Describe(TraceEvent e)
        {
            switch (e.Kind)
            {
                case TraceEventKind.Value: return "= " + (e.Text ?? e.Number.ToString("0.###"));
                case TraceEventKind.Diagnostic: return "Problem: " + e.Text;
                default: return e.Text ?? e.Kind.ToString();
            }
        }

        private void RefreshTooltip()
        {
            List<string> parts = new List<string>();
            if (!string.IsNullOrEmpty(baseTooltip)) parts.Add(baseTooltip);
            if (problemText.Length > 0) parts.Add(problemText);
            if (traceText.Length > 0) parts.Add("Last run:\n" + traceText);
            tooltip = string.Join("\n\n", parts);
        }

        /// <summary>The badge text shown on the node, empty when it has no problems.</summary>
        public string BadgeText => badge.style.display == DisplayStyle.None ? string.Empty : badge.text;

        /// <summary>
        /// Marks the node with its problems: a coloured outline and badge, the messages in its tooltip, and a red connector on any port at fault.
        /// </summary>
        public void SetProblems(IReadOnlyList<ValidationMessage> problems)
        {
            Problems = problems;
            int errors = problems.Count(p => p.Severity == ValidationSeverity.Error);
            int warnings = problems.Count - errors;
            Color? outline = errors > 0 ? ErrorColor : warnings > 0 ? WarningColor : (Color?)null;

            float width = outline.HasValue ? 2f : 0f;
            Color color = outline ?? Color.clear;
            style.borderTopWidth = style.borderBottomWidth = style.borderLeftWidth = style.borderRightWidth = width;
            style.borderTopColor = style.borderBottomColor = style.borderLeftColor = style.borderRightColor = color;

            badge.style.display = problems.Count > 0 ? DisplayStyle.Flex : DisplayStyle.None;
            badge.text = errors > 0 ? (errors == 1 ? "1 error" : errors + " errors") : (warnings == 1 ? "1 warning" : warnings + " warnings");
            badge.style.color = color;

            problemText = problems.Count == 0
                ? string.Empty
                : string.Join("\n", problems.Select(p => (p.Severity == ValidationSeverity.Error ? "Error: " : "Warning: ") + p.Text));
            RefreshTooltip();

            foreach (KeyValuePair<string, Port> pair in ports)
            {
                bool portFault = problems.Any(p => p.PortId == pair.Key && p.Severity == ValidationSeverity.Error);
                pair.Value.portColor = portFault ? ErrorColor : portColors[pair.Key];
            }
        }

        public bool TryGetPort(string portId, out Port port) => ports.TryGetValue(portId, out port);

        private void AddPort(PortDescriptor descriptor)
        {
            bool input = descriptor.Direction == PortDirection.Input;
            Port port = InstantiatePort(
                Orientation.Horizontal,
                input ? Direction.Input : Direction.Output,
                PortCompatibility.TakesOneConnection(descriptor) ? Port.Capacity.Single : Port.Capacity.Multi,
                descriptor.Kind == PortKind.Flow ? typeof(FlowSignal) : DataTypeOf(descriptor.DataType));
            port.portName = descriptor.DisplayName;
            port.userData = descriptor;
            port.portColor = ColorOf(descriptor);
            portColors[descriptor.Id] = port.portColor;
            port.tooltip = descriptor.Kind == PortKind.Flow ? "Flow" : descriptor.DataType + (descriptor.Required ? " (required)" : string.Empty);
            (input ? inputContainer : outputContainer).Add(port);
            ports[descriptor.Id] = port;
        }

        // Ports of different kinds get different types, so the canvas's own checks agree with PortCompatibility.
        private static Type DataTypeOf(DataType type)
        {
            switch (type)
            {
                case DataType.Float: return typeof(float);
                case DataType.Entity: return typeof(EntityValue);
                case DataType.EntitySet: return typeof(EntitySetValue);
                default: return typeof(object);
            }
        }

        private static Color ColorOf(PortDescriptor port)
        {
            if (port.Kind == PortKind.Flow) return new Color(0.9f, 0.9f, 0.9f);

            switch (port.DataType)
            {
                case DataType.Float: return new Color(0.55f, 0.85f, 0.6f);
                case DataType.Entity: return new Color(0.95f, 0.65f, 0.35f);
                case DataType.EntitySet: return new Color(0.9f, 0.45f, 0.55f);
                default: return Color.gray;
            }
        }

        // Marker types that only give ports of a kind their own identity on the canvas.
        private sealed class FlowSignal { }

        private sealed class EntityValue { }

        private sealed class EntitySetValue { }
    }

    /// <summary>
    /// The searchable list of node types, built from the registry so a newly registered node type appears without any editor change.
    /// </summary>
    public sealed class NodePalette : ScriptableObject, ISearchWindowProvider
    {
        private AbilityNodeRegistry registry;

        private Action<string> onChosen;

        private static NodePalette shared;

        /// <summary>
        /// The palette the canvas opens. One instance is reused, so opening the palette again and again does not leave objects behind.
        /// </summary>
        public static NodePalette Obtain()
        {
            if (shared == null)
            {
                shared = CreateInstance<NodePalette>();
                shared.hideFlags = HideFlags.HideAndDontSave;
            }

            return shared;
        }

        public void Initialize(AbilityNodeRegistry registry, Action<string> onChosen)
        {
            this.registry = registry;
            this.onChosen = onChosen;
        }

        public List<SearchTreeEntry> CreateSearchTree(SearchWindowContext context)
        {
            List<SearchTreeEntry> tree = new List<SearchTreeEntry> { new SearchTreeGroupEntry(new GUIContent("Add Node"), 0) };
            foreach (IGrouping<string, IAbilityNodeDescriptor> category in registry.Search(null).GroupBy(d => d.Category ?? "Other"))
            {
                tree.Add(new SearchTreeGroupEntry(new GUIContent(category.Key), 1));
                foreach (IAbilityNodeDescriptor descriptor in category)
                {
                    tree.Add(new SearchTreeEntry(new GUIContent(descriptor.DisplayName, descriptor.Description)) { level = 2, userData = descriptor.TypeId });
                }
            }

            return tree;
        }

        public bool OnSelectEntry(SearchTreeEntry entry, SearchWindowContext context)
        {
            onChosen?.Invoke((string)entry.userData);
            return true;
        }
    }
}
