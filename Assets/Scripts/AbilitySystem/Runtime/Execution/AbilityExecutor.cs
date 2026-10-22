using SimpleRPG.Gameplay;
using System.Collections.Generic;

namespace SimpleRPG.Abilities
{
    /// <summary>
    /// The result of starting an ability.
    /// </summary>
    public readonly struct ExecutionResult
    {
        public ExecutionResult(ExecutionId id, bool completed)
        {
            Id = id;
            Completed = completed;
        }

        public ExecutionId Id { get; }

        /// <summary>False if the execution was cut short by a budget.</summary>
        public bool Completed { get; }
    }

    /// <summary>
    /// Runs compiled abilities. Server only. Operations run in a queue, never recursively, with a budget so a bad graph cannot freeze the game.
    /// An ability that launches a projectile is suspended until the projectile reports, and resumes with the impact's details.
    /// </summary>
    public sealed class AbilityExecutor : IProjectileEvents, IMeleeEvents
    {
        private readonly ExecutionServices services;

        private readonly Dictionary<ContinuationToken, Continuation> continuations = new Dictionary<ContinuationToken, Continuation>();

        public AbilityExecutor(ExecutionServices services)
        {
            this.services = services;
        }

        /// <summary>
        /// Suspended executions waiting for a delivery to report.
        /// </summary>
        public int PendingContinuations => continuations.Count;

        /// <summary>
        /// Starts an ability and runs it until it finishes or suspends.
        /// </summary>
        public ExecutionResult Execute(CompiledAbility ability, in ExecutionRequest request)
        {
            ExecutionId id = new ExecutionId((uint)services.ExecutionIds.Next());
            ExecutionContext context = new ExecutionContext(this, ability, services, id, request);

            // Inputs authored to be read at activation are captured now, before anything can change them.
            foreach (SnapshotInput input in ability.Snapshots)
            {
                input.Binding.TryResolve(services.Attributes, context.Source, context.Instigator, out double value);
                context.Snapshot[input.Slot] = value;
            }

            if (context.Tracing) context.Trace(TraceEventKind.ExecutionStarted, null, null, 0d, request.Source + ", instigator " + request.Instigator);
            context.Continue(ability.EntryOperation, new BranchFrame(default, 0, default, false, status: request.Status));
            Run(context);
            return new ExecutionResult(id, !context.Aborted);
        }

        /// <inheritdoc/>
        void IProjectileEvents.Impact(ContinuationToken token, in ProjectileImpact impact)
        {
            if (!continuations.Remove(token, out Continuation continuation))
            {
                return;
            }

            if (continuation.Context.Tracing) TraceResumed(continuation, "Projectile hit " + impact.Victim);
            continuation.Context.Continue(continuation.ImpactOperation, BranchFrame.ForImpact(impact).WithStatus(continuation.Context.Status));
            Run(continuation.Context);
        }

        /// <inheritdoc/>
        void IProjectileEvents.Expired(ContinuationToken token)
        {
            if (continuations.Remove(token, out Continuation continuation))
            {
                if (continuation.Context.Tracing) continuation.Context.Trace(TraceEventKind.Outcome, continuation.LaunchedBy, null, 0d, "Projectile ended without hitting anything.");
            }
        }

        /// <inheritdoc/>
        void IMeleeEvents.Hit(ContinuationToken token, in MeleeHit hit)
        {
            // A swing can hit many targets, so the continuation stays until the swing completes.
            if (!continuations.TryGetValue(token, out Continuation continuation))
            {
                return;
            }

            if (continuation.Context.Tracing) TraceResumed(continuation, "Swing hit " + hit.Victim + " (attack " + hit.AttackNumber + ")");
            continuation.Context.Continue(continuation.ImpactOperation, BranchFrame.ForMelee(hit).WithStatus(continuation.Context.Status));
            Run(continuation.Context);
        }

        /// <inheritdoc/>
        void IMeleeEvents.Completed(ContinuationToken token)
        {
            if (!continuations.Remove(token, out Continuation continuation))
            {
                return;
            }

            if (continuation.CompletedOperation >= 0)
            {
                if (continuation.Context.Tracing) TraceResumed(continuation, "Swing completed", completed: true);
                continuation.Context.Continue(continuation.CompletedOperation, new BranchFrame(default, 0, default, false));
                Run(continuation.Context);
            }
        }

        /// <summary>
        /// Suspends an execution until a delivery reports. Called by operations that launch something.
        /// </summary>
        /// <param name="context">The execution to suspend.</param>
        /// <param name="impactOperation">Where execution resumes when the delivery reports an impact or hit, or -1.</param>
        /// <param name="completedOperation">Where execution resumes when a swing completes, or -1.</param>
        internal ContinuationToken Suspend(ExecutionContext context, int impactOperation, int completedOperation = -1)
        {
            ContinuationToken token = new ContinuationToken((uint)services.ContinuationIds.Next());
            continuations.Add(token, new Continuation(context, impactOperation, completedOperation, context.CurrentNodeId));
            if (context.Tracing) context.Trace(TraceEventKind.Suspended, context.CurrentNodeId, null, token.Value, "Waiting for " + token);
            return token;
        }

        internal void Cancel(ContinuationToken token) => continuations.Remove(token);

        private void Run(ExecutionContext context)
        {
            while (context.TryTakeNext(out int operation, out BranchFrame frame))
            {
                if (context.OperationsRun >= services.MaxOperationsPerExecution)
                {
                    services.Report(context.Ability.Name + ": execution " + context.Id + " stopped after " + context.OperationsRun + " operations (the limit).");
                    if (context.Tracing) context.Trace(TraceEventKind.ExecutionStopped, context.CurrentNodeId, null, context.OperationsRun, "Stopped after " + context.OperationsRun + " operations (the limit).");
                    context.Aborted = true;
                    context.Clear();
                    return;
                }

                context.OperationsRun++;
                CompiledOperation current = context.Ability.Operations[operation];
                context.CurrentNodeId = current.NodeId;
                if (context.Tracing) context.Trace(TraceEventKind.NodeRan, current.NodeId, null, context.OperationsRun, null);
                current.Execute(context, frame);
            }
        }

        private static void TraceResumed(Continuation continuation, string text, bool completed = false)
        {
            int operation = completed ? continuation.CompletedOperation : continuation.ImpactOperation;
            string node = operation >= 0 ? continuation.Context.Ability.Operations[operation].NodeId : null;
            continuation.Context.Trace(TraceEventKind.Resumed, node, null, 0d, text);
        }

        private readonly struct Continuation
        {
            public Continuation(ExecutionContext context, int impactOperation, int completedOperation, string launchedBy)
            {
                LaunchedBy = launchedBy;
                Context = context;
                ImpactOperation = impactOperation;
                CompletedOperation = completedOperation;
            }

            /// <summary>The node that launched the projectile or swing, for traces.</summary>
            public string LaunchedBy { get; }

            public ExecutionContext Context { get; }

            public int ImpactOperation { get; }

            public int CompletedOperation { get; }
        }
    }
}
