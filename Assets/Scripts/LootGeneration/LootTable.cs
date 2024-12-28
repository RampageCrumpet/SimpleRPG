using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace SimpleRPG.InventorySystem.LootGeneration
{
    /// <summary>
    /// This class is used to represent a collection of items that can drop and their respective drop chances.
    /// </summary>
    [CreateAssetMenu(fileName = "LootTable", menuName = "Scriptable Objects/LootTable")]
    public class LootTable : LootableObject
    {
        [Header("Loot Table Settings")]
        /// <summary>
        /// The number of items to drop from this table.
        /// </summary>
        [field: SerializeField]
        [Tooltip("The number of items to drop from this table.")]
        public int Count { get; set; }

        /// <inheritdoc/>
        [SerializeReference]
        [Tooltip("The items that can be dropped.")]
        public List<LootableObject> Contents;

        /// <inheritdoc/>
        public override IEnumerable<LootableObject> GetResult()
        {
            List<LootableObject> result = new List<LootableObject>();

            // Notify listeners that the loot table is about to be evaluated.
            foreach (LootableObject item in Contents)
            {
                item.OnPreResultEvaluation(EventArgs.Empty);
            }

            // Add the items that always drop.
            foreach (LootableObject item in Contents)
            {
                if (item.Always && item.Enabled)
                {
                    // Add the item to the result list and stop searching for the dropped item
                    result.AddRange(item.GetResult());
                    break;
                }
            }

            // Calculate the actual drops by picking a number of items from the loot table based on the count.
            while(result.Count() < Count)
            {
                //Only pick from the items that can be dropped.
                IEnumerable<LootableObject> droppableItems = Contents
                    // Filter out items that are not enabled
                    .Where(x => x.Enabled)
                    // Don't add the items that are set to always drop. We've already added those.
                    .Where(x=> !x.Always)
                    // Don't add the items that are set to unique and have already been added.
                    .Where(x => !(x.Unique && result.Contains(x)));


                if(!droppableItems.Any())
                {
                    Debug.LogWarning("No droppable items found in " + this.name + ".");
                    break;
                }

                // The actual roll on the drop table.
                // TODO: Casting to a float is dumb. I'm already paying for a double I should use it.
                double actualDropRoll = UnityEngine.Random.Range(0, (float)droppableItems.Sum(x => x.DropChance));

                // Counts up the weight of all of the objects we've already looped over.
                double runningDropChanceSum = 0;
                foreach (LootableObject item in droppableItems)
                {
                    runningDropChanceSum += item.DropChance;
                    if (runningDropChanceSum >= actualDropRoll)
                    {
                        // Add the item to the result list and stop searching for the dropped item
                        result.AddRange(item.GetResult());
                        break;
                    }
                }
            }

            // Notify listeners that the loot table has been evaluated.
            foreach (LootableObject item in Contents)
            {
                ResultEventArgs resultEventArgs = new ResultEventArgs(result);
                item.OnPostResultEvaluation(resultEventArgs);
            }

            return result;
        }
    }
}
