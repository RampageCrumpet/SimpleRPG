using System;
using System.Collections.Generic;
using UnityEngine;

namespace SimpleRPG.InventorySystem.LootGeneration
{
    [CreateAssetMenu(fileName = "LootableItem", menuName = "Scriptable Objects/Lootable Item")]
    [Serializable]
    public class LootableItem  : LootableObject
    {
        /// <summary>
        /// The item to be dropped.
        /// </summary>
        [field: SerializeField]
        [Tooltip("The item to be dropped.")]
        public Item Item { get; set; }

        /// <inheritdoc/>
        public override IEnumerable<LootableObject> GetResult()
        {
            List<LootableObject> result = new List<LootableObject>();
            result.Add(this);
            return result;
        }
    }
}
