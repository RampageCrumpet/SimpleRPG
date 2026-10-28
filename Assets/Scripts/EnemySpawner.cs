using SimpleRPG.InventorySystem.LootGeneration;
using System.Linq;
using UnityEngine;

namespace SimpleRPG
{
    [RequireComponent(typeof(NetworkSpawn))]
    public class EnemySpawner : MonoBehaviour
    {
        /// <summary>
        /// The LootTable containing enemies this spawner can spawn.
        /// </summary>
        [Tooltip("The LootTable containing enemies this spawner can spawn.")]
        [SerializeField]
        WeightedRandomTable lootTable;

        NetworkSpawn networkSpawn;

        // Start is called once before the first execution of Update after the MonoBehaviour is created
        void Start()
        {
            networkSpawn = this.GetComponent<NetworkSpawn>();

            // Get a random enemy from the loot table
            var result = lootTable.GetResult(GameplayRandomSource.Current).OfType<EnemyRandomEntry>().FirstOrDefault();
            if (result != null && result.EnemyPrefab != null)
            {
                networkSpawn.SpawnGameObject(result.EnemyPrefab);
            }
            else
            {
                Debug.LogWarning("No enemy prefab selected or prefab is missing.");
            }
        }
    }
}
