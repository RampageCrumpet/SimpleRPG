using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace SimpleRPG.InventorySystem.LootGeneration
{
    /// <summary>
    /// This class is used to represent a collection of items that can drop and their respective drop chances.
    /// </summary>
    [CreateAssetMenu(fileName = "WeightedRandomTable", menuName = "Scriptable Objects/WeightedRandomTable")]
    public class WeightedRandomTable : WeightedRandomEntry
    {
        [Header("Loot Table Settings")]
        /// <summary>
        /// The number of weighted rolls made on this table. Every roll either selects one entry or yields no drop
        /// (when nothing is eligible), so this bounds the number of selections. <see cref="WeightedRandomEntry.Always"/>
        /// entries are not counted against it.
        /// </summary>
        [field: SerializeField]
        [Tooltip("The number of weighted rolls made on this table. Always entries are added in addition to these rolls.")]
        public int Count { get; set; }

        /// <inheritdoc/>
        [SerializeReference]
        [Tooltip("The items that can be dropped.")]
        public List<WeightedRandomEntry> Contents;

        /// <inheritdoc/>
        public override IEnumerable<WeightedRandomEntry> GetResult()
        {
            return GetResult(null);
        }

        /// <inheritdoc/>
        public override IEnumerable<WeightedRandomEntry> GetResult(System.Random random)
        {
            List<WeightedRandomEntry> contents = Contents == null
                ? new List<WeightedRandomEntry>()
                : Contents.Where(x => x != null).ToList();

            List<WeightedRandomEntry> result = new List<WeightedRandomEntry>();

            // Notify listeners that the loot table is about to be evaluated.
            foreach (WeightedRandomEntry item in contents)
            {
                item.OnPreResultEvaluation(EventArgs.Empty);
            }

            // Every enabled Always entry drops, regardless of the weighted rolls.
            foreach (WeightedRandomEntry item in contents.Where(x => x.Always && x.Enabled))
            {
                item.OnObjectSelected(EventArgs.Empty);
                AddResult(result, item.GetResult(random));
            }

            // Make up to Count weighted rolls. Unique entries that have already dropped are excluded from later rolls.
            HashSet<WeightedRandomEntry> droppedUnique = new HashSet<WeightedRandomEntry>();
            for (int roll = 0; roll < Count; roll++)
            {
                WeightedRandomEntry picked = PickEntry(contents, random, droppedUnique);

                // Nothing is eligible (empty table, all weights zero, or every unique entry used): this roll drops nothing.
                if (picked == null)
                {
                    break;
                }

                if (picked.Unique)
                {
                    droppedUnique.Add(picked);
                }

                picked.OnObjectSelected(EventArgs.Empty);
                AddResult(result, picked.GetResult(random));
            }

            // Notify listeners that the loot table has been evaluated.
            ResultEventArgs resultEventArgs = new ResultEventArgs(result);
            foreach (WeightedRandomEntry item in contents)
            {
                item.OnPostResultEvaluation(resultEventArgs);
            }

            return result;
        }

        /// <summary>
        /// Selects one eligible entry with probability proportional to its weight. Entries with a zero weight are never
        /// selected; entries with a negative, NaN or infinite weight are ignored with a warning.
        /// </summary>
        /// <returns>The selected entry, or null if no entry is eligible.</returns>
        private WeightedRandomEntry PickEntry(List<WeightedRandomEntry> contents, System.Random random, HashSet<WeightedRandomEntry> droppedUnique)
        {
            List<KeyValuePair<WeightedRandomEntry, double>> candidates = new List<KeyValuePair<WeightedRandomEntry, double>>();
            double totalWeight = 0;

            foreach (WeightedRandomEntry item in contents)
            {
                // Always entries are handled separately, and disabled or already-dropped unique entries cannot be picked.
                if (!item.Enabled || item.Always || (item.Unique && droppedUnique.Contains(item)))
                {
                    continue;
                }

                double weight = item.DropChance;
                if (double.IsNaN(weight) || double.IsInfinity(weight) || weight < 0)
                {
                    Debug.LogWarning($"Ignoring entry '{item.name}' in {name}: drop chance {weight} is not a finite non-negative weight.");
                    continue;
                }

                if (weight == 0)
                {
                    continue;
                }

                candidates.Add(new KeyValuePair<WeightedRandomEntry, double>(item, weight));
                totalWeight += weight;
            }

            if (candidates.Count == 0)
            {
                return null;
            }

            if (double.IsInfinity(totalWeight))
            {
                Debug.LogWarning($"Total drop weight of {name} overflows; this roll drops nothing.");
                return null;
            }

            // Walk the cumulative weights until we pass the roll. The roll is in [0, totalWeight).
            double rollValue = NextUnit(random) * totalWeight;
            double runningWeight = 0;
            foreach (KeyValuePair<WeightedRandomEntry, double> candidate in candidates)
            {
                runningWeight += candidate.Value;
                if (runningWeight > rollValue)
                {
                    return candidate.Key;
                }
            }

            // Floating point rounding can leave the running total just short of the roll; the last candidate owns that sliver.
            return candidates[candidates.Count - 1].Key;
        }

        private static double NextUnit(System.Random random)
        {
            return random != null ? random.NextDouble() : UnityEngine.Random.value;
        }

        private static void AddResult(List<WeightedRandomEntry> result, IEnumerable<WeightedRandomEntry> drops)
        {
            if (drops == null)
            {
                return;
            }

            foreach (WeightedRandomEntry drop in drops)
            {
                if (drop != null)
                {
                    result.Add(drop);
                }
            }
        }
    }
}
