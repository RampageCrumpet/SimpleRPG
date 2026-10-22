using SimpleRPG.Gameplay;
using System;
using System.Collections.Generic;
using System.Linq;

namespace SimpleRPG.Abilities
{
    /// <summary>
    /// The outcome of compiling a graph: the runnable ability if it is valid, and every problem found either way.
    /// </summary>
    public sealed class CompileResult
    {
        public CompileResult(CompiledAbility ability, ValidationReport report)
        {
            Ability = ability;
            Report = report;
        }

        /// <summary>The compiled ability, or null if the graph has errors.</summary>
        public CompiledAbility Ability { get; }

        public ValidationReport Report { get; }

        public bool Success => Ability != null;
    }

    /// <summary>
    /// Checks an ability graph and turns it into a <see cref="CompiledAbility"/>. Every problem is reported against the asset, node and port
    /// it belongs to, and a graph with any error is never compiled.
    /// </summary>
    public static class AbilityCompiler
    {
        public static CompileResult Compile(string assetName, DefinitionId id, IReadOnlyList<AbilityNodeRecord> nodes, IReadOnlyList<AbilityEdge> edges, AbilityNodeRegistry registry)
        {
            Compilation compilation = new Compilation(assetName, id, nodes, edges, registry);
            return compilation.Run();
        }

        private sealed class Sink : IValidationSink
        {
            private readonly Compilation owner;
            private readonly string nodeId;

            public Sink(Compilation owner, string nodeId)
            {
                this.owner = owner;
                this.nodeId = nodeId;
            }

            public void Error(string text, string portId = null) => owner.Add(ValidationSeverity.Error, nodeId, portId, text);

            public void Warning(string text, string portId = null) => owner.Add(ValidationSeverity.Warning, nodeId, portId, text);
        }

        private sealed class Compilation : ICompilationContext
        {
            private readonly string assetName;
            private readonly DefinitionId id;
            private readonly IReadOnlyList<AbilityNodeRecord> nodes;
            private readonly IReadOnlyList<AbilityEdge> edges;
            private readonly AbilityNodeRegistry registry;
            private readonly ValidationReport report = new ValidationReport();

            private readonly Dictionary<string, AbilityNodeRecord> byId = new Dictionary<string, AbilityNodeRecord>(StringComparer.Ordinal);
            private readonly Dictionary<string, IAbilityNodeDescriptor> descriptors = new Dictionary<string, IAbilityNodeDescriptor>(StringComparer.Ordinal);
            private readonly List<AbilityEdge> validEdges = new List<AbilityEdge>();

            // Compile-time state.
            private readonly Dictionary<string, CompiledNode> compiled = new Dictionary<string, CompiledNode>(StringComparer.Ordinal);
            private readonly Dictionary<string, int> operationIndex = new Dictionary<string, int>(StringComparer.Ordinal);
            private readonly List<SnapshotInput> snapshots = new List<SnapshotInput>();
            private string currentNode;

            public Compilation(string assetName, DefinitionId id, IReadOnlyList<AbilityNodeRecord> nodes, IReadOnlyList<AbilityEdge> edges, AbilityNodeRegistry registry)
            {
                this.assetName = assetName;
                this.id = id;
                this.nodes = nodes;
                this.edges = edges;
                this.registry = registry;
            }

            public string NodeId => currentNode;

            public void Add(ValidationSeverity severity, string nodeId, string portId, string text)
            {
                report.Messages.Add(new ValidationMessage(severity, assetName, nodeId, portId, text));
            }

            public CompileResult Run()
            {
                CheckNodes();
                CheckEdges();
                CheckConnections();
                CheckEntry();
                CheckCycles();
                CheckReachabilityAndContext();

                if (report.HasErrors)
                {
                    return new CompileResult(null, report);
                }

                return new CompileResult(Build(), report);
            }

            // ----- Validation -----

            private void CheckNodes()
            {
                foreach (AbilityNodeRecord node in nodes)
                {
                    if (node == null || string.IsNullOrEmpty(node.NodeId))
                    {
                        Add(ValidationSeverity.Error, null, null, "A node has no node ID.");
                        continue;
                    }

                    if (byId.ContainsKey(node.NodeId))
                    {
                        Add(ValidationSeverity.Error, node.NodeId, null, "Two nodes share the ID '" + node.NodeId + "'.");
                        continue;
                    }

                    byId.Add(node.NodeId, node);

                    if (!registry.TryGet(node.TypeId, out IAbilityNodeDescriptor descriptor))
                    {
                        Add(ValidationSeverity.Error, node.NodeId, null, "Unknown node type '" + node.TypeId + "'.");
                        continue;
                    }

                    descriptors.Add(node.NodeId, descriptor);

                    if (node.Config == null || !descriptor.ConfigType.IsInstanceOfType(node.Config))
                    {
                        Add(ValidationSeverity.Error, node.NodeId, null, "The node's settings are missing or are not " + descriptor.ConfigType.Name + ".");
                        continue;
                    }

                    descriptor.Validate(node.Config, new Sink(this, node.NodeId));
                }
            }

            private bool TryFindPort(string nodeId, string portId, out PortDescriptor port)
            {
                port = default;
                if (!descriptors.TryGetValue(nodeId, out IAbilityNodeDescriptor descriptor)) return false;

                foreach (PortDescriptor candidate in descriptor.Ports)
                {
                    if (candidate.Id == portId)
                    {
                        port = candidate;
                        return true;
                    }
                }

                return false;
            }

            private void CheckEdges()
            {
                foreach (AbilityEdge edge in edges)
                {
                    if (edge == null) continue;

                    bool fromExists = byId.ContainsKey(edge.FromNodeId ?? "");
                    bool toExists = byId.ContainsKey(edge.ToNodeId ?? "");
                    if (!fromExists)
                    {
                        Add(ValidationSeverity.Error, edge.FromNodeId, edge.FromPortId, "The connection '" + edge + "' starts at a node that does not exist.");
                    }

                    if (!toExists)
                    {
                        Add(ValidationSeverity.Error, edge.ToNodeId, edge.ToPortId, "The connection '" + edge + "' ends at a node that does not exist.");
                    }

                    if (!fromExists || !toExists || !descriptors.ContainsKey(edge.FromNodeId) || !descriptors.ContainsKey(edge.ToNodeId))
                    {
                        continue;
                    }

                    if (!TryFindPort(edge.FromNodeId, edge.FromPortId, out PortDescriptor from))
                    {
                        Add(ValidationSeverity.Error, edge.FromNodeId, edge.FromPortId, "The node has no port '" + edge.FromPortId + "'.");
                        continue;
                    }

                    if (!TryFindPort(edge.ToNodeId, edge.ToPortId, out PortDescriptor to))
                    {
                        Add(ValidationSeverity.Error, edge.ToNodeId, edge.ToPortId, "The node has no port '" + edge.ToPortId + "'.");
                        continue;
                    }

                    if (from.Direction != PortDirection.Output)
                    {
                        Add(ValidationSeverity.Error, edge.FromNodeId, edge.FromPortId, "A connection must start at an output port, but '" + edge.FromPortId + "' is an input.");
                        continue;
                    }

                    if (to.Direction != PortDirection.Input)
                    {
                        Add(ValidationSeverity.Error, edge.ToNodeId, edge.ToPortId, "A connection must end at an input port, but '" + edge.ToPortId + "' is an output.");
                        continue;
                    }

                    if (from.Kind != to.Kind)
                    {
                        Add(ValidationSeverity.Error, edge.ToNodeId, edge.ToPortId, "Cannot connect a " + from.Kind + " port to a " + to.Kind + " port ('" + edge + "').");
                        continue;
                    }

                    if (from.Kind == PortKind.Data && from.DataType != to.DataType)
                    {
                        Add(ValidationSeverity.Error, edge.ToNodeId, edge.ToPortId, "Cannot connect " + from.DataType + " to " + to.DataType + " ('" + edge + "'). There is no implicit conversion.");
                        continue;
                    }

                    validEdges.Add(edge);
                }
            }

            private void CheckConnections()
            {
                foreach (KeyValuePair<string, IAbilityNodeDescriptor> pair in descriptors)
                {
                    foreach (PortDescriptor port in pair.Value.Ports)
                    {
                        int count = validEdges.Count(e => port.Direction == PortDirection.Input
                            ? e.ToNodeId == pair.Key && e.ToPortId == port.Id
                            : e.FromNodeId == pair.Key && e.FromPortId == port.Id);

                        if (port.Direction == PortDirection.Input && port.Kind == PortKind.Data)
                        {
                            if (count == 0 && port.Required) Add(ValidationSeverity.Error, pair.Key, port.Id, "The required input '" + port.Id + "' is not connected.");
                            if (count > 1) Add(ValidationSeverity.Error, pair.Key, port.Id, "The input '" + port.Id + "' has " + count + " connections; it takes one.");
                        }
                        else if (port.Direction == PortDirection.Output && port.Kind == PortKind.Flow && count > 1)
                        {
                            Add(ValidationSeverity.Error, pair.Key, port.Id, "The output '" + port.Id + "' has " + count + " connections; it leads to one operation. Use separate steps to branch.");
                        }
                    }
                }
            }

            private void CheckEntry()
            {
                List<string> entries = descriptors.Where(p => NodeTypes.IsEntry(p.Value.TypeId)).Select(p => p.Key).ToList();
                if (entries.Count == 0) Add(ValidationSeverity.Error, null, null, "The ability has no Activate node, so nothing can start it.");
                if (entries.Count > 1) Add(ValidationSeverity.Error, entries[1], null, "The ability has " + entries.Count + " entry nodes (Activate or Status Tick); it needs exactly one.");
            }

            private bool IsOperation(string nodeId) => descriptors[nodeId].Ports.Any(p => p.Kind == PortKind.Flow);

            private void CheckCycles()
            {
                // Data dependencies and flow connections are checked separately; either kind of loop never terminates.
                DetectCycle(PortKind.Data, "data inputs depend on each other in a loop");
                DetectCycle(PortKind.Flow, "operations lead back to themselves");
            }

            private void DetectCycle(PortKind kind, string description)
            {
                Dictionary<string, List<string>> graph = new Dictionary<string, List<string>>();
                foreach (AbilityEdge edge in validEdges)
                {
                    if (!TryFindPort(edge.FromNodeId, edge.FromPortId, out PortDescriptor port) || port.Kind != kind) continue;

                    if (!graph.TryGetValue(edge.FromNodeId, out List<string> targets))
                    {
                        targets = new List<string>();
                        graph.Add(edge.FromNodeId, targets);
                    }

                    targets.Add(edge.ToNodeId);
                }

                Dictionary<string, int> state = new Dictionary<string, int>();
                foreach (string start in graph.Keys.OrderBy(k => k, StringComparer.Ordinal))
                {
                    string cycleNode = Visit(start, graph, state);
                    if (cycleNode != null)
                    {
                        Add(ValidationSeverity.Error, cycleNode, null, "A cycle was found: " + description + ".");
                        return;
                    }
                }
            }

            private static string Visit(string node, Dictionary<string, List<string>> graph, Dictionary<string, int> state)
            {
                if (state.TryGetValue(node, out int s))
                {
                    return s == 1 ? node : null;
                }

                state[node] = 1;
                if (graph.TryGetValue(node, out List<string> targets))
                {
                    foreach (string target in targets)
                    {
                        string found = Visit(target, graph, state);
                        if (found != null) return found;
                    }
                }

                state[node] = 2;
                return null;
            }

            private IEnumerable<AbilityEdge> FlowEdgesFrom(string nodeId) =>
                validEdges.Where(e => e.FromNodeId == nodeId && TryFindPort(e.FromNodeId, e.FromPortId, out PortDescriptor p) && p.Kind == PortKind.Flow);

            private void CheckReachabilityAndContext()
            {
                string entry = descriptors.Where(p => NodeTypes.IsEntry(p.Value.TypeId)).Select(p => p.Key).FirstOrDefault();
                if (entry == null) return;

                // Walk the flow from Activate, remembering for each operation which kinds of context some path to it lacks.
                const EventContext Everything = EventContext.Impact | EventContext.Target | EventContext.Status;
                Dictionary<string, EventContext> possiblyMissing = new Dictionary<string, EventContext>();
                Queue<(string node, EventContext missing)> queue = new Queue<(string, EventContext)>();
                queue.Enqueue((entry, Everything));
                HashSet<(string, EventContext)> seen = new HashSet<(string, EventContext)>();

                while (queue.Count > 0)
                {
                    (string node, EventContext missing) = queue.Dequeue();
                    if (!seen.Add((node, missing))) continue;

                    possiblyMissing[node] = possiblyMissing.TryGetValue(node, out EventContext existing) ? existing | missing : missing;

                    foreach (AbilityEdge edge in FlowEdgesFrom(node))
                    {
                        TryFindPort(edge.FromNodeId, edge.FromPortId, out PortDescriptor port);
                        queue.Enqueue((edge.ToNodeId, missing & ~port.ProvidesContext));
                    }
                }

                foreach (string node in descriptors.Keys.Where(IsOperation))
                {
                    if (!possiblyMissing.ContainsKey(node))
                    {
                        Add(ValidationSeverity.Warning, node, null, "This node can never run: nothing leads to it from Activate.");
                    }
                }

                // A data node that reads context is an error if an operation using it can run without that context.
                foreach (KeyValuePair<string, EventContext> pair in possiblyMissing.Where(p => p.Value != EventContext.None))
                {
                    string operation = pair.Key;
                    foreach (string offender in DataDependencies(operation))
                    {
                        EventContext missing = descriptors[offender].RequiredContext & pair.Value;
                        if ((missing & EventContext.Impact) != 0)
                        {
                            Add(ValidationSeverity.Error, offender, null, "'" + descriptors[offender].DisplayName + "' is only available after an impact or hit, but node '" + operation + "' can run without one. Connect it downstream of an OnImpact or OnHit output.");
                        }

                        if ((missing & EventContext.Status) != 0)
                        {
                            Add(ValidationSeverity.Error, offender, null, "'" + descriptors[offender].DisplayName + "' is only available in a status tick ability, but node '" + operation + "' can run outside one. Start the ability from a Status Tick node instead of Activate.");
                        }

                        if ((missing & EventContext.Target) != 0)
                        {
                            Add(ValidationSeverity.Error, offender, null, "'" + descriptors[offender].DisplayName + "' is only available inside a For Each Target branch, but node '" + operation + "' can run outside one. Connect it downstream of the Each output.");
                        }
                    }
                }

                // Data nodes nothing uses are harmless but probably a mistake.
                foreach (string node in descriptors.Keys.Where(n => !IsOperation(n)))
                {
                    if (!validEdges.Any(e => e.FromNodeId == node))
                    {
                        Add(ValidationSeverity.Warning, node, null, "Nothing uses this node's value.");
                    }
                }
            }

            private IEnumerable<string> DataDependencies(string operation)
            {
                HashSet<string> found = new HashSet<string>();
                Stack<string> stack = new Stack<string>();
                stack.Push(operation);
                while (stack.Count > 0)
                {
                    string current = stack.Pop();
                    foreach (AbilityEdge edge in validEdges.Where(e => e.ToNodeId == current && TryFindPort(e.ToNodeId, e.ToPortId, out PortDescriptor p) && p.Kind == PortKind.Data))
                    {
                        if (found.Add(edge.FromNodeId)) stack.Push(edge.FromNodeId);
                    }
                }

                return found;
            }

            // ----- Compilation -----

            private CompiledAbility Build()
            {
                // Operations get their positions first, so connections can refer forward to them.
                List<string> operationNodes = nodes.Where(n => descriptors.ContainsKey(n.NodeId) && IsOperation(n.NodeId)).Select(n => n.NodeId).ToList();
                for (int i = 0; i < operationNodes.Count; i++)
                {
                    operationIndex[operationNodes[i]] = i;
                }

                CompiledOperation[] operations = new CompiledOperation[operationNodes.Count];
                for (int i = 0; i < operationNodes.Count; i++)
                {
                    CompiledOperation operation = (CompiledOperation)CompileNode(operationNodes[i]);
                    operation.Index = i;
                    operations[i] = operation;
                }

                string entry = descriptors.Where(p => NodeTypes.IsEntry(p.Value.TypeId)).Select(p => p.Key).First();
                return new CompiledAbility(id, assetName, operations, operationIndex[entry], snapshots.ToArray());
            }

            private CompiledNode CompileNode(string nodeId)
            {
                if (compiled.TryGetValue(nodeId, out CompiledNode existing))
                {
                    return existing;
                }

                string previous = currentNode;
                currentNode = nodeId;
                CompiledNode result = descriptors[nodeId].Compile(byId[nodeId].Config, this);
                currentNode = previous;

                result.NodeId = nodeId;
                compiled[nodeId] = result;
                return result;
            }

            private AbilityEdge IncomingData(string portId) =>
                validEdges.FirstOrDefault(e => e.ToNodeId == currentNode && e.ToPortId == portId);

            public FloatExpr FloatInput(string portId)
            {
                AbilityEdge edge = IncomingData(portId);
                return edge == null ? null : (FloatExpr)CompileNode(edge.FromNodeId);
            }

            public EntitySetExpr EntitySetInput(string portId)
            {
                AbilityEdge edge = IncomingData(portId);
                return edge == null ? null : (EntitySetExpr)CompileNode(edge.FromNodeId);
            }

            public EntityExpr EntityInput(string portId)
            {
                AbilityEdge edge = IncomingData(portId);
                return edge == null ? null : (EntityExpr)CompileNode(edge.FromNodeId);
            }

            public int FlowTarget(string outputPortId)
            {
                AbilityEdge edge = validEdges.FirstOrDefault(e => e.FromNodeId == currentNode && e.FromPortId == outputPortId);
                return edge == null ? -1 : operationIndex[edge.ToNodeId];
            }

            public int AllocateSnapshot(AttributeBinding binding)
            {
                int slot = snapshots.Count;
                snapshots.Add(new SnapshotInput(slot, binding));
                return slot;
            }
        }
    }
}
