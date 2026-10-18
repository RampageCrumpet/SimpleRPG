using System.Collections.Generic;
using UnityEngine;

namespace SimpleRPG.InventorySystem
{
    /// <summary>
    /// Maps item assets to small integer IDs so inventories can replicate "which item" without sending ScriptableObject data.
    /// An ID is the item's position in <see cref="items"/> plus one (0 means "no item"), so new items must only be APPENDED;
    /// reordering or removing entries changes the IDs and breaks compatibility between builds.
    /// This is a deliberately minimal lookup; see bead simplerpg-7du.6.16 (D5) for the broader serialization decision.
    /// </summary>
    [CreateAssetMenu(fileName = "ItemRegistry", menuName = "Items/Item Registry")]
    public class ItemRegistry : ScriptableObject
    {
        /// <summary>
        /// The Resources path the registry asset is loaded from.
        /// </summary>
        public const string ResourcePath = "ItemRegistry";

        [SerializeField]
        [Tooltip("Every item that can appear in a networked inventory. Only append; the position is the item's network ID.")]
        private List<Item> items = new List<Item>();

        private static ItemRegistry instance;

        /// <summary>
        /// The registry used by inventories. Loaded from Resources on first use.
        /// </summary>
        public static ItemRegistry Instance
        {
            get
            {
                if (instance == null)
                {
                    instance = Resources.Load<ItemRegistry>(ResourcePath);
                }

                return instance;
            }
            set
            {
                instance = value;
            }
        }

        /// <summary>
        /// Creates a registry from a list of items. Intended for tests and tooling.
        /// </summary>
        public static ItemRegistry Create(IEnumerable<Item> registeredItems)
        {
            ItemRegistry registry = CreateInstance<ItemRegistry>();
            registry.items = new List<Item>(registeredItems);
            return registry;
        }

        /// <summary>
        /// Gets the network ID of an item or of the asset a cloned item was created from.
        /// </summary>
        /// <returns>The ID, or 0 if the item isn't registered.</returns>
        public int GetId(Item item)
        {
            if (item == null)
            {
                return 0;
            }

            int index = items.IndexOf(item.SourceAsset);
            return index + 1;
        }

        /// <summary>
        /// Gets the item asset for a network ID.
        /// </summary>
        /// <returns>The item asset, or null if the ID isn't valid.</returns>
        public Item GetItem(int id)
        {
            int index = id - 1;
            return index >= 0 && index < items.Count ? items[index] : null;
        }
    }
}
