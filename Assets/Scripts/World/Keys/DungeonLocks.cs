using LevelGeneration;
using System.Collections.Generic;
using System.Linq;
using Unity.Netcode;
using UnityEngine;

namespace SimpleRPG.Keys
{
    /// <summary>
    /// Puts the gameplay on the locked doors and keys the level spawner placed.
    /// </summary>
    public static class DungeonLocks
    {
        /// <summary>
        /// Registers both ends of every lock with the key ring and makes their solid parts usable. Run on every machine,
        /// since every machine builds the same doors. A door whose room has no lock object gets a plain barrier, so it still blocks the way.
        /// </summary>
        /// <param name="ring"> Where the locks are registered.</param>
        /// <param name="rooms"> The spawned rooms, indexed by room id.</param>
        /// <param name="locks"> The locks the spawner applied.</param>
        public static void Attach(DungeonKeyRing ring, IReadOnlyList<RoomBlueprint> rooms, IEnumerable<LockedDoor> locks)
        {
            foreach (LockedDoor locked in locks)
            {
                foreach (PlacedDoor end in new[] { locked.Door, locked.Mate })
                {
                    ConnectionBlueprint connection = rooms[end.Room.Id].Connections.ElementAtOrDefault(end.Index);
                    if (connection == null)
                    {
                        continue;
                    }

                    GameObject lockObject = connection.LockObject != null ? connection.LockObject : CreateBarrier(connection);
                    ring.RegisterLock(locked.KeyId, lockObject);

                    // One interactable for the whole lock object; it answers for every collider beneath it.
                    if (lockObject.GetComponent<LockedDoorInteractable>() == null)
                    {
                        lockObject.AddComponent<LockedDoorInteractable>().Setup(ring, locked.KeyId);
                    }
                }
            }
        }

        /// <summary>
        /// Spawns the key's pickup in a room. Server only.
        /// </summary>
        /// <returns> The spawned pickup, or null if it could not be spawned.</returns>
        public static KeyPickup SpawnKey(GameObject pickupPrefab, int keyId, RoomBlueprint room)
        {
            if (pickupPrefab == null || pickupPrefab.GetComponent<KeyPickup>() == null)
            {
                Debug.LogError("The level generator has no key pickup prefab with a KeyPickup, so key " + keyId + " cannot be placed.");
                return null;
            }

            GameObject instance = Object.Instantiate(pickupPrefab, KeyPosition(room), Quaternion.identity);
            KeyPickup pickup = instance.GetComponent<KeyPickup>();
            pickup.Initialize(keyId);
            instance.GetComponent<NetworkObject>().Spawn();
            return pickup;
        }

        /// <summary>
        /// Where a key lies in a room: just above the floor near the middle of the room, in open space. The middle of the room's
        /// bounds is tried first, then a fixed ring of spots around it, so the same room always gives the same answer.
        /// </summary>
        public static Vector3 KeyPosition(RoomBlueprint room)
        {
            Renderer[] renderers = room.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0)
            {
                return room.transform.position + Vector3.up;
            }

            Bounds bounds = renderers[0].bounds;
            foreach (Renderer renderer in renderers)
            {
                bounds.Encapsulate(renderer.bounds);
            }

            // Colliders of a room that was just placed may not be in the physics world yet.
            Physics.SyncTransforms();

            // Collect the free spots, then take the first one that is at floor level: the top of a pillar or low wall is also a
            // free upward facing surface, but it is higher than the floor beside it.
            var spots = new List<Vector3>();
            foreach (Vector2 offset in CandidateOffsets)
            {
                if (Mathf.Abs(offset.x) > bounds.extents.x || Mathf.Abs(offset.y) > bounds.extents.z)
                {
                    continue;
                }

                if (TryFindFloorSpot(room, bounds.center.x + offset.x, bounds.center.z + offset.y, bounds, out Vector3 spot))
                {
                    spots.Add(spot);
                }
            }

            if (spots.Count > 0)
            {
                float floor = spots.Min(x => x.y);
                return spots.First(x => x.y <= floor + FloorTolerance);
            }

            return new Vector3(bounds.center.x, bounds.min.y + 1f, bounds.center.z);
        }

        private const float FloorTolerance = 0.25f;

        private const float KeyHeight = 0.5f;
        private const float KeyClearance = 0.35f;

        private static readonly Vector2[] CandidateOffsets = BuildCandidateOffsets();

        private static Vector2[] BuildCandidateOffsets()
        {
            var offsets = new List<Vector2> { Vector2.zero };
            foreach (float radius in new[] { 1.5f, 3f, 4.5f })
            {
                for (int i = 0; i < 8; i++)
                {
                    float angle = i * Mathf.PI / 4f;
                    offsets.Add(new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius);
                }
            }

            return offsets.ToArray();
        }

        /// <summary>
        /// Finds the lowest upward facing surface of the room beneath a point that has open space above it. Surfaces buried
        /// in foundation geometry fail the clearance check, and the ceiling is never reached because the floor comes first.
        /// </summary>
        private static bool TryFindFloorSpot(RoomBlueprint room, float x, float z, Bounds bounds, out Vector3 spot)
        {
            var origin = new Vector3(x, bounds.max.y + 1f, z);
            RaycastHit[] hits = Physics.RaycastAll(origin, Vector3.down, bounds.size.y + 2f, ~0, QueryTriggerInteraction.Ignore);
            System.Array.Sort(hits, (a, b) => a.point.y.CompareTo(b.point.y));

            foreach (RaycastHit hit in hits)
            {
                if (hit.normal.y < 0.7f || !hit.collider.transform.IsChildOf(room.transform))
                {
                    continue;
                }

                Vector3 candidate = hit.point + Vector3.up * KeyHeight;
                if (!Physics.CheckSphere(candidate, KeyClearance, ~0, QueryTriggerInteraction.Ignore))
                {
                    spot = candidate;
                    return true;
                }
            }

            spot = default;
            return false;
        }

        private static GameObject CreateBarrier(ConnectionBlueprint connection)
        {
            GameObject barrier = GameObject.CreatePrimitive(PrimitiveType.Cube);
            barrier.name = "LockedDoorBarrier";
            barrier.transform.SetParent(connection.transform, false);
            float width = connection.ConnectionWidth > 0f ? connection.ConnectionWidth : 2f;
            barrier.transform.localPosition = Vector3.up * 1.5f;
            barrier.transform.localScale = new Vector3(width, 3f, 0.2f);
            return barrier;
        }
    }
}
