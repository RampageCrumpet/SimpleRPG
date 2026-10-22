using System.Collections.Generic;
using UnityEngine;

namespace SimpleRPG.Abilities.Editor
{
    /// <summary>
    /// A copy of some nodes and the connections between them, ready to be pasted into the same ability or another one. Node IDs here are only
    /// labels for the copy: pasting gives every node a fresh ID, so a paste can never clash with, or reconnect to, the nodes it was copied from.
    /// </summary>
    public sealed class AbilityGraphClipboard
    {
        /// <summary>One copied node.</summary>
        public sealed class NodeCopy
        {
            public string SourceId;
            public string TypeId;
            public AbilityNodeConfig Config;
            public Vector2 Position;
        }

        /// <summary>The text placed on the system clipboard so the canvas knows a graph copy is waiting.</summary>
        public const string Marker = "SimpleRPG.AbilityGraph.Clipboard";

        /// <summary>The last copy made in this editor session, shared by every ability window.</summary>
        public static AbilityGraphClipboard Current { get; set; }

        public List<NodeCopy> Nodes { get; } = new List<NodeCopy>();

        /// <summary>Only connections with both ends among the copied nodes.</summary>
        public List<AbilityEdge> Edges { get; } = new List<AbilityEdge>();

        public bool IsEmpty => Nodes.Count == 0;

        /// <summary>
        /// Copies a node's settings so the copy and the original can be edited separately.
        /// </summary>
        public static AbilityNodeConfig CloneConfig(AbilityNodeConfig config)
        {
            if (config == null) return null;

            // The settings are plain fields (numbers, text, enums and asset references), so a shallow copy is a full copy.
            return (AbilityNodeConfig)typeof(object)
                .GetMethod("MemberwiseClone", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                .Invoke(config, null);
        }
    }
}
