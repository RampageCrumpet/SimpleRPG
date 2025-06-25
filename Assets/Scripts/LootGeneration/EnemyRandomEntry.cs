using UnityEngine;
using SimpleRPG.InventorySystem.LootGeneration;
using System.Collections.Generic;

[CreateAssetMenu(fileName = "EnemyRandomEntry", menuName = "Scriptable Objects/EnemyRandomEntry")]
public class EnemyRandomEntry : WeightedRandomEntry
{
    [Tooltip("The enemy prefab to spawn.")]
    public GameObject EnemyPrefab;

    public override IEnumerable<WeightedRandomEntry> GetResult()
    {
        yield return this;
    }
}
