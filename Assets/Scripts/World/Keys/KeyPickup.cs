using SimpleRPG.ObjectInteractions;
using Unity.Netcode;
using UnityEngine;

namespace SimpleRPG.Keys
{
    /// <summary>
    /// A dungeon key lying in a room. The server spawns it (see <c>LevelGeneratorController</c>); when a player picks it up
    /// the server adds the key to the party's <see cref="DungeonKeyRing"/> and despawns the pickup.
    /// </summary>
    public class KeyPickup : Interactable
    {
        private readonly NetworkVariable<int> keyId = new NetworkVariable<int>();

        /// <summary>
        /// Which lock this key opens.
        /// </summary>
        public int KeyId => keyId.Value;

        /// <summary>
        /// Sets which lock the key opens. Server only, before the pickup is spawned, so clients receive it with the spawn.
        /// </summary>
        public void Initialize(int id)
        {
            keyId.Value = id;
        }

        private DungeonKeyRing ring;

        /// <inheritdoc/>
        public override void OnNetworkSpawn()
        {
            if (IsServer)
            {
                ring = FindRing();
            }
        }

        /// <inheritdoc/>
        public override void ActivateInteraction(Interactor interactor)
        {
            RequestPickUp();
            interactor.StopInteraction();
        }

        /// <summary>
        /// Asks the server to give the calling player's party this key. Safe to call from any machine.
        /// </summary>
        public void RequestPickUp()
        {
            if (IsSpawned)
            {
                PickUpRpc();
            }
        }

        /// <inheritdoc/>
        public override void FinishInteraction(Interactor interactor)
        {
        }

        /// <summary>
        /// Gives the key to the party and removes the pickup, if the player is close enough. Server only.
        /// </summary>
        /// <returns> True if the key was picked up.</returns>
        public bool TryPickUp(float distance)
        {
            if (!IsServer || !IsSpawned || !KeyRules.InRange(distance, InteractionRange))
            {
                return false;
            }

            // The ring normally spawns with the level generator, before any key; look again only if it was not there yet.
            if (ring == null)
            {
                ring = FindRing();
            }

            if (ring == null || !ring.AddKey(KeyId))
            {
                return false;
            }

            NetworkObject.Despawn(true);
            return true;
        }

        // Looks only among the objects this pickup's own NetworkManager spawned, so it never picks up another instance's ring.
        private DungeonKeyRing FindRing()
        {
            foreach (NetworkObject spawned in NetworkManager.SpawnManager.SpawnedObjects.Values)
            {
                if (spawned.TryGetComponent(out DungeonKeyRing found))
                {
                    return found;
                }
            }

            return null;
        }

        [Rpc(SendTo.Server)]
        private void PickUpRpc(RpcParams rpcParams = default)
        {
            if (DungeonKeyRing.TryGetPlayerPosition(rpcParams.Receive.SenderClientId, out Vector3 player))
            {
                TryPickUp(Vector3.Distance(player, transform.position));
            }
        }
    }
}
