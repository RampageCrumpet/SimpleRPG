using SimpleRPG.Gameplay;
using System;
using System.Collections.Generic;

namespace SimpleRPG.Abilities
{
    /// <summary>
    /// Tells the status host which triggered effects a status brings. The game integration implements it over the definition registry.
    /// </summary>
    public interface IStatusEffectProvider
    {
        /// <returns>False if the status is unknown or brings none.</returns>
        bool TryGetEffects(DefinitionId status, out IReadOnlyList<TriggeredEffect> effects);
    }

    /// <summary>
    /// A status as a host of triggered effects: its effects are active for the holder from the moment the status is applied until its last stack
    /// ends. This is how a spell's effects last after it is cast: the spell applies a status, and the status brings the effects. Each applied
    /// status (each group) is its own host, so effects of two statuses never share state.
    /// </summary>
    public sealed class StatusEffectHost : IDisposable
    {
        private readonly GameplayEventDispatcher events;
        private readonly TriggeredEffectEngine engine;
        private readonly IStatusEffectProvider provider;
        private readonly List<SubscriptionHandle> subscriptions = new List<SubscriptionHandle>();

        public StatusEffectHost(GameplayEventDispatcher events, TriggeredEffectEngine engine, IStatusEffectProvider provider)
        {
            this.events = events ?? throw new ArgumentNullException(nameof(events));
            this.engine = engine ?? throw new ArgumentNullException(nameof(engine));
            this.provider = provider ?? throw new ArgumentNullException(nameof(provider));

            subscriptions.Add(events.Subscribe<StatusAppliedEvent>(OnApplied));
            subscriptions.Add(events.Subscribe<StatusRemovedEvent>(e => engine.Deactivate(e.Target, SourceHandle.FromStatus(e.Group))));
        }

        public void Dispose()
        {
            foreach (SubscriptionHandle handle in subscriptions)
            {
                events.Unsubscribe(handle);
            }

            subscriptions.Clear();
        }

        private void OnApplied(StatusAppliedEvent e)
        {
            if (provider.TryGetEffects(e.Status, out IReadOnlyList<TriggeredEffect> effects))
            {
                engine.Activate(e.Target, SourceHandle.FromStatus(e.Group), effects);
            }
        }
    }
}
