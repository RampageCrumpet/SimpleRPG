using SimpleRPG.Gameplay;
using System;
using System.Collections.Generic;

namespace SimpleRPG.Abilities
{
    /// <summary>
    /// Tells the item host which triggered effects a worn item brings. The game integration implements it over its items (the effects the item's
    /// asset declares and those its affixes bring).
    /// </summary>
    public interface IItemEffectProvider
    {
        /// <summary>
        /// Gets the effects of an equipped item.
        /// </summary>
        /// <returns>False if the item is unknown or has none.</returns>
        bool TryGetEffects(EntityId wearer, ItemInstanceId item, out IReadOnlyList<TriggeredEffect> effects);

        /// <summary>
        /// Every item that is equipped right now, so a host created late can pick up equipment that was put on before it existed.
        /// </summary>
        IEnumerable<KeyValuePair<EntityId, ItemInstanceId>> EnumerateEquipped();
    }

    /// <summary>
    /// A worn item as a host of triggered effects: its effects are active while it is equipped and stop when it is taken off. The item is the
    /// effects' host (<see cref="SourceHandle.FromItem"/>), so an effect with <see cref="EffectScope.Host"/> reacts to what this item did.
    /// </summary>
    public sealed class ItemEffectHost : IDisposable
    {
        private readonly GameplayEventDispatcher events;
        private readonly TriggeredEffectEngine engine;
        private readonly IItemEffectProvider provider;
        private readonly List<SubscriptionHandle> subscriptions = new List<SubscriptionHandle>();

        public ItemEffectHost(GameplayEventDispatcher events, TriggeredEffectEngine engine, IItemEffectProvider provider)
        {
            this.events = events ?? throw new ArgumentNullException(nameof(events));
            this.engine = engine ?? throw new ArgumentNullException(nameof(engine));
            this.provider = provider ?? throw new ArgumentNullException(nameof(provider));

            subscriptions.Add(events.Subscribe<ItemEquippedEvent>(e => Activate(e.Wearer, e.Item)));
            subscriptions.Add(events.Subscribe<ItemUnequippedEvent>(e => engine.Deactivate(e.Wearer, SourceHandle.FromItem(e.Item))));

            // Equipment put on before this host existed still counts.
            foreach (KeyValuePair<EntityId, ItemInstanceId> equipped in provider.EnumerateEquipped())
            {
                Activate(equipped.Key, equipped.Value);
            }
        }

        public void Dispose()
        {
            foreach (SubscriptionHandle handle in subscriptions)
            {
                events.Unsubscribe(handle);
            }

            subscriptions.Clear();
        }

        private void Activate(EntityId wearer, ItemInstanceId item)
        {
            if (provider.TryGetEffects(wearer, item, out IReadOnlyList<TriggeredEffect> effects))
            {
                engine.Activate(wearer, SourceHandle.FromItem(item), effects);
            }
        }
    }
}
