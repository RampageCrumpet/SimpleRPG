using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

namespace SimpleRPG.InventorySystem.LootGeneration
{
    [CreateAssetMenu(fileName = "LootableItem", menuName = "Scriptable Objects/Lootable Item")]
    [Serializable]
    public class LootableItem  : WeightedRandomEntry
    {
        /// <summary>
        /// The item to be dropped.
        /// </summary>
        [SerializeField]
        [FormerlySerializedAs("<Item>k__BackingField")]
        [Tooltip("The item to be dropped.")]
        private Item item;

        public Item Item
        {
            get => item;
            set => item = value;
        }

        /// <inheritdoc/>
        public override IEnumerable<WeightedRandomEntry> GetResult()
        {
            List<WeightedRandomEntry> result = new List<WeightedRandomEntry>();
            result.Add(this);
            return result;
        }
    }
}
