using Unity.Netcode;
using UnityEngine;

namespace SimpleRPG.LevelGeneration
{
    /// <summary>
    /// Applies the network authority rule to the contents of a freshly instantiated room.
    /// Every machine builds the room's geometry from the shared seed, but anything with a <see cref="NetworkObject"/>
    /// (chests, ...) exists exactly once: the server spawns it and Netcode creates the replica on clients.
    /// A client's locally instantiated copy would otherwise be a duplicate that never receives server state.
    /// </summary>
    public static class RoomNetworkObjects
    {
        /// <summary>
        /// Spawns the room's nested network objects on the server and removes them on clients.
        /// </summary>
        /// <param name="roomRoot">The instantiated room.</param>
        /// <param name="isServer">True if this machine is the server or host.</param>
        public static void Apply(GameObject roomRoot, bool isServer)
        {
            foreach (NetworkObject networkObject in roomRoot.GetComponentsInChildren<NetworkObject>(true))
            {
                if (networkObject.gameObject == roomRoot)
                {
                    continue;
                }

                if (isServer)
                {
                    if (!networkObject.IsSpawned)
                    {
                        networkObject.Spawn();
                    }
                }
                else
                {
                    // Deactivate first so Start() never runs on the discarded copy.
                    networkObject.gameObject.SetActive(false);
                    Object.Destroy(networkObject.gameObject);
                }
            }
        }
    }
}
