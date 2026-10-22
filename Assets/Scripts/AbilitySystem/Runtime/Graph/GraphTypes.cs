using System;
using System.Collections.Generic;
using UnityEngine;

namespace SimpleRPG.Abilities
{
    /// <summary>
    /// The two families of port. Flow ports pass control to the next operation; data ports resolve typed values without side effects.
    /// </summary>
    public enum PortKind : byte
    {
        Flow = 0,
        Data = 1,
    }

    public enum PortDirection : byte
    {
        Input = 0,
        Output = 1,
    }

    /// <summary>
    /// The type of value a data port carries. Types are added only when a primitive needs one.
    /// </summary>
    public enum DataType : byte
    {
        None = 0,
        Float = 1,
        Entity = 2,
        EntitySet = 3,
    }


    /// <summary>
    /// What a branch of an ability knows because of how it began. A branch that starts at a projectile impact or melee hit knows who was hit
    /// and where; a branch that starts for one target of a set knows that target. A node that reads this is only valid downstream of a port that provides it.
    /// </summary>
    [Flags]
    public enum EventContext : byte
    {
        None = 0,

        /// <summary>The branch began at an impact or hit: the victim and position are known.</summary>
        Impact = 1,

        /// <summary>The branch began for one target of a set: the current target is known.</summary>
        Target = 2,

        /// <summary>The ability was started by a status: the entity holding it and the status's strength are known.</summary>
        Status = 4,
    }

    /// <summary>
    /// A stable, named connection point on a node. Port IDs never change meaning silently: renaming or retyping one is a validation error for
    /// graphs that used it.
    /// </summary>
    public readonly struct PortDescriptor
    {
        public PortDescriptor(string id, PortKind kind, PortDirection direction, DataType dataType = DataType.None, bool required = false, string displayName = null, bool providesImpact = false, EventContext providesContext = EventContext.None)
        {
            ProvidesContext = providesContext | (providesImpact ? EventContext.Impact : EventContext.None);
            Id = id;
            Kind = kind;
            Direction = direction;
            DataType = dataType;
            Required = required;
            DisplayName = displayName ?? id;
        }

        public string Id { get; }

        public PortKind Kind { get; }

        public PortDirection Direction { get; }

        /// <summary>The value type, for data ports. <see cref="Abilities.DataType.None"/> for flow ports.</summary>
        public DataType DataType { get; }

        /// <summary>True if an input must be connected for the graph to be valid.</summary>
        public bool Required { get; }

        public string DisplayName { get; }

        /// <summary>What a flow output's branch knows because it began there, so nodes that read it are valid downstream of the output.</summary>
        public EventContext ProvidesContext { get; }

        /// <summary>True for a flow output whose branch begins at a projectile impact or melee hit.</summary>
        public bool ProvidesImpact => (ProvidesContext & EventContext.Impact) != 0;

        public static PortDescriptor FlowIn(string id = "In") => new PortDescriptor(id, PortKind.Flow, PortDirection.Input);

        public static PortDescriptor FlowOut(string id, bool providesImpact = false, EventContext providesContext = EventContext.None) =>
            new PortDescriptor(id, PortKind.Flow, PortDirection.Output, providesImpact: providesImpact, providesContext: providesContext);

        public static PortDescriptor DataIn(string id, DataType type, bool required = true) => new PortDescriptor(id, PortKind.Data, PortDirection.Input, type, required);

        public static PortDescriptor DataOut(string id, DataType type) => new PortDescriptor(id, PortKind.Data, PortDirection.Output, type);
    }

    /// <summary>
    /// Which ports may be connected, for tools that check a connection before it is made. The compiler enforces the same rules when it compiles.
    /// </summary>
    public static class PortCompatibility
    {
        /// <summary>
        /// True if an output port may be connected to an input port: an output to an input, flow to flow, or data to data of the same type.
        /// </summary>
        public static bool CanConnect(PortDescriptor from, PortDescriptor to, out string reason)
        {
            if (from.Direction != PortDirection.Output)
            {
                reason = "A connection must start at an output port.";
                return false;
            }

            if (to.Direction != PortDirection.Input)
            {
                reason = "A connection must end at an input port.";
                return false;
            }

            if (from.Kind != to.Kind)
            {
                reason = "Cannot connect a " + from.Kind + " port to a " + to.Kind + " port.";
                return false;
            }

            if (from.Kind == PortKind.Data && from.DataType != to.DataType)
            {
                reason = "Cannot connect " + from.DataType + " to " + to.DataType + ". There is no implicit conversion.";
                return false;
            }

            reason = null;
            return true;
        }

        /// <summary>
        /// True if the port takes one connection at most: a data input takes one value and a flow output leads to one operation.
        /// </summary>
        public static bool TakesOneConnection(PortDescriptor port) =>
            (port.Direction == PortDirection.Input && port.Kind == PortKind.Data) || (port.Direction == PortDirection.Output && port.Kind == PortKind.Flow);
    }

    /// <summary>
    /// The settings of one node. Each node type has its own config class; the graph stores them polymorphically.
    /// </summary>
    [Serializable]
    public abstract class AbilityNodeConfig
    {
    }

    /// <summary>
    /// One node in an ability graph, as stored in the asset.
    /// </summary>
    [Serializable]
    public sealed class AbilityNodeRecord
    {
        /// <summary>Unique within the graph.</summary>
        public string NodeId;

        /// <summary>The stable registry key of the node type, for example "simplerpg.action.damage.v1". Never a C# type name.</summary>
        public string TypeId;

        [SerializeReference]
        public AbilityNodeConfig Config;

        /// <summary>Where the node sits in the editor. Has no effect on gameplay.</summary>
        public Vector2 EditorPosition;
    }

    /// <summary>
    /// A connection from one node's output port to another node's input port.
    /// </summary>
    [Serializable]
    public sealed class AbilityEdge
    {
        public string FromNodeId;
        public string FromPortId;
        public string ToNodeId;
        public string ToPortId;

        public override string ToString() => FromNodeId + "." + FromPortId + " -> " + ToNodeId + "." + ToPortId;
    }

    public enum ValidationSeverity : byte
    {
        Warning = 0,
        Error = 1,
    }

    /// <summary>
    /// One problem found in an ability graph, located by asset, node and port.
    /// </summary>
    public sealed class ValidationMessage
    {
        public ValidationMessage(ValidationSeverity severity, string asset, string nodeId, string portId, string text)
        {
            Severity = severity;
            Asset = asset;
            NodeId = nodeId;
            PortId = portId;
            Text = text;
        }

        public ValidationSeverity Severity { get; }

        public string Asset { get; }

        public string NodeId { get; }

        public string PortId { get; }

        public string Text { get; }

        public override string ToString()
        {
            string where = Asset;
            if (NodeId != null) where += ", node '" + NodeId + "'";
            if (PortId != null) where += ", port '" + PortId + "'";
            return (Severity == ValidationSeverity.Error ? "Error" : "Warning") + " in " + where + ": " + Text;
        }
    }

    /// <summary>
    /// Receives the problems a node reports about its own config.
    /// </summary>
    public interface IValidationSink
    {
        void Error(string text, string portId = null);

        void Warning(string text, string portId = null);
    }

    /// <summary>
    /// Everything found while validating or compiling a graph.
    /// </summary>
    public sealed class ValidationReport
    {
        public List<ValidationMessage> Messages { get; } = new List<ValidationMessage>();

        public bool HasErrors
        {
            get
            {
                foreach (ValidationMessage message in Messages)
                {
                    if (message.Severity == ValidationSeverity.Error) return true;
                }

                return false;
            }
        }

        public IEnumerable<ValidationMessage> Errors
        {
            get
            {
                foreach (ValidationMessage message in Messages)
                {
                    if (message.Severity == ValidationSeverity.Error) yield return message;
                }
            }
        }

        public IEnumerable<ValidationMessage> Warnings
        {
            get
            {
                foreach (ValidationMessage message in Messages)
                {
                    if (message.Severity == ValidationSeverity.Warning) yield return message;
                }
            }
        }

        public override string ToString() => string.Join("\n", Messages);
    }
}
