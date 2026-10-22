using SimpleRPG.Gameplay;
using System;
using System.Collections.Generic;
using System.Linq;

namespace SimpleRPG.Abilities
{
    /// <summary>
    /// What the compiler offers a node while it compiles: its connected inputs and where its flow outputs lead.
    /// </summary>
    public interface ICompilationContext
    {
        /// <summary>The graph node being compiled.</summary>
        string NodeId { get; }

        /// <summary>The compiled expression connected to a number input, or null if nothing is connected.</summary>
        FloatExpr FloatInput(string portId);

        /// <summary>The compiled expression connected to an entity-set input, or null if nothing is connected.</summary>
        EntitySetExpr EntitySetInput(string portId);

        /// <summary>The compiled expression connected to an entity input, or null if nothing is connected.</summary>
        EntityExpr EntityInput(string portId);

        /// <summary>The operation a flow output leads to, or -1 if it is not connected.</summary>
        int FlowTarget(string outputPortId);

        /// <summary>Reserves a snapshot slot for an input that is read when the ability is activated.</summary>
        int AllocateSnapshot(AttributeBinding binding);
    }

    /// <summary>
    /// Describes one node type: its ports, its default settings, how to check its settings and how to compile it. The editor palette and the
    /// compiler both read the same descriptors, so adding a node type adds it to both.
    /// </summary>
    public interface IAbilityNodeDescriptor
    {
        /// <summary>The stable key stored in graphs, for example "simplerpg.action.damage.v1".</summary>
        string TypeId { get; }

        string DisplayName { get; }

        string Category { get; }

        string Description { get; }

        IReadOnlyList<PortDescriptor> Ports { get; }

        Type ConfigType { get; }

        /// <summary>True if the node reads impact data, so it is only valid downstream of an output that provides it.</summary>
        bool RequiresImpactContext { get; }

        /// <summary>Everything the node reads from the branch it runs in: impact data, the current target, or both.</summary>
        EventContext RequiredContext { get; }

        AbilityNodeConfig CreateDefaultConfig();

        void Validate(AbilityNodeConfig config, IValidationSink sink);

        CompiledNode Compile(AbilityNodeConfig config, ICompilationContext context);
    }

    /// <summary>
    /// A node descriptor defined by a few functions, so a new node type needs one registration and no editor changes.
    /// </summary>
    public sealed class NodeDescriptor<TConfig> : IAbilityNodeDescriptor where TConfig : AbilityNodeConfig, new()
    {
        private readonly Action<TConfig, IValidationSink> validate;

        private readonly Func<TConfig, ICompilationContext, CompiledNode> compile;

        public NodeDescriptor(
            string typeId,
            string displayName,
            string category,
            string description,
            IReadOnlyList<PortDescriptor> ports,
            Func<TConfig, ICompilationContext, CompiledNode> compile,
            Action<TConfig, IValidationSink> validate = null,
            bool requiresImpactContext = false,
            EventContext requiredContext = EventContext.None)
        {
            RequiredContext = requiredContext | (requiresImpactContext ? EventContext.Impact : EventContext.None);
            TypeId = typeId;
            DisplayName = displayName;
            Category = category;
            Description = description;
            Ports = ports;
            this.compile = compile;
            this.validate = validate;
        }

        public string TypeId { get; }

        public string DisplayName { get; }

        public string Category { get; }

        public string Description { get; }

        public IReadOnlyList<PortDescriptor> Ports { get; }

        public Type ConfigType => typeof(TConfig);

        public bool RequiresImpactContext => (RequiredContext & EventContext.Impact) != 0;

        public EventContext RequiredContext { get; }

        public AbilityNodeConfig CreateDefaultConfig() => new TConfig();

        public void Validate(AbilityNodeConfig config, IValidationSink sink) => validate?.Invoke((TConfig)config, sink);

        public CompiledNode Compile(AbilityNodeConfig config, ICompilationContext context) => compile((TConfig)config, context);
    }

    /// <summary>
    /// The node types available to ability graphs. Registration is explicit, so nothing depends on reflection at runtime.
    /// </summary>
    public sealed class AbilityNodeRegistry
    {
        private static AbilityNodeRegistry defaultRegistry;

        private readonly Dictionary<string, IAbilityNodeDescriptor> descriptors = new Dictionary<string, IAbilityNodeDescriptor>(StringComparer.Ordinal);

        /// <summary>
        /// The registry with every built-in node type.
        /// </summary>
        public static AbilityNodeRegistry Default
        {
            get
            {
                if (defaultRegistry == null)
                {
                    AbilityNodeRegistry registry = new AbilityNodeRegistry();
                    BuiltInNodes.RegisterAll(registry);
                    defaultRegistry = registry;
                }

                return defaultRegistry;
            }
        }

        public IEnumerable<IAbilityNodeDescriptor> All => descriptors.Values;

        /// <summary>
        /// Adds a node type. A type ID can only be registered once.
        /// </summary>
        public void Register(IAbilityNodeDescriptor descriptor)
        {
            if (descriptor == null) throw new ArgumentNullException(nameof(descriptor));
            if (string.IsNullOrEmpty(descriptor.TypeId)) throw new ArgumentException("A node type needs a type ID.", nameof(descriptor));
            if (descriptors.ContainsKey(descriptor.TypeId)) throw new ArgumentException("Node type " + descriptor.TypeId + " is already registered.", nameof(descriptor));

            descriptors.Add(descriptor.TypeId, descriptor);
        }

        public bool TryGet(string typeId, out IAbilityNodeDescriptor descriptor)
        {
            if (typeId == null)
            {
                descriptor = null;
                return false;
            }

            return descriptors.TryGetValue(typeId, out descriptor);
        }

        /// <summary>
        /// Searches node types by name, category or description, for a palette. Matching ignores case. An empty search lists everything.
        /// </summary>
        public IEnumerable<IAbilityNodeDescriptor> Search(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return descriptors.Values.OrderBy(d => d.Category).ThenBy(d => d.DisplayName);
            }

            return descriptors.Values
                .Where(d => Contains(d.DisplayName, text) || Contains(d.Category, text) || Contains(d.Description, text))
                .OrderBy(d => d.Category)
                .ThenBy(d => d.DisplayName);
        }

        private static bool Contains(string haystack, string needle) => haystack != null && haystack.IndexOf(needle, StringComparison.OrdinalIgnoreCase) >= 0;
    }
}
