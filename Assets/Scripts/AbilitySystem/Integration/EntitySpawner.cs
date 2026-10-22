using SimpleRPG.Gameplay;
using Unity.Netcode;
using UnityEngine;
using EntityId = SimpleRPG.Gameplay.EntityId;

namespace SimpleRPG.Abilities.Integration
{
    /// <summary>
    /// Spawns creatures for abilities the way the rest of the game spawns them: instantiate the prefab and spawn its network object. The creature is
    /// an ordinary one, with its own AI, health and lifetime, and it does not depend on whatever spawned it staying around. Server only.
    /// </summary>
    public sealed class EntitySpawner : ISpawnService
    {
        /// <inheritdoc/>
        public bool Spawn(in SpawnRequest request)
        {
            NetworkManager manager = NetworkManager.Singleton;
            if (manager == null || !manager.IsServer)
            {
                Debug.LogError("Entities can only be spawned on the server.");
                return false;
            }

            if (request.Prefab == null || !request.Prefab.TryGetComponent(out NetworkObject _))
            {
                Debug.LogError("The prefab '" + (request.Prefab != null ? request.Prefab.name : "(none)") + "' needs a NetworkObject to be spawned.", request.Prefab);
                return false;
            }

            Vector3 position = new Vector3(request.Position.X, request.Position.Y, request.Position.Z);
            GameObject instance = Object.Instantiate(request.Prefab, position, Quaternion.identity);
            NetworkObject spawned = instance.GetComponent<NetworkObject>();
            spawned.Spawn();

            GameplayEventDispatcher events = GameplayServices.Current.Events;
            events.Enqueue(new EntitySpawnedEvent(new EntityId(spawned.NetworkObjectId), request.Instigator, request.Source, request.Execution, request.Position));
            if (!events.IsDraining)
            {
                events.Drain();
            }

            return true;
        }
    }
}
