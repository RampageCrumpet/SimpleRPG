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

        /// <inheritdoc/>
        public event EventHandler PreResultEvaluation;

        /// <inheritdoc/>
        public event EventHandler PostResultEvaluation;

        /// <inheritdoc/>
        public event EventHandler ObjectSelected;

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

        // Fires the ObjectSelected event. Called when the object is selected to be included in the loot drop.
        public void OnObjectSelected(EventArgs e)
        {
            ObjectSelected?.Invoke(this, e);
        }

        // Fires the PostResultEvaluation event. Called after the results have been calculated and the result set is complete, but before the results are returned.
        public void OnPostResultEvaluation(ResultEventArgs e)
        {
            PostResultEvaluation?.Invoke(this, e);
        }

        // Fires the PreResultEvaluation event. Called before all of the probabilities of all items of the current LootTable have been calculated.
        public void OnPreResultEvaluation(EventArgs e)
        {
            PreResultEvaluation?.Invoke(this, e);
        }
    }
}
