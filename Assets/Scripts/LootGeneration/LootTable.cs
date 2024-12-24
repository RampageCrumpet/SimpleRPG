using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace SimpleRPG.InventorySystem.LootGeneration
{
    /// <summary>
    /// A loot table is a collection of items that can be dropped by a monster or found in a chest.
    /// </summary>
    public interface ILootTable : ILootableObject
    {
        [Header("Loot Table Settings")]
        /// <summary>
        /// The number of items to drop from this table.
        /// </summary>
        [Tooltip("The number of items to drop from this table.")]
        public int Count { get; set; }

        /// <summary>
        /// The items that can be dropped.
        /// </summary>
        [SerializeField]
        [Tooltip("The items that can be dropped.")]
        IEnumerable<ILootableObject> Contents { get; set; }
    }
}
