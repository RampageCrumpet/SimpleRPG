using System;
using System.Collections.Generic;

namespace SimpleRPG.Gameplay
{
    /// <summary>
    /// Identifies one subscription so it can be removed.
    /// </summary>
    public readonly struct SubscriptionHandle
    {
        internal SubscriptionHandle(Type eventType, uint id)
        {
            EventType = eventType;
            Id = id;
        }

        internal Type EventType { get; }

        internal uint Id { get; }

        public bool IsValid => Id != 0;
    }

    /// <summary>
    /// How a call to <see cref="GameplayEventDispatcher.Drain"/> ended.
    /// </summary>
    public readonly struct DrainResult
    {
        public DrainResult(int dispatched, bool budgetExceeded)
        {
            Dispatched = dispatched;
            BudgetExceeded = budgetExceeded;
        }

        /// <summary>Events delivered.</summary>
        public int Dispatched { get; }

        /// <summary>True if the event budget ran out and the remaining queued events were dropped.</summary>
        public bool BudgetExceeded { get; }
    }

    /// <summary>
    /// Delivers gameplay events in the order they were queued. Events raised by a handler wait their turn instead of running inside it, so a
    /// chain such as damage, death, item reaction, spawn never re-enters a half-finished step. Depth and per-drain budgets stop a runaway chain.
    /// </summary>
    public sealed class GameplayEventDispatcher
    {
        public const int DefaultMaxEventsPerDrain = 256;

        public const int DefaultMaxDepth = 8;

        private readonly Queue<GameplayEvent> queue = new Queue<GameplayEvent>();

        private readonly Dictionary<Type, List<Subscription>> subscriptions = new Dictionary<Type, List<Subscription>>();

        private long lastSequence;

        // Set once a drain runs out of budget: from then on only events that must be delivered are kept.
        private bool cleanupOnly;

        private uint lastSubscription;

        public GameplayEventDispatcher(int maxEventsPerDrain = DefaultMaxEventsPerDrain, int maxDepth = DefaultMaxDepth)
        {
            MaxEventsPerDrain = maxEventsPerDrain;
            MaxDepth = maxDepth;
        }

        public int MaxEventsPerDrain { get; }

        /// <summary>The deepest cause chain allowed: an event of depth greater than this is refused.</summary>
        public int MaxDepth { get; }

        /// <summary>True while events are being delivered.</summary>
        public bool IsDraining { get; private set; }

        /// <summary>
        /// The depth of the event being delivered, or the depth an event raised outside any handler would have (zero).
        /// </summary>
        public int CurrentDepth { get; private set; }

        /// <summary>The depth a newly queued event would get.</summary>
        public int NextEventDepth => IsDraining ? CurrentDepth + 1 : 0;

        /// <summary>Events waiting to be delivered.</summary>
        public int PendingCount => queue.Count;

        /// <summary>
        /// Raised with a description when something is refused or goes wrong: a handler throws, depth or budget is exceeded.
        /// </summary>
        public event Action<string> Diagnostic;

        /// <summary>
        /// Calls <paramref name="handler"/> for every event of exactly type <typeparamref name="T"/>, in the order handlers subscribed.
        /// </summary>
        public SubscriptionHandle Subscribe<T>(Action<T> handler) where T : GameplayEvent
        {
            if (handler == null) throw new ArgumentNullException(nameof(handler));

            Type type = typeof(T);
            if (!subscriptions.TryGetValue(type, out List<Subscription> list))
            {
                list = new List<Subscription>();
                subscriptions.Add(type, list);
            }

            uint id = ++lastSubscription;
            list.Add(new Subscription(id, e => handler((T)e)));
            return new SubscriptionHandle(type, id);
        }

        /// <summary>
        /// Stops a subscription. Takes effect for events not yet delivered, even mid-drain.
        /// </summary>
        /// <returns>True if it was still subscribed.</returns>
        public bool Unsubscribe(SubscriptionHandle handle)
        {
            return handle.IsValid && subscriptions.TryGetValue(handle.EventType, out List<Subscription> list) && list.RemoveAll(x => x.Id == handle.Id) > 0;
        }

        /// <summary>
        /// Queues an event. It is delivered by <see cref="Drain"/>, after everything already queued.
        /// </summary>
        /// <returns>False if the event is too deep and was dropped.</returns>
        public bool Enqueue(GameplayEvent gameplayEvent)
        {
            if (gameplayEvent == null) throw new ArgumentNullException(nameof(gameplayEvent));

            if (cleanupOnly && !gameplayEvent.MustDeliver)
            {
                return false;
            }

            int depth = NextEventDepth;
            if (depth > MaxDepth && !gameplayEvent.MustDeliver)
            {
                Report("Dropped " + gameplayEvent.GetType().Name + ": its cause chain is " + depth + " events deep, the limit is " + MaxDepth + ".");
                return false;
            }

            gameplayEvent.Sequence = ++lastSequence;
            gameplayEvent.Depth = depth;
            queue.Enqueue(gameplayEvent);
            return true;
        }

        /// <summary>
        /// Delivers queued events, including any queued while delivering, until none are left or the budget runs out.
        /// Does nothing if a drain is already running; the running one will deliver the new events.
        /// </summary>
        public DrainResult Drain()
        {
            if (IsDraining)
            {
                return new DrainResult(0, false);
            }

            IsDraining = true;
            int dispatched = 0;
            bool exceeded = false;
            try
            {
                while (queue.Count > 0)
                {
                    if (!cleanupOnly && dispatched >= MaxEventsPerDrain)
                    {
                        // Everything still waiting is dropped except cleanup after deaths, which must run or a dead entity would keep its state.
                        List<GameplayEvent> keep = new List<GameplayEvent>();
                        foreach (GameplayEvent waiting in queue)
                        {
                            if (waiting.MustDeliver) keep.Add(waiting);
                        }

                        Report("Event budget of " + MaxEventsPerDrain + " exceeded; dropped " + (queue.Count - keep.Count) + " queued events.");
                        queue.Clear();
                        foreach (GameplayEvent waiting in keep) queue.Enqueue(waiting);
                        exceeded = true;
                        cleanupOnly = true;
                        continue;
                    }

                    GameplayEvent next = queue.Dequeue();
                    CurrentDepth = next.Depth;
                    Deliver(next);
                    dispatched++;
                }
            }
            finally
            {
                IsDraining = false;
                CurrentDepth = 0;
                cleanupOnly = false;
            }

            return new DrainResult(dispatched, exceeded);
        }

        private void Deliver(GameplayEvent gameplayEvent)
        {
            if (!subscriptions.TryGetValue(gameplayEvent.GetType(), out List<Subscription> list) || list.Count == 0)
            {
                return;
            }

            // Delivered from a copy so a handler may subscribe or unsubscribe; an unsubscribed handler is skipped.
            Subscription[] snapshot = list.ToArray();
            foreach (Subscription subscription in snapshot)
            {
                if (!list.Exists(x => x.Id == subscription.Id)) continue;

                try
                {
                    subscription.Handler(gameplayEvent);
                }
                catch (Exception exception)
                {
                    Report("A handler for " + gameplayEvent.GetType().Name + " threw " + exception.GetType().Name + ": " + exception.Message);
                }
            }
        }

        /// <summary>
        /// Reports a problem to whoever listens for diagnostics: something was dropped or refused and the cause should be findable.
        /// </summary>
        public void Report(string message)
        {
            Diagnostic?.Invoke(message);
        }

        private readonly struct Subscription
        {
            public Subscription(uint id, Action<GameplayEvent> handler)
            {
                Id = id;
                Handler = handler;
            }

            public uint Id { get; }

            public Action<GameplayEvent> Handler { get; }
        }
    }
}
