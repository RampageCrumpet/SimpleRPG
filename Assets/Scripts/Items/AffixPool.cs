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
        [Min(0)]
        [Tooltip("The fewest affixes an instance rolls.")]
        private int minimumCount = 1;

        [SerializeField]
        [Min(0)]
        [Tooltip("The most affixes an instance rolls.")]
        private int maximumCount = 2;

        [SerializeField]
        [Min(0)]
        [Tooltip("The most prefixes an instance rolls.")]
        private int maximumPrefixes = 1;

        [SerializeField]
        [Min(0)]
        [Tooltip("The most suffixes an instance rolls.")]
        private int maximumSuffixes = 1;

        [SerializeField]
        [Tooltip("The words rare items of this pool are named from. Empty: its items are never rare.")]
        private RareNameComponents rareNames;

        [SerializeField]
        [Min(0)]
        [Tooltip("An instance that rolls at least this many affixes is rare and gets a generated name. Zero: never.")]
        private int rareFromCount;

        public IReadOnlyList<AffixDefinition> Affixes => affixes;

        public RareNameComponents RareNames => rareNames;

        public int RareFromCount => rareFromCount;

        public int MinimumCount => minimumCount;

        public int MaximumCount => maximumCount;

        public int MaximumPrefixes => maximumPrefixes;

        public int MaximumSuffixes => maximumSuffixes;

        /// <summary>
        /// Everything wrong with this pool for a game a player can find loot in: an affix that is missing, badly authored or that nothing in the
        /// game reads, or a pool whose largest possible roll does not fit the space an item has for affixes. Empty if the pool is fit to ship.
        /// </summary>
        public List<string> Problems()
        {
            var problems = new List<string>();
            var footprints = new List<int>();
            foreach (AffixDefinition affix in affixes)
            {
                if (affix == null)
                {
                    problems.Add(name + " has an empty entry.");
                    continue;
                }

                string problem = affix.Problem();
                if (problem != null)
                {
                    problems.Add(name + ": " + problem);
                    continue;
                }

                if (!affix.IsLive(out string reason)) problems.Add(name + " holds an affix that does nothing: " + reason);
                footprints.Add(ItemAffixes.Footprint(affix.ValueCount));
            }

            if (rareFromCount > 0)
            {
                if (rareNames == null) problems.Add(name + " makes items rare from " + rareFromCount + " affixes but has no words to name them.");
                else if (rareNames.Problem() != null) problems.Add(rareNames.Problem());
                if (rareFromCount > maximumCount) problems.Add(name + " makes items rare from " + rareFromCount + " affixes but never rolls more than " + maximumCount + ".");
            }

            // The most space a roll of the allowed size can take: the largest affixes, as many as may roll.
            footprints.Sort((a, b) => b.CompareTo(a));
            int take = Mathf.Min(maximumCount, footprints.Count);
            int worst = 0;
            for (int i = 0; i < take; i++) worst += footprints[i];
            if (worst > ItemAffixes.WordCapacity)
            {
                problems.Add(name + " can roll " + take + " affixes needing " + worst + " words, but an item has room for " + ItemAffixes.WordCapacity + ".");
            }

            return problems;
        }
    }
}
