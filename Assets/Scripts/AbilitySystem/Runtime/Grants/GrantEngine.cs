using SimpleRPG.Gameplay;
using System;
using System.Collections.Generic;
using UnityEngine;
using EntityId = SimpleRPG.Gameplay.EntityId;

namespace SimpleRPG.Abilities
{
    /// <summary>
    /// A change to an attribute that an item brings while it is worn: +3 Strength, +10% Constitution.
    /// </summary>
    [Serializable]
    public sealed class GrantSpec
    {
        [Tooltip("The attribute's stable key, for example simplerpg.attr.strength.")]
        public string AttributeKey = "";

        [Tooltip("Where the change applies: a flat amount, a percentage bonus, or a final factor.")]
        public ModifierLayer Layer = ModifierLayer.Add;

        [Tooltip("The amount: 3 for +3, 0.1 for +10% on the percent layer, 1.5 for x1.5 on the multiply layer.")]
        public float Value;
    }

    /// <summary>
    /// Tells the grant engine which attribute grants an equipped item brings. The game integration implements it over its items.
    /// </summary>
    public interface IItemGrantProvider
    {
        /// <summary>
        /// Gets the grants of an equipped item.
        /// </summary>
        /// <returns>False if the item is unknown or has none.</returns>
        bool TryGetGrants(EntityId wearer, ItemInstanceId item, out IReadOnlyList<GrantSpec> grants);

        /// <summary>
        /// Gets the attributes an item has of its own, such as a staff's magic power.
        /// </summary>
        /// <returns>False if the item is unknown or has none.</returns>
        bool TryGetFixedAttributes(EntityId wearer, ItemInstanceId item, out IReadOnlyList<AttributeValueSpec> fixedAttributes);

        /// <summary>
        /// Every item that is equipped right now, so an engine created late picks up equipment that was put on before it existed.
        /// </summary>
        IEnumerable<KeyValuePair<EntityId, ItemInstanceId>> EnumerateEquipped();
    }

    /// <summary>
    /// Applies the attribute grants of worn items. Server only. Each grant is one modifier on the wearer's attributes, owned by the item, and
    /// taking the item off releases exactly those modifiers. Stats are never added to on equip and subtracted from on removal, so nothing drifts
    /// and nothing else's bonus is ever removed by mistake.
    /// </summary>
    public sealed class GrantEngine : IDisposable
    {
        private readonly GameplayEventDispatcher events;
        private readonly AttributeSetRegistry attributes;
        private readonly IItemGrantProvider provider;
        private readonly Dictionary<(EntityId wearer, ItemInstanceId item), List<ModifierHandle>> held = new Dictionary<(EntityId, ItemInstanceId), List<ModifierHandle>>();
        private readonly List<SubscriptionHandle> subscriptions = new List<SubscriptionHandle>();

        public GrantEngine(GameplayEventDispatcher events, AttributeSetRegistry attributes, IItemGrantProvider provider)
        {
            this.events = events ?? throw new ArgumentNullException(nameof(events));
            this.attributes = attributes ?? throw new ArgumentNullException(nameof(attributes));
            this.provider = provider ?? throw new ArgumentNullException(nameof(provider));

            subscriptions.Add(events.Subscribe<ItemEquippedEvent>(e => Grant(e.Wearer, e.Item)));
            subscriptions.Add(events.Subscribe<ItemUnequippedEvent>(e => Release(e.Wearer, e.Item)));
            subscriptions.Add(events.Subscribe<EntityRemovedEvent>(OnEntityRemoved));

            // Equipment put on before this engine existed still counts.
            foreach (KeyValuePair<EntityId, ItemInstanceId> equipped in provider.EnumerateEquipped())
            {
                Grant(equipped.Key, equipped.Value);
            }
        }

        /// <summary>
        /// Raised with a description when a grant cannot be applied, for example it names no attribute.
        /// </summary>
        public event Action<string> Diagnostic;

        /// <summary>
        /// How many grant modifiers an entity currently holds from worn items.
        /// </summary>
        public int ActiveGrantCount(EntityId wearer)
        {
            int count = 0;
            foreach (KeyValuePair<(EntityId wearer, ItemInstanceId item), List<ModifierHandle>> pair in held)
            {
                if (pair.Key.wearer == wearer) count += pair.Value.Count;
            }

            return count;
        }

        public void Dispose()
        {
            foreach (SubscriptionHandle handle in subscriptions) events.Unsubscribe(handle);
            subscriptions.Clear();
            held.Clear();
        }

        private void Grant(EntityId wearer, ItemInstanceId item)
        {
            GiveFixedAttributes(wearer, item);

            if (held.ContainsKey((wearer, item)) || !provider.TryGetGrants(wearer, item, out IReadOnlyList<GrantSpec> grants))
            {
                return;
            }

            SourceHandle source = SourceHandle.FromItem(item);
            AttributeSet set = attributes.GetOrCreate(SourceHandle.FromEntity(wearer));
            List<ModifierHandle> handles = new List<ModifierHandle>();

            foreach (GrantSpec spec in grants)
            {
                if (spec == null || string.IsNullOrWhiteSpace(spec.AttributeKey) || float.IsNaN(spec.Value) || float.IsInfinity(spec.Value))
                {
                    Diagnostic?.Invoke("Skipped an item grant with no attribute or a value that is not a finite number.");
                    continue;
                }

                handles.Add(set.AddModifier(new AttributeModifier(new AttributeId(spec.AttributeKey), spec.Layer, spec.Value, source)));
            }

            if (handles.Count > 0)
            {
                held[(wearer, item)] = handles;
            }
        }

        /// <summary>
        /// An item's own attributes belong to the item, so abilities the item provides can read them whoever wields it.
        /// </summary>
        private void GiveFixedAttributes(EntityId wearer, ItemInstanceId item)
        {
            if (!provider.TryGetFixedAttributes(wearer, item, out IReadOnlyList<AttributeValueSpec> fixedAttributes))
            {
                return;
            }

            AttributeSet set = attributes.GetOrCreate(SourceHandle.FromItem(item));
            foreach (AttributeValueSpec spec in fixedAttributes)
            {
                if (spec == null || string.IsNullOrWhiteSpace(spec.AttributeKey) || float.IsNaN(spec.Value) || float.IsInfinity(spec.Value))
                {
                    Diagnostic?.Invoke("Skipped an item attribute with no key or a value that is not a finite number.");
                    continue;
                }

                set.SetBase(new AttributeId(spec.AttributeKey), spec.Value);
            }
        }

        private void Release(EntityId wearer, ItemInstanceId item)
        {
            attributes.Unregister(SourceHandle.FromItem(item));

            if (!held.Remove((wearer, item), out List<ModifierHandle> handles))
            {
                return;
            }

            if (attributes.TryGetSet(SourceHandle.FromEntity(wearer), out AttributeSet set))
            {
                foreach (ModifierHandle handle in handles)
                {
                    set.RemoveModifier(handle);
                }
            }
        }

        private void OnEntityRemoved(EntityRemovedEvent e)
        {
            foreach ((EntityId wearer, ItemInstanceId item) key in new List<(EntityId, ItemInstanceId)>(held.Keys))
            {
                if (key.wearer == e.Entity) held.Remove(key);
            }
        }
    }
}
