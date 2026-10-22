using System.Collections.Generic;

namespace SimpleRPG.Gameplay
{
    /// <summary>
    /// Why an item left a slot.
    /// </summary>
    public enum UnequipReason
    {
        /// <summary>It was taken off.</summary>
        Removed = 0,

        /// <summary>Another item was put in its slot.</summary>
        Replaced = 1,
    }

    /// <summary>
    /// An item was put in an equipment slot.
    /// </summary>
    public sealed class ItemEquippedEvent : GameplayEvent
    {
        public ItemEquippedEvent(EntityId wearer, int slot, ItemInstanceId item)
        {
            Wearer = wearer;
            Slot = slot;
            Item = item;
        }

        /// <summary>The entity wearing the item.</summary>
        public EntityId Wearer { get; }

        /// <summary>The slot, as the integer value of the game's equipment slot enum.</summary>
        public int Slot { get; }

        public ItemInstanceId Item { get; }
    }

    /// <summary>
    /// An item left an equipment slot.
    /// </summary>
    public sealed class ItemUnequippedEvent : GameplayEvent
    {
        public ItemUnequippedEvent(EntityId wearer, int slot, ItemInstanceId item, UnequipReason reason)
        {
            Wearer = wearer;
            Slot = slot;
            Item = item;
            Reason = reason;
        }

        public EntityId Wearer { get; }

        public int Slot { get; }

        public ItemInstanceId Item { get; }

        public UnequipReason Reason { get; }
    }

    /// <summary>
    /// What one entity has equipped, by slot. It holds only identities; the game's equipment component keeps the items themselves.
    /// Every change raises an event, so rules that depend on equipment (grants, on-hit effects) follow it without polling.
    /// An item can be in only one slot at a time. Server only.
    /// </summary>
    public sealed class EquipmentSet
    {
        private readonly EntityId wearer;

        private readonly GameplayEventDispatcher events;

        private readonly SortedDictionary<int, ItemInstanceId> slots = new SortedDictionary<int, ItemInstanceId>();

        public EquipmentSet(EntityId wearer, GameplayEventDispatcher events)
        {
            this.wearer = wearer;
            this.events = events;
        }

        /// <summary>
        /// The occupied slots and what is in them, in slot order.
        /// </summary>
        public IEnumerable<KeyValuePair<int, ItemInstanceId>> Items => slots;

        public int Count => slots.Count;

        /// <summary>
        /// Gets what is in a slot.
        /// </summary>
        public bool TryGet(int slot, out ItemInstanceId item) => slots.TryGetValue(slot, out item);

        /// <summary>
        /// True if the item is equipped in any slot.
        /// </summary>
        public bool IsEquipped(ItemInstanceId item)
        {
            foreach (KeyValuePair<int, ItemInstanceId> pair in slots)
            {
                if (pair.Value == item) return true;
            }

            return false;
        }

        /// <summary>
        /// Puts an item in a slot, taking out whatever was there first. The old item's unequip event comes before the new item's equip event.
        /// </summary>
        /// <returns>False if the item is invalid, already in this slot, or already equipped in another slot.</returns>
        public bool Equip(int slot, ItemInstanceId item)
        {
            if (!item.IsValid || IsEquipped(item))
            {
                return false;
            }

            if (slots.TryGetValue(slot, out ItemInstanceId previous))
            {
                events.Enqueue(new ItemUnequippedEvent(wearer, slot, previous, UnequipReason.Replaced));
            }

            slots[slot] = item;
            events.Enqueue(new ItemEquippedEvent(wearer, slot, item));
            DrainIfOutermost();
            return true;
        }

        /// <summary>
        /// Takes whatever is in a slot out.
        /// </summary>
        /// <returns>False if the slot was empty.</returns>
        public bool Unequip(int slot)
        {
            if (!slots.TryGetValue(slot, out ItemInstanceId item))
            {
                return false;
            }

            slots.Remove(slot);
            events.Enqueue(new ItemUnequippedEvent(wearer, slot, item, UnequipReason.Removed));
            DrainIfOutermost();
            return true;
        }

        private void DrainIfOutermost()
        {
            if (!events.IsDraining)
            {
                events.Drain();
            }
        }
    }
}
