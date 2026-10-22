using System;
using System.Collections.Generic;

namespace SimpleRPG.Gameplay
{
    /// <summary>
    /// The mutable state of item instances, such as how many charges a staff has left. It is kept apart from item definitions, which are shared
    /// and never change: two staffs made from one definition each have their own state here, found by their <see cref="ItemInstanceId"/>.
    /// The state follows the instance wherever it goes, because it is keyed by instance and not by who holds it.
    /// Server only. A counter can be spent but not refilled: restoring charges is deliberately not provided yet.
    /// </summary>
    public sealed class ItemStateStore
    {
        private readonly Dictionary<ItemInstanceId, Dictionary<DefinitionId, int>> counters =
            new Dictionary<ItemInstanceId, Dictionary<DefinitionId, int>>();

        /// <summary>
        /// Raised after a counter is created or changes: the instance, the counter and its new value.
        /// </summary>
        public event Action<ItemInstanceId, DefinitionId, int> Changed;

        /// <summary>
        /// The number of item instances with state.
        /// </summary>
        public int InstanceCount => counters.Count;

        /// <summary>
        /// Gives an instance a counter with a starting value, if it doesn't have that counter yet. An existing counter is never reset,
        /// so calling this each time an item is loaded or moved is safe.
        /// </summary>
        /// <returns>True if the counter was created.</returns>
        public bool EnsureCounter(ItemInstanceId item, DefinitionId counter, int initialValue)
        {
            if (!item.IsValid) throw new ArgumentException("An item instance needs a valid ID.", nameof(item));
            if (counter.IsNone) throw new ArgumentException("A counter needs an ID.", nameof(counter));
            if (initialValue < 0) throw new ArgumentOutOfRangeException(nameof(initialValue), "A counter cannot start below zero.");

            if (!counters.TryGetValue(item, out Dictionary<DefinitionId, int> forItem))
            {
                forItem = new Dictionary<DefinitionId, int>();
                counters.Add(item, forItem);
            }

            if (forItem.ContainsKey(counter))
            {
                return false;
            }

            forItem.Add(counter, initialValue);
            Changed?.Invoke(item, counter, initialValue);
            return true;
        }

        /// <summary>
        /// Reads a counter.
        /// </summary>
        public bool TryGetCounter(ItemInstanceId item, DefinitionId counter, out int value)
        {
            value = 0;
            return counters.TryGetValue(item, out Dictionary<DefinitionId, int> forItem) && forItem.TryGetValue(counter, out value);
        }

        /// <summary>
        /// Spends from a counter, all or nothing: if there is not enough, nothing is taken.
        /// </summary>
        /// <param name="amount">What to spend, at least 1.</param>
        /// <returns>True if the amount was spent.</returns>
        public bool TrySpend(ItemInstanceId item, DefinitionId counter, int amount)
        {
            if (amount < 1) throw new ArgumentOutOfRangeException(nameof(amount), "Spend at least 1.");

            if (!counters.TryGetValue(item, out Dictionary<DefinitionId, int> forItem) || !forItem.TryGetValue(counter, out int value) || value < amount)
            {
                return false;
            }

            forItem[counter] = value - amount;
            Changed?.Invoke(item, counter, value - amount);
            return true;
        }

        /// <summary>
        /// Forgets all state for an instance, for example when the item is destroyed.
        /// </summary>
        /// <returns>True if it had any state.</returns>
        public bool Remove(ItemInstanceId item) => counters.Remove(item);
    }
}
