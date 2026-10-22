using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace SimpleRPG.Abilities
{
    /// <summary>
    /// A reusable ability: what it looks like to the player and a graph of nodes that says what it does. The definition is shared and never
    /// changes while the game runs; anything that changes lives in runtime state. An ability does not know whether a player, a monster,
    /// an item or a trap uses it.
    /// </summary>
    [CreateAssetMenu(fileName = "Ability", menuName = "SimpleRPG/Ability")]
    public class AbilityDefinition : GameplayDefinition
    {
        [SerializeField]
        [Tooltip("The name shown to the player.")]
        private string abilityName = "New Ability";

        [SerializeField]
        [TextArea]
        [Tooltip("The tooltip shown when the player hovers over this ability.")]
        private string toolTip;

        [SerializeField]
        [Tooltip("The icon shown on the ability bar.")]
        private Sprite icon;

        [SerializeField]
        [Tooltip("The sound played when this ability is used.")]
        private AudioClip sound;

        [SerializeField]
        [Min(0f)]
        [Tooltip("Seconds before this ability can be used again by the same user.")]
        private float cooldownSeconds = 1f;

        [SerializeField]
        private List<AbilityNodeRecord> nodes = new List<AbilityNodeRecord>();

        [SerializeField]
        private List<AbilityEdge> edges = new List<AbilityEdge>();

        // The compiled form is derived data. It is kept beside the asset, never in it, so the asset stays unchanged while the game runs.
        private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<AbilityDefinition, CompiledEntry> compiledCache =
            new System.Runtime.CompilerServices.ConditionalWeakTable<AbilityDefinition, CompiledEntry>();

        private sealed class CompiledEntry
        {
            public AbilityNodeRegistry Registry;

            public CompileResult Result;
        }

        private void Invalidate() => compiledCache.Remove(this);

        public string AbilityName => abilityName;

        public string ToolTip => toolTip;

        public Sprite Icon => icon;

        public AudioClip Sound => sound;

        public float CooldownSeconds => cooldownSeconds;

        public IReadOnlyList<AbilityNodeRecord> Nodes => nodes;

        public IReadOnlyList<AbilityEdge> Edges => edges;

        /// <summary>
        /// Sets the player-facing details. Used by tools and tests that build abilities in code.
        /// </summary>
        public void SetDetails(string name, float cooldown, string tip = null, Sprite abilityIcon = null, AudioClip abilitySound = null)
        {
            abilityName = name;
            cooldownSeconds = Mathf.Max(0f, cooldown);
            toolTip = tip;
            icon = abilityIcon;
            sound = abilitySound;
            Invalidate();
        }

        /// <summary>
        /// True if the graph has a node of the given type.
        /// </summary>
        public bool HasNode(string typeId)
        {
            for (int i = 0; i < nodes.Count; i++)
            {
                if (nodes[i] != null && nodes[i].TypeId == typeId) return true;
            }

            return false;
        }

        /// <summary>
        /// Adds a node to the graph and returns its ID. The same document operations are used by the editor, tools and tests.
        /// </summary>
        /// <param name="typeId">The node type, from <see cref="NodeTypes"/> or a registered type.</param>
        /// <param name="config">The node's settings, or null for the type's defaults.</param>
        /// <param name="nodeId">The ID to give the node, or null to generate a unique one.</param>
        public string AddNode(string typeId, AbilityNodeConfig config = null, string nodeId = null)
        {
            if (string.IsNullOrEmpty(nodeId))
            {
                int next = nodes.Count + 1;
                do
                {
                    nodeId = "n" + next++;
                }
                while (nodes.Any(n => n.NodeId == nodeId));
            }
            else if (nodes.Any(n => n.NodeId == nodeId))
            {
                throw new ArgumentException("A node with the ID '" + nodeId + "' already exists.", nameof(nodeId));
            }

            if (config == null && AbilityNodeRegistry.Default.TryGet(typeId, out IAbilityNodeDescriptor descriptor))
            {
                config = descriptor.CreateDefaultConfig();
            }

            nodes.Add(new AbilityNodeRecord { NodeId = nodeId, TypeId = typeId, Config = config });
            Invalidate();
            return nodeId;
        }

        /// <summary>
        /// Removes a node and every connection to or from it.
        /// </summary>
        /// <returns>True if the node existed.</returns>
        public bool RemoveNode(string nodeId)
        {
            int removed = nodes.RemoveAll(n => n.NodeId == nodeId);
            edges.RemoveAll(e => e.FromNodeId == nodeId || e.ToNodeId == nodeId);
            Invalidate();
            return removed > 0;
        }

        /// <summary>
        /// Connects an output port to an input port. The connection is checked when the ability is compiled.
        /// </summary>
        public void Connect(string fromNodeId, string fromPortId, string toNodeId, string toPortId)
        {
            edges.Add(new AbilityEdge { FromNodeId = fromNodeId, FromPortId = fromPortId, ToNodeId = toNodeId, ToPortId = toPortId });
            Invalidate();
        }

        /// <summary>
        /// Removes one connection.
        /// </summary>
        /// <returns>True if the connection existed.</returns>
        public bool Disconnect(string fromNodeId, string fromPortId, string toNodeId, string toPortId)
        {
            int removed = edges.RemoveAll(e => e.FromNodeId == fromNodeId && e.FromPortId == fromPortId && e.ToNodeId == toNodeId && e.ToPortId == toPortId);
            Invalidate();
            return removed > 0;
        }

        /// <summary>
        /// Replaces a node's settings, for example to put back the defaults of its type.
        /// </summary>
        /// <returns>True if the node exists.</returns>
        public bool SetNodeConfig(string nodeId, AbilityNodeConfig config)
        {
            for (int i = 0; i < nodes.Count; i++)
            {
                if (nodes[i] != null && nodes[i].NodeId == nodeId)
                {
                    nodes[i].Config = config;
                    Invalidate();
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Moves a node in the editor. Has no effect on gameplay.
        /// </summary>
        /// <returns>True if the node exists.</returns>
        public bool SetNodePosition(string nodeId, Vector2 position)
        {
            for (int i = 0; i < nodes.Count; i++)
            {
                if (nodes[i] != null && nodes[i].NodeId == nodeId)
                {
                    nodes[i].EditorPosition = position;
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Checks the graph and compiles it. The result is kept until the graph changes.
        /// </summary>
        public CompileResult Compile(AbilityNodeRegistry registry = null)
        {
            AbilityNodeRegistry used = registry ?? AbilityNodeRegistry.Default;
            if (compiledCache.TryGetValue(this, out CompiledEntry entry) && ReferenceEquals(entry.Registry, used))
            {
                return entry.Result;
            }

            CompileResult result = AbilityCompiler.Compile(name, Id, nodes, edges, used);
            compiledCache.Remove(this);
            compiledCache.Add(this, new CompiledEntry { Registry = used, Result = result });
            return result;
        }

#if UNITY_EDITOR
        // A new ability starts with an Activate node, so it is a valid starting point.
        protected override void Reset()
        {
            base.Reset();
            if (nodes.Count == 0)
            {
                AddNode(NodeTypes.Activate, null, "activate");
            }
        }

        protected override void OnValidate()
        {
            base.OnValidate();
            Invalidate();
        }
#endif
    }
}
