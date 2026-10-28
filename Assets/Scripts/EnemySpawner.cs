using SimpleRPG.InventorySystem.LootGeneration;
using System.Linq;
using UnityEngine;
using UnityEngine.Serialization;

namespace SimpleRPG
{
    [RequireComponent(typeof(NetworkSpawn))]
    public class EnemySpawner : MonoBehaviour
    {
        /// <summary>
        /// The encounter table containing the enemies this spawner can spawn.
        /// </summary>
        [Tooltip("The encounter table containing the enemies this spawner can spawn.")]
        [SerializeField]
        [FormerlySerializedAs("lootTable")]
        WeightedRandomTable encounterTable;

        NetworkSpawn networkSpawn;

        // Start is called once before the first execution of Update after the MonoBehaviour is created
        void Start()
        {
            networkSpawn = this.GetComponent<NetworkSpawn>();

            // Get a random enemy from the encounter table
            var result = encounterTable.GetResult(GameplayRandomSource.Current).OfType<EncounterEntry>().FirstOrDefault();
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
