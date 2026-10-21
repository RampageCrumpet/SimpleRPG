using System;
using System.Collections.Generic;
using System.Linq;
using Unity.Netcode;
using UnityEngine;

namespace SimpleRPG.Keys
{
    /// <summary>
    /// The party's keys and which locked doors are already open. The server owns both lists; they are <see cref="NetworkList{T}"/>s,
    /// so a client that joins late receives the full state with the spawn and sees the same open and locked doors as everyone else.
    /// Every machine builds the same locked doors from the dungeon seed and registers them here with <see cref="RegisterLock"/>.
    /// Lives next to the <c>LevelGeneratorController</c>.
    /// </summary>
    public class DungeonKeyRing : NetworkBehaviour
    {
        /// <summary>
        /// How close a player must stand to open a locked door.
        /// </summary>
        public const float DoorRange = 3f;

        private readonly NetworkList<int> held = new NetworkList<int>();
        private readonly NetworkList<int> opened = new NetworkList<int>();
        private readonly Dictionary<int, List<GameObject>> locks = new Dictionary<int, List<GameObject>>();

        /// <summary>
        /// Raised on every machine when the held or opened keys change.
        /// </summary>
        public event Action Changed;

        /// <summary>
        /// The ids of the keys the party holds.
        /// </summary>
        public IEnumerable<int> HeldKeys => Snapshot(held);

        /// <summary>
        /// The ids of the locks that have been opened.
        /// </summary>
        public IEnumerable<int> OpenedKeys => Snapshot(opened);

        private static List<int> Snapshot(NetworkList<int> list)
        {
            var copy = new List<int>(list.Count);
            for (int i = 0; i < list.Count; i++)
            {
                copy.Add(list[i]);
            }

            return copy;
        }

        public bool Holds(int keyId) => held.Contains(keyId);

        public bool IsOpen(int keyId) => opened.Contains(keyId);

        public override void OnNetworkSpawn()
        {
            held.OnListChanged += HandleListChanged;
            opened.OnListChanged += HandleListChanged;

            // A late joiner gets the lists with the spawn but no change event.
            ApplyOpenedLocks();
        }

        public override void OnNetworkDespawn()
        {
            held.OnListChanged -= HandleListChanged;
            opened.OnListChanged -= HandleListChanged;
        }

        private void HandleListChanged(NetworkListEvent<int> changeEvent)
        {
            ApplyOpenedLocks();
            Changed?.Invoke();
        }

        /// <summary>
        /// Adds a lock's visible parts (gate, bars). They are switched off when the lock opens, including right away if it is already open.
        /// </summary>
        public void RegisterLock(int keyId, GameObject lockObject)
        {
            if (!locks.TryGetValue(keyId, out List<GameObject> objects))
            {
                objects = new List<GameObject>();
                locks[keyId] = objects;
            }

            objects.Add(lockObject);

            if (opened.Contains(keyId))
            {
                lockObject.SetActive(false);
            }
        }

        /// <summary>
        /// How far the closest registered part of a lock is from a point, or null if this machine has none.
        /// </summary>
        public float? DistanceToLock(int keyId, Vector3 from)
        {
            if (!locks.TryGetValue(keyId, out List<GameObject> objects))
            {
                return null;
            }

            return objects.Where(x => x != null).Select(x => (float?)Vector3.Distance(x.transform.position, from)).Min();
        }

        private void ApplyOpenedLocks()
        {
            foreach (int keyId in opened)
            {
                if (locks.TryGetValue(keyId, out List<GameObject> objects))
                {
                    foreach (GameObject lockObject in objects.Where(x => x != null))
                    {
                        lockObject.SetActive(false);
                    }
                }
            }
        }

        /// <summary>
        /// Gives the party a key. Server only.
        /// </summary>
        /// <returns> False if this is not the server or the party already holds the key.</returns>
        public bool AddKey(int keyId)
        {
            if (!IsServer || held.Contains(keyId) || opened.Contains(keyId))
            {
                return false;
            }

            held.Add(keyId);
            return true;
        }

        /// <summary>
        /// Opens a lock with the party's key, using the key up, if the rules allow it. Server only.
        /// </summary>
        /// <param name="keyId"> The lock to open.</param>
        /// <param name="distance"> How far the player is from the door.</param>
        public KeyUseResult TryOpen(int keyId, float distance)
        {
            if (!IsServer)
            {
                return KeyUseResult.NotHeld;
            }

            KeyUseResult result = KeyRules.CheckOpen(Snapshot(held), Snapshot(opened), keyId, distance, DoorRange);
            if (result == KeyUseResult.Opened)
            {
                held.Remove(keyId);
                opened.Add(keyId);
            }

            return result;
        }

        /// <summary>
        /// Asks the server to open a lock for the calling player. Safe to call from any machine.
        /// </summary>
        public void RequestOpen(int keyId)
        {
            RequestOpenRpc(keyId);
        }

        [Rpc(SendTo.Server)]
        private void RequestOpenRpc(int keyId, RpcParams rpcParams = default)
        {
            if (!TryGetPlayerPosition(rpcParams.Receive.SenderClientId, out Vector3 player))
            {
                return;
            }

            float? distance = DistanceToLock(keyId, player);
            if (distance.HasValue)
            {
                TryOpen(keyId, distance.Value);
            }
        }

        /// <summary>
        /// Finds where a connected client's character is, as the server sees it.
        /// </summary>
        public static bool TryGetPlayerPosition(ulong clientId, out Vector3 position)
        {
            position = default;
            NetworkManager manager = NetworkManager.Singleton;
            if (manager == null || !manager.ConnectedClients.TryGetValue(clientId, out NetworkClient client) || client.PlayerObject == null)
            {
                return false;
            }

            Player player = client.PlayerObject.GetComponent<Player>();
            if (player == null || player.character == null)
            {
                return false;
            }

            position = player.character.transform.position;
            return true;
        }
    }
}
