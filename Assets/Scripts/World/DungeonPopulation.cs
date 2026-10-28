using SimpleRPG.LevelGeneration;
using SimpleRPG.InventorySystem.LootGeneration;
using System.Collections.Generic;
using System.Linq;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.AI;

namespace SimpleRPG.LevelGeneration
{
    /// <summary>Populates generated rooms after navigation is ready, using existing nested weighted tables.</summary>
    public sealed class DungeonPopulation : MonoBehaviour
    {
        [SerializeField] private WeightedRandomTable encounters;
        [SerializeField] private WeightedRandomTable treasure;
        [SerializeField, Range(0, 1)] private float encounterChance = 0.65f;
        [SerializeField, Min(0)] private int treasureRollsPerRoom = 2;
        [SerializeField] private LayerMask geometryLayers;
        private readonly List<Vector3> occupied = new List<Vector3>();
        public Vector3 PlayerSpawnPosition { get; private set; }
        public int EnemyCount { get; private set; }
        public int TreasureCount { get; private set; }
        public bool IsReady { get; private set; }
        /// <summary>True once population has ended unsuccessfully; waiters must stop waiting on <see cref="IsReady"/>.</summary>
        public bool HasFailed { get; private set; }
        public string FailureReason { get; private set; }

        public void Populate(IReadOnlyList<RoomBlueprint> rooms, float cellSize, int seed)
        {
            IsReady = false;
            HasFailed = false;
            FailureReason = null;
            if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsServer) return;
            if (!TryPopulate(rooms, cellSize, seed, out string error))
                Debug.LogError($"Dungeon population failed: {error}", this);
        }

        /// <summary>Populates the rooms. On failure, records <see cref="FailureReason"/>, sets <see cref="HasFailed"/> and returns false.</summary>
        public bool TryPopulate(IReadOnlyList<RoomBlueprint> rooms, float cellSize, int seed, out string error)
        {
            IsReady = false;
            HasFailed = false;
            FailureReason = null;
            error = null;
            if (rooms == null || rooms.Count == 0)
                error = "The generated dungeon has no rooms to populate.";
            else
            {
                occupied.Clear();
                EnemyCount = TreasureCount = 0;
                var random = new System.Random(seed);
                if (!TryFindArrival(rooms, cellSize, random, out Vector3 start))
                    error = "The dungeon's first room has no safe player start connected to another room.";
                else
                {
                    occupied.Add(start);
                    PopulateRooms(rooms, cellSize, random);
                    IsReady = true;
                    return true;
                }
            }
            HasFailed = true;
            FailureReason = error;
            return false;
        }

        private void PopulateRooms(IReadOnlyList<RoomBlueprint> rooms, float cellSize, System.Random random)
        {

            for (int i = 0; i < rooms.Count; i++)
            {
                // Keep the arrival room safe. No restrictions on room types or dungeon size.
                if (i > 0 && encounters != null && random.NextDouble() < encounterChance)
                {
                    foreach (var enemy in encounters.GetResult(random).OfType<EnemyRandomEntry>())
                        if (enemy.EnemyPrefab != null && TryFindPosition(rooms[i], cellSize, random, true, out Vector3 point))
                        {
                            Spawn(enemy.EnemyPrefab, point, Quaternion.Euler(0, (float)random.NextDouble() * 360, 0));
                            occupied.Add(point);
                            EnemyCount++;
                        }
                }
                if (treasure == null) continue;
                for (int roll = 0; roll < treasureRollsPerRoom; roll++)
                    foreach (var entry in treasure.GetResult(random).OfType<LootableItem>())
                        if (entry.Item != null && entry.Item.WorldPrefab != null && TryFindPosition(rooms[i], cellSize, random, true, out Vector3 point))
                        {
                            Vector3 ground = Physics.Raycast(point + Vector3.up * 0.5f, Vector3.down, out RaycastHit hit, 2f, geometryLayers, QueryTriggerInteraction.Ignore)
                                ? hit.point : point;
                            Spawn(entry.Item.WorldPrefab, ground + Vector3.up * 0.01f, Quaternion.Euler(0, (float)random.NextDouble() * 360, 0));
                            occupied.Add(point);
                            TreasureCount++;
                        }
            }
        }

        private bool TryFindArrival(IReadOnlyList<RoomBlueprint> rooms, float cellSize, System.Random random, out Vector3 start)
        {
            for (int attempt = 0; attempt < 80; attempt++)
            {
                if (!TryFindPosition(rooms[0], cellSize, random, false, out start)) break;
                PlayerSpawnPosition = start;
                if (rooms.Count == 1) return true;
                // Stair edges can produce small, valid NavMesh islands. Prove that arrival reaches the dungeon.
                if (rooms.Skip(1).Any(room => TryFindPosition(room, cellSize, random, true, out _))) return true;
            }
            start = default;
            return false;
        }

        private static void Spawn(GameObject prefab, Vector3 position, Quaternion rotation)
        {
            var instance = Instantiate(prefab, position, rotation);
            instance.GetComponent<NetworkObject>().Spawn(true);
        }

        public bool TryFindPosition(RoomBlueprint room, float cellSize, System.Random random, bool requirePath, out Vector3 position, bool avoidOccupied = true)
        {
            for (int attempt = 0; attempt < 80; attempt++)
            {
                Vector3 local = new Vector3(
                    (room.OriginOffset.x + (float)random.NextDouble() * room.Size.x) * cellSize,
                    0.5f,
                    (room.OriginOffset.y + (float)random.NextDouble() * room.Size.y) * cellSize);
                Vector3 candidate = room.transform.TransformPoint(local);
                if (!NavMesh.SamplePosition(candidate, out NavMeshHit hit, 1.5f, NavMesh.AllAreas)) continue;
                // SamplePosition may select another floor through a ceiling.
                if (Mathf.Abs(hit.position.y - candidate.y) > 1.5f) continue;
                if (avoidOccupied && occupied.Any(p => (p - hit.position).sqrMagnitude < 2.25f)) continue;
                if (Physics.CheckCapsule(hit.position + Vector3.up * 0.4f, hit.position + Vector3.up * 1.65f, 0.3f, geometryLayers, QueryTriggerInteraction.Ignore)) continue;
                if (requirePath)
                {
                    var path = new NavMeshPath();
                    if (!NavMesh.CalculatePath(PlayerSpawnPosition, hit.position, NavMesh.AllAreas, path) || path.status != NavMeshPathStatus.PathComplete) continue;
                }
                position = hit.position;
                return true;
            }
            position = default;
            return false;
        }
    }
}
