using System;
using System.Collections.Generic;
using UnityEngine;

namespace SimpleRPG.InventorySystem.LootGeneration
{
    [CreateAssetMenu(fileName = "LootableItem", menuName = "Scriptable Objects/Lootable Item")]
    [Serializable]
    public class LootableItem  : WeightedRandomEntry
    {
        /// <summary>
        /// The item to be dropped.
        /// </summary>
        [field: SerializeField]
        [Tooltip("The item to be dropped.")]
        public Item Item { get; set; }

        /// <inheritdoc/>
        public override IEnumerable<WeightedRandomEntry> GetResult()
        {
            List<WeightedRandomEntry> result = new List<WeightedRandomEntry>();
            result.Add(this);
            return result;
        }
    }
}
