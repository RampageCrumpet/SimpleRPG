using System;
using System.Collections.Generic;
using UnityEngine;

namespace SimpleRPG.InventorySystem.LootGeneration
{
    /// <summary>
    /// An object that can be dropped by the random loot system.
    /// </summary>
    [Serializable]
    public abstract class WeightedRandomEntry : ScriptableObject
    {
        [Header("Drop Settings")]
        /// <summary>
        /// The chance the item can be dropped as a relative weight.
        /// </summary>
        [field: SerializeField]
        [Tooltip("The chance the item is dropped as a relative weight.")]
        public double DropChance { get; set; }

        /// <summary>
        /// The item can only be dropped once per query.
        /// </summary>]
        [field: SerializeField]
        [Tooltip("The item can only be dropped once per query.")]
        public bool Unique { get; set; }

        /// <summary>
        /// The item is always be dropped.
        /// </summary>
        [field: SerializeField]
        [Tooltip("The item is always be dropped.")]
        public bool Always { get; set; }

        /// <summary>
        /// The item dropping is enabled.
        /// </summary>
        [field: SerializeField]
        [Tooltip("The item dropping is enabled.")]
        public bool Enabled { get; set; } = true;

        /// <summary>
        /// How much better (positive) or worse (negative) the loot below this entry is than the area's ordinary loot, in item levels. It adds up
        /// along the nested tables an item came through. The area's ceiling still holds.
        /// </summary>
        [field: SerializeField]
        [Tooltip("How many item levels better (or worse, if negative) than the area's ordinary loot everything under this entry is. Adds up through nested tables; the area's ceiling still holds.")]
        public int ItemLevelOffset { get; set; }

        /// <summary>
        /// Gets the resulting loot drop from this lootable object.
        /// </summary>
        /// <returns> An IEnumerable containing all of the loot drops from the object</returns>
        public abstract IEnumerable<WeightedRandomEntry> GetResult();

        /// <summary>
        /// Gets the resulting loot drop using the given random source. Entries that roll internally (such as nested tables)
        /// override this so a seeded <see cref="System.Random"/> reaches them; everything else falls back to <see cref="GetResult()"/>.
        /// </summary>
        /// <param name="random">The random source, or null to use <c>UnityEngine.Random</c>.</param>
        public virtual IEnumerable<WeightedRandomEntry> GetResult(System.Random random)
        {
            return GetResult();
        }

        /// <summary>
        /// Like <see cref="GetResult(System.Random)"/>, but each result carries the item level offset of the entries it came through. A table
        /// overrides it to add its own offset to what its entries produce.
        /// </summary>
        public virtual IEnumerable<LootDrop> GetDrops(System.Random random)
        {
            foreach (WeightedRandomEntry entry in GetResult(random))
            {
                yield return new LootDrop(entry, ItemLevelOffset);
            }
        }
    }
}
