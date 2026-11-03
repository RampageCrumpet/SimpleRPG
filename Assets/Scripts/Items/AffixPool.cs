using System.Collections.Generic;
using UnityEngine;

namespace SimpleRPG.InventorySystem
{
    /// <summary>
    /// The affixes an item can roll and how many. An item points at a pool; every new instance of it rolls from the pool once, as it comes
    /// into the world, and keeps the result. An item with no pool has no affixes and the game rolls nothing for it.
    /// </summary>
    [CreateAssetMenu(fileName = "AffixPool", menuName = "Items/Affix Pool")]
    public class AffixPool : ScriptableObject
    {
        [SerializeField]
        [Tooltip("The affixes that can roll.")]
        private List<AffixDefinition> affixes = new List<AffixDefinition>();

        [SerializeField]
        [Range(0, ItemAffixes.MaxAffixes)]
        [Tooltip("The fewest affixes an instance rolls.")]
        private int minimumCount = 1;

        [SerializeField]
        [Range(0, ItemAffixes.MaxAffixes)]
        [Tooltip("The most affixes an instance rolls.")]
        private int maximumCount = 2;

        public IReadOnlyList<AffixDefinition> Affixes => affixes;

        public int MinimumCount => minimumCount;

        public int MaximumCount => maximumCount;
    }
}
