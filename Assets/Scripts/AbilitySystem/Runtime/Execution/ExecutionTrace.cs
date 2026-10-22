using SimpleRPG.Gameplay;
using System;
using System.Collections.Generic;
using System.Linq;

namespace SimpleRPG.Abilities
{
    /// <summary>
    /// What kind of thing a <see cref="TraceEvent"/> records.
    /// </summary>
    public enum TraceEventKind : byte
    {
        /// <summary>An ability started running.</summary>
        ExecutionStarted = 0,

        /// <summary>The executor ran an operation node.</summary>
        NodeRan = 1,

        /// <summary>A data node produced a value (a number, an entity, or a set of entities) while something read it.</summary>
        Value = 2,

        /// <summary>An operation did something or chose something: damage dealt, a chance rolled, a projectile launched.</summary>
        Outcome = 3,

        /// <summary>The ability paused until a projectile or swing reports.</summary>
        Suspended = 4,

        /// <summary>The ability resumed because a projectile or swing reported.</summary>
        Resumed = 5,

        /// <summary>An operation could not do its job, or a limit was reached. The same messages the executor reports as diagnostics.</summary>
        Diagnostic = 6,

        /// <summary>The ability was stopped for exceeding a limit.</summary>
        ExecutionStopped = 7,
    }

    /// <summary>
    /// One thing that happened while an ability ran. Events are in the order they happened; <see cref="Sequence"/> says what came first.
    /// </summary>
    public readonly struct TraceEvent
    {
        public TraceEvent(long sequence, TraceEventKind kind, ExecutionId execution, DefinitionId ability, string abilityName, string nodeId, string portId, double number, string text)
        {
            Sequence = sequence;
            Kind = kind;
            Execution = execution;
            Ability = ability;
            AbilityName = abilityName;
            NodeId = nodeId;
            PortId = portId;
            Number = number;
            Text = text;
        }

        /// <summary>Position in the trace; increases by one for every event, across executions.</summary>
        public long Sequence { get; }

        public TraceEventKind Kind { get; }

        public ExecutionId Execution { get; }

        /// <summary>The ability that was running.</summary>
        public DefinitionId Ability { get; }

        public string AbilityName { get; }

        /// <summary>The graph node the event belongs to, or null for events about the whole execution.</summary>
        public string NodeId { get; }

        /// <summary>The port, when the event is about one.</summary>
        public string PortId { get; }

        /// <summary>A number the event carries: the value of a number node, an amount of damage, a probability.</summary>
        public double Number { get; }

        /// <summary>A readable description of the event.</summary>
        public string Text { get; }

        public override string ToString() => "#" + Sequence + " " + Kind + " " + (NodeId ?? "-") + (Text == null ? string.Empty : " " + Text);
    }

    /// <summary>
    /// Receives what an ability does as it runs. Attach one to <see cref="ExecutionServices.Trace"/>; the real executor reports to it and
    /// nothing else changes. With none attached, running an ability costs one empty check at each report point. Server only.
    /// </summary>
    public interface IExecutionTrace
    {
        void Record(in TraceEvent traceEvent);
    }

    /// <summary>
    /// Keeps the most recent trace events in memory for a viewer to read. When it is full the oldest events are dropped, so leaving it
    /// attached to a running game cannot grow without limit.
    /// </summary>
    public sealed class RecordingTrace : IExecutionTrace
    {
        private readonly Queue<TraceEvent> events = new Queue<TraceEvent>();

        public RecordingTrace(int capacity = 4096)
        {
            if (capacity <= 0) throw new ArgumentOutOfRangeException(nameof(capacity));

            Capacity = capacity;
        }

        public int Capacity { get; }

        /// <summary>The events kept, oldest first.</summary>
        public IReadOnlyList<TraceEvent> Events => events.ToList();

        public int Count => events.Count;

        /// <summary>Raised after each event is recorded.</summary>
        public event Action<TraceEvent> Recorded;

        public void Record(in TraceEvent traceEvent)
        {
            events.Enqueue(traceEvent);
            while (events.Count > Capacity)
            {
                events.Dequeue();
            }

            Recorded?.Invoke(traceEvent);
        }

        public void Clear() => events.Clear();

        /// <summary>The kept events of one execution, in order.</summary>
        public List<TraceEvent> ForExecution(ExecutionId execution) => events.Where(e => e.Execution == execution).ToList();

        /// <summary>The executions that have events, oldest first.</summary>
        public List<ExecutionId> Executions() => events.Select(e => e.Execution).Distinct().ToList();
    }
}
