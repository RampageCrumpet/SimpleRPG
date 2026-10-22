using SimpleRPG.Gameplay;
using System.Collections.Generic;

namespace SimpleRPG.Abilities
{
    /// <summary>
    /// A compiled piece of a graph. It is one of: a number expression, an entity expression, or an operation. Compiled nodes are immutable and
    /// contain no references to the serialized graph, so running an ability never walks Unity objects.
    /// </summary>
    public abstract class CompiledNode
    {
        /// <summary>The graph node this was compiled from, for diagnostics and traces.</summary>
        public string NodeId { get; internal set; }
    }

    /// <summary>
    /// A pure number computed from other nodes, constants and attributes. It has no side effects.
    /// </summary>
    public abstract class FloatExpr : CompiledNode
    {
        /// <summary>Computes the number, and tells the trace if there is one.</summary>
        public double Evaluate(ExecutionContext context, in BranchFrame frame)
        {
            double value = EvaluateCore(context, frame);
            context.TraceValue(this, value, null);
            return value;
        }

        protected abstract double EvaluateCore(ExecutionContext context, in BranchFrame frame);
    }

    /// <summary>
    /// A pure reference to an entity, such as the victim of an impact.
    /// </summary>
    public abstract class EntityExpr : CompiledNode
    {
        /// <summary>Finds the entity, and tells the trace if there is one.</summary>
        public EntityId Evaluate(ExecutionContext context, in BranchFrame frame)
        {
            EntityId value = EvaluateCore(context, frame);
            if (context.Tracing) context.TraceValue(this, 0d, value.IsValid ? value.ToString() : "no entity");
            return value;
        }

        protected abstract EntityId EvaluateCore(ExecutionContext context, in BranchFrame frame);
    }

    /// <summary>
    /// A pure set of entities, such as everything inside a radius. Its order is deterministic.
    /// </summary>
    public abstract class EntitySetExpr : CompiledNode
    {
        /// <summary>Finds the entities, and tells the trace if there is one.</summary>
        public IReadOnlyList<EntityId> Evaluate(ExecutionContext context, in BranchFrame frame)
        {
            IReadOnlyList<EntityId> value = EvaluateCore(context, frame);
            if (context.Tracing) context.TraceValue(this, value.Count, value.Count == 1 ? "1 entity" : value.Count + " entities");
            return value;
        }

        protected abstract IReadOnlyList<EntityId> EvaluateCore(ExecutionContext context, in BranchFrame frame);
    }

    /// <summary>
    /// A step that does something. It runs, then schedules the operations that follow it.
    /// </summary>
    public abstract class CompiledOperation : CompiledNode
    {
        /// <summary>This operation's position in <see cref="CompiledAbility.Operations"/>.</summary>
        public int Index { get; internal set; }

        public abstract void Execute(ExecutionContext context, in BranchFrame frame);
    }

    /// <summary>
    /// An input the ability reads once, when it is activated, so a later change (the caster dying, a buff ending) does not alter it.
    /// </summary>
    public readonly struct SnapshotInput
    {
        public SnapshotInput(int slot, AttributeBinding binding)
        {
            Slot = slot;
            Binding = binding;
        }

        public int Slot { get; }

        public AttributeBinding Binding { get; }
    }

    /// <summary>
    /// The runnable form of an ability graph: operations with their connections resolved, ready to execute.
    /// </summary>
    public sealed class CompiledAbility
    {
        public CompiledAbility(DefinitionId id, string name, CompiledOperation[] operations, int entryOperation, SnapshotInput[] snapshots)
        {
            Id = id;
            Name = name;
            Operations = operations;
            EntryOperation = entryOperation;
            Snapshots = snapshots;
        }

        public DefinitionId Id { get; }

        public string Name { get; }

        public CompiledOperation[] Operations { get; }

        /// <summary>The operation where execution starts.</summary>
        public int EntryOperation { get; }

        public SnapshotInput[] Snapshots { get; }

        public int SnapshotCount => Snapshots.Length;

        /// <summary>
        /// Describes an operation for diagnostics: the ability name and the graph node it came from.
        /// </summary>
        public string Describe(int operation) => Name + ", node '" + Operations[operation].NodeId + "'";
    }
}
