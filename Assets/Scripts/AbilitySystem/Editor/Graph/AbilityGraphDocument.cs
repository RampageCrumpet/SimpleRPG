using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace SimpleRPG.Abilities.Editor
{
    /// <summary>
    /// The editing operations of the visual ability editor, with no canvas in them. An <see cref="AbilityDefinition"/> is the only copy of the
    /// graph: every operation records undo for it, changes it directly and marks it dirty, so the canvas is a view that can be rebuilt from the
    /// asset at any time.
    /// </summary>
    public sealed class AbilityGraphDocument
    {
        /// <summary>Gap between columns and rows when nodes are laid out automatically.</summary>
        private const float ColumnWidth = 300f;
        private const float RowHeight = 160f;

        public AbilityGraphDocument(AbilityDefinition definition, AbilityNodeRegistry registry = null)
        {
            Definition = definition ?? throw new ArgumentNullException(nameof(definition));
            Registry = registry ?? AbilityNodeRegistry.Default;
        }

        public AbilityDefinition Definition { get; }

        public AbilityNodeRegistry Registry { get; }

        /// <summary>
        /// Raised after any operation changes the graph, and after undo or redo, so views can rebuild.
        /// </summary>
        public event Action Changed;

        /// <summary>
        /// Raised with a plain-language reason when an edit is refused, so a view can tell the user why nothing happened.
        /// </summary>
        public event Action<string> Refused;

        /// <summary>
        /// True if the asset has unsaved changes.
        /// </summary>
        public bool IsDirty => EditorUtility.IsDirty(Definition);

        /// <summary>
        /// Adds a node of a registered type with its default settings.
        /// </summary>
        /// <returns>The new node's ID, or null if the type is not registered.</returns>
        public string AddNode(string typeId, Vector2 position)
        {
            if (!Registry.TryGet(typeId, out IAbilityNodeDescriptor descriptor))
            {
                return null;
            }

            Begin("Add " + descriptor.DisplayName);
            string nodeId = Definition.AddNode(typeId);
            Definition.SetNodePosition(nodeId, position);
            End();
            return nodeId;
        }

        /// <summary>
        /// Removes nodes and every connection to or from them.
        /// </summary>
        /// <returns>True if any node was removed.</returns>
        public bool RemoveNodes(IEnumerable<string> nodeIds)
        {
            List<string> existing = nodeIds.Where(id => Definition.Nodes.Any(n => n.NodeId == id)).Distinct().ToList();
            if (existing.Count == 0)
            {
                return false;
            }

            Begin(existing.Count == 1 ? "Delete Node" : "Delete Nodes");
            foreach (string id in existing)
            {
                Definition.RemoveNode(id);
            }

            End();
            return true;
        }

        /// <summary>
        /// Connects an output port to an input port, if the ports are compatible. An input that takes one connection and an output that leads to
        /// one operation give up their old connection to take the new one.
        /// </summary>
        /// <returns>True if the connection exists afterwards. When false, <paramref name="reason"/> says why.</returns>
        public bool Connect(string fromNodeId, string fromPortId, string toNodeId, string toPortId, out string reason)
        {
            if (!TryGetPort(fromNodeId, fromPortId, out PortDescriptor from) || !TryGetPort(toNodeId, toPortId, out PortDescriptor to))
            {
                reason = "One of the ports does not exist.";
                Refused?.Invoke(reason);
                return false;
            }

            if (!PortCompatibility.CanConnect(from, to, out reason))
            {
                Refused?.Invoke(reason);
                return false;
            }

            if (fromNodeId == toNodeId)
            {
                reason = "A node cannot be connected to itself.";
                Refused?.Invoke(reason);
                return false;
            }

            if (Definition.Edges.Any(e => Same(e, fromNodeId, fromPortId, toNodeId, toPortId)))
            {
                reason = null;
                return true;
            }

            Begin("Connect");
            if (PortCompatibility.TakesOneConnection(to))
            {
                RemoveEdgesWhere(e => e.ToNodeId == toNodeId && e.ToPortId == toPortId);
            }

            if (PortCompatibility.TakesOneConnection(from))
            {
                RemoveEdgesWhere(e => e.FromNodeId == fromNodeId && e.FromPortId == fromPortId);
            }

            Definition.Connect(fromNodeId, fromPortId, toNodeId, toPortId);
            End();
            reason = null;
            return true;
        }

        /// <summary>
        /// Removes one connection.
        /// </summary>
        /// <returns>True if the connection existed.</returns>
        public bool Disconnect(string fromNodeId, string fromPortId, string toNodeId, string toPortId)
        {
            if (!Definition.Edges.Any(e => Same(e, fromNodeId, fromPortId, toNodeId, toPortId)))
            {
                return false;
            }

            Begin("Disconnect");
            Definition.Disconnect(fromNodeId, fromPortId, toNodeId, toPortId);
            End();
            return true;
        }

        /// <summary>
        /// Copies nodes, and the connections between them, to the clipboard. Connections to nodes that are not copied are left behind.
        /// </summary>
        public AbilityGraphClipboard Copy(IEnumerable<string> nodeIds)
        {
            AbilityGraphClipboard copy = Snapshot(nodeIds);
            if (!copy.IsEmpty)
            {
                AbilityGraphClipboard.Current = copy;
            }

            return copy;
        }

        /// <summary>
        /// Adds a copy of some nodes, and the connections between them, as one undo step. Every pasted node gets a fresh ID and its own copy of
        /// the settings; pasted nodes are not connected to anything outside the paste.
        /// </summary>
        /// <param name="offset">How far to shift the pasted nodes, so they do not land exactly on the originals.</param>
        /// <returns>The IDs of the new nodes, in the order they were copied.</returns>
        public IReadOnlyList<string> Paste(AbilityGraphClipboard clipboard, Vector2 offset)
        {
            List<string> created = new List<string>();
            if (clipboard == null || clipboard.IsEmpty)
            {
                return created;
            }

            Begin(clipboard.Nodes.Count == 1 ? "Paste Node" : "Paste Nodes");
            Dictionary<string, string> newIds = new Dictionary<string, string>();
            foreach (AbilityGraphClipboard.NodeCopy node in clipboard.Nodes)
            {
                string id = Definition.AddNode(node.TypeId, AbilityGraphClipboard.CloneConfig(node.Config));
                Definition.SetNodePosition(id, node.Position + offset);
                newIds[node.SourceId] = id;
                created.Add(id);
            }

            foreach (AbilityEdge edge in clipboard.Edges)
            {
                if (newIds.TryGetValue(edge.FromNodeId, out string from) && newIds.TryGetValue(edge.ToNodeId, out string to))
                {
                    Definition.Connect(from, edge.FromPortId, to, edge.ToPortId);
                }
            }

            End();
            return created;
        }

        /// <summary>
        /// Pastes the clipboard's last copy.
        /// </summary>
        public IReadOnlyList<string> PasteCurrent() => Paste(AbilityGraphClipboard.Current, new Vector2(40f, 40f));

        /// <summary>
        /// Copies nodes and pastes them straight back, without touching the clipboard.
        /// </summary>
        public IReadOnlyList<string> Duplicate(IEnumerable<string> nodeIds) => Paste(Snapshot(nodeIds), new Vector2(40f, 40f));

        private AbilityGraphClipboard Snapshot(IEnumerable<string> nodeIds)
        {
            HashSet<string> wanted = new HashSet<string>(nodeIds);
            IReadOnlyDictionary<string, Vector2> positions = DisplayPositions();
            AbilityGraphClipboard copy = new AbilityGraphClipboard();
            foreach (AbilityNodeRecord node in Definition.Nodes)
            {
                if (wanted.Contains(node.NodeId))
                {
                    copy.Nodes.Add(new AbilityGraphClipboard.NodeCopy
                    {
                        SourceId = node.NodeId,
                        TypeId = node.TypeId,
                        Config = AbilityGraphClipboard.CloneConfig(node.Config),
                        Position = positions[node.NodeId],
                    });
                }
            }

            foreach (AbilityEdge edge in Definition.Edges)
            {
                if (wanted.Contains(edge.FromNodeId) && wanted.Contains(edge.ToNodeId))
                {
                    copy.Edges.Add(new AbilityEdge { FromNodeId = edge.FromNodeId, FromPortId = edge.FromPortId, ToNodeId = edge.ToNodeId, ToPortId = edge.ToPortId });
                }
            }

            return copy;
        }

        /// <summary>
        /// Puts back the default settings of a node's type, for a node whose settings are missing or are of the wrong kind.
        /// </summary>
        /// <returns>True if the node exists and its type is registered.</returns>
        public bool ResetConfig(string nodeId)
        {
            AbilityNodeRecord node = Definition.Nodes.FirstOrDefault(n => n != null && n.NodeId == nodeId);
            if (node == null || !Registry.TryGet(node.TypeId, out IAbilityNodeDescriptor descriptor))
            {
                return false;
            }

            Begin("Reset Settings");
            Definition.SetNodeConfig(nodeId, descriptor.CreateDefaultConfig());
            End();
            return true;
        }

        /// <summary>
        /// Moves nodes to new positions in a single undo step.
        /// </summary>
        public void MoveNodes(IReadOnlyDictionary<string, Vector2> positions)
        {
            if (positions.Count == 0 || positions.All(p => Definition.Nodes.Any(n => n.NodeId == p.Key && n.EditorPosition == p.Value)))
            {
                return;
            }

            Begin("Move Nodes");
            foreach (KeyValuePair<string, Vector2> pair in positions)
            {
                Definition.SetNodePosition(pair.Key, pair.Value);
            }

            End();
        }

        /// <summary>
        /// Stores an automatic layout as the nodes' positions.
        /// </summary>
        public void AutoLayout()
        {
            IReadOnlyDictionary<string, Vector2> layout = ComputeLayout(Definition);
            Begin("Auto Layout");
            foreach (KeyValuePair<string, Vector2> pair in layout)
            {
                Definition.SetNodePosition(pair.Key, pair.Value);
            }

            End();
        }

        /// <summary>
        /// Where each node is drawn. A graph that has never been arranged, with every node at the origin, is shown in an automatic layout
        /// without changing the asset; the layout is stored once the user moves a node or asks for it.
        /// </summary>
        public IReadOnlyDictionary<string, Vector2> DisplayPositions()
        {
            IReadOnlyList<AbilityNodeRecord> nodes = Definition.Nodes;
            if (nodes.Count > 1 && nodes.All(n => n.EditorPosition == Vector2.zero))
            {
                return ComputeLayout(Definition);
            }

            return nodes.ToDictionary(n => n.NodeId, n => n.EditorPosition);
        }

        /// <summary>
        /// Looks up a port of a node by the node type's descriptor.
        /// </summary>
        public bool TryGetPort(string nodeId, string portId, out PortDescriptor port)
        {
            AbilityNodeRecord node = Definition.Nodes.FirstOrDefault(n => n != null && n.NodeId == nodeId);
            if (node != null && Registry.TryGet(node.TypeId, out IAbilityNodeDescriptor descriptor))
            {
                foreach (PortDescriptor candidate in descriptor.Ports)
                {
                    if (candidate.Id == portId)
                    {
                        port = candidate;
                        return true;
                    }
                }
            }

            port = default;
            return false;
        }

        /// <summary>
        /// Writes the asset to disk.
        /// </summary>
        public void Save()
        {
            AssetDatabase.SaveAssetIfDirty(Definition);
        }

        /// <summary>
        /// Tells views the asset changed from outside the document, for example after undo or redo.
        /// </summary>
        public void NotifyChanged() => Changed?.Invoke();

        /// <summary>
        /// Arranges nodes in columns: a node sits to the right of everything that feeds it, and nodes in a column are stacked.
        /// </summary>
        public static IReadOnlyDictionary<string, Vector2> ComputeLayout(AbilityDefinition definition)
        {
            Dictionary<string, int> depth = definition.Nodes.ToDictionary(n => n.NodeId, _ => 0);

            // Longest path from a source, with a cap so a cycle (which the compiler rejects) cannot loop forever.
            for (int pass = 0; pass < depth.Count; pass++)
            {
                bool changed = false;
                foreach (AbilityEdge edge in definition.Edges)
                {
                    if (depth.TryGetValue(edge.FromNodeId, out int from) && depth.TryGetValue(edge.ToNodeId, out int to) && to < from + 1)
                    {
                        depth[edge.ToNodeId] = from + 1;
                        changed = true;
                    }
                }

                if (!changed) break;
            }

            Dictionary<int, int> rows = new Dictionary<int, int>();
            Dictionary<string, Vector2> layout = new Dictionary<string, Vector2>();
            foreach (AbilityNodeRecord node in definition.Nodes)
            {
                int column = depth[node.NodeId];
                rows.TryGetValue(column, out int row);
                rows[column] = row + 1;
                layout[node.NodeId] = new Vector2(column * ColumnWidth, row * RowHeight);
            }

            return layout;
        }

        private void RemoveEdgesWhere(Predicate<AbilityEdge> match)
        {
            foreach (AbilityEdge edge in Definition.Edges.Where(e => match(e)).ToList())
            {
                Definition.Disconnect(edge.FromNodeId, edge.FromPortId, edge.ToNodeId, edge.ToPortId);
            }
        }

        private static bool Same(AbilityEdge edge, string fromNodeId, string fromPortId, string toNodeId, string toPortId) =>
            edge.FromNodeId == fromNodeId && edge.FromPortId == fromPortId && edge.ToNodeId == toNodeId && edge.ToPortId == toPortId;

        private void Begin(string undoName) => Undo.RecordObject(Definition, undoName);

        private void End()
        {
            EditorUtility.SetDirty(Definition);
            Changed?.Invoke();
        }
    }
}
