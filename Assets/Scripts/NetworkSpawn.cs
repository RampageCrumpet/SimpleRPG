using Unity.Netcode;
using UnityEngine;

namespace SimpleRPG
{
    /// <summary>
    /// Spawns a given object across the network.
    /// </summary>
    public class NetworkSpawn : MonoBehaviour
    {
        /// <summary>
        /// Spawns a GameObject across the network if the player is the host or server.
        /// </summary>
        /// <param name="objectPrefab"> The prefab we want to spawn across the network.</param>
        public void SpawnGameObject(GameObject objectPrefab)
        {
            // Ensure NetworkManager is available
            if (NetworkManager.Singleton == null)
            {
                Debug.LogWarning("NetworkManager not found. Destroying NetworkSpawn object.");
                Destroy(this.gameObject);
                return;
            }

            // Check if this instance is the host or server
            if (NetworkManager.Singleton.IsServer)
            {
                GameObject gameObject = Object.Instantiate(objectPrefab, transform.position, transform.rotation);
                var networkObject = gameObject.GetComponent<NetworkObject>();

                if(networkObject is null)
                {
                    Debug.LogError("NetworkObject component is missing on the instantiated object.");
                    Destroy(gameObject);
                    return;
                }

                if (!networkObject.IsSpawned)
                {
                    networkObject.Spawn();
                }
            }
        }
    }
}
