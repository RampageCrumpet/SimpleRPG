using UnityEngine;
using SimpleRPG.InventorySystem.LootGeneration;
using System.Collections.Generic;

namespace SimpleRPG
{
    /// <summary>
    /// One possible encounter in an encounter table: an enemy that can be spawned. It reuses the weighted-table machinery that loot uses
    /// (weights, always, unique, nested tables) but is a spawn concept, not a drop.
    /// </summary>
    [CreateAssetMenu(fileName = "EncounterEntry", menuName = "Encounters/Encounter entry")]
    public class EncounterEntry : WeightedRandomEntry
    {
        [Tooltip("The enemy prefab to spawn.")]
        public GameObject EnemyPrefab;

        public override IEnumerable<WeightedRandomEntry> GetResult()
        {
            yield return this;
        }
    }
}
