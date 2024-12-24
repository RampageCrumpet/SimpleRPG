using System;
using UnityEngine;

namespace SimpleRPG.InventorySystem.LootGeneration
{
    /// <summary>
    /// An object that can be dropped by the random loot system.
    /// </summary>
    public interface ILootableObject
    {
        [Header("Drop Settings")]
        /// <summary>
        /// The chance the item can be dropped as a relative weight.
        /// </summary>
        [SerializeField]
        [Tooltip("The chance the item is dropped as a relative weight.")]
        public double DropChance { get; set; }

        /// <summary>
        /// The item can only be dropped once per query.
        /// </summary>]
        [SerializeField]
        [Tooltip("The item can only be dropped once per query.")]
        public bool Unique { get; set; }

        /// <summary>
        /// The item is always be dropped.
        /// </summary>
        [SerializeField]
        [Tooltip("The item is always be dropped.")]
        public bool Always { get; set; }

        /// <summary>
        /// The item dropping is enabled.
        /// </summary>
        [SerializeField]
        [Tooltip("The item dropping is enabled.")]
        public bool Enabled { get; set; }

        /// <summary>
        /// Occurs before all of the probabilities of all items of the current <see cref="LootTable"/> have been calculated.
        /// This is the moment to modify any settings immediately before a result is calculated.
        /// </summary>
        event EventHandler PreResultEvaluation;

        /// <summary>
        /// Occurs after the results have been calculated and the result set is complete, but before the results are returned.
        /// </summary>
        event EventHandler PostResultEvaluation;

        /// <summary>
        /// Occurs when this object is selected to be included in the loot drop.
        /// </summary>
        event EventHandler ObjectSelected;

        void OnPreResultEvaluation(EventArgs e);
        void OnPostResultEvaluation(EventArgs e);
        void OnObjectSelected(EventArgs e);
    }
}
