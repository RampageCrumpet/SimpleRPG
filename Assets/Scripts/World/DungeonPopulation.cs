using SimpleRPG.InventorySystem;
using SimpleRPG.LevelGeneration;
using SimpleRPG.InventorySystem.LootGeneration;
using SimpleRPG.ObjectInteractions;
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
        [SerializeField, Min(0), Tooltip("How many players the dungeon is built for. 0 or 1 is a lone player. A bigger party meets more groups of enemies in the same rooms; the enemies themselves do not get stronger.")]
        private int expectedPartySize;
        [SerializeField, Min(0f), Tooltip("Each player beyond the first adds this much to how many groups of enemies wait where a lone player would meet one. 0.5 is one extra group for every two extra players.")]
        private float extraEncountersPerExtraPlayer = 0.5f;
        [SerializeField, Tooltip("How strong the loot on each floor can be, floor by floor: the level ordinary loot there starts at and the ceiling nothing can pass. These are set by hand and are not the floor's number. A floor past the end of the list uses the last entry; with none, all loot is the lowest level.")]
        private LootArea[] floorLoot = new LootArea[0];
        private readonly List<Vector3> occupied = new List<Vector3>();
        public Vector3 PlayerSpawnPosition { get; private set; }

        /// <summary>How many players the dungeon is built for; set before <see cref="Populate"/>.</summary>
        public int ExpectedPartySize { get => expectedPartySize; set => expectedPartySize = Mathf.Max(0, value); }

        /// <summary>Where the party gets out, picked far from the arrival once population has succeeded. See <see cref="HasExtraction"/>.</summary>
        public Vector3 ExtractionPosition { get; private set; }

        /// <summary>False if no reachable spot for extraction could be found, for example in a dungeon of one room.</summary>
        public bool HasExtraction { get; private set; }
        public int EnemyCount { get; private set; }
        public int TreasureCount { get; private set; }
        public bool IsReady { get; private set; }
        /// <summary>True once population has ended unsuccessfully; waiters must stop waiting on <see cref="IsReady"/>.</summary>
        public bool HasFailed { get; private set; }
        public string FailureReason { get; private set; }

        public void Populate(IReadOnlyList<RoomBlueprint> rooms, float cellSize, int seed, float floorHeight = 0f)
        {
            IsReady = false;
            HasFailed = false;
            FailureReason = null;
            if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsServer) return;
            if (!TryPopulate(rooms, cellSize, seed, out string error, floorHeight))
                Debug.LogError($"Dungeon population failed: {error}", this);
        }

        /// <summary>Populates the rooms. On failure, records <see cref="FailureReason"/>, sets <see cref="HasFailed"/> and returns false.</summary>
        public bool TryPopulate(IReadOnlyList<RoomBlueprint> rooms, float cellSize, int seed, out string error, float floorHeight = 0f)
        {
            IsReady = false;
            HasFailed = false;
            FailureReason = null;
            HasExtraction = false;
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
                    PopulateRooms(rooms, cellSize, random, floorHeight);

                    // Chosen after everything else so adding it does not change where enemies and treasure go for a given seed.
                    HasExtraction = TryFindExtraction(rooms, cellSize, random, out Vector3 extraction);
                    ExtractionPosition = extraction;
                    IsReady = true;
                    return true;
                }
            }
            HasFailed = true;
            FailureReason = error;
            return false;
        }

        private void PopulateRooms(IReadOnlyList<RoomBlueprint> rooms, float cellSize, System.Random random, float floorHeight)
        {

            for (int i = 0; i < rooms.Count; i++)
            {
                LootArea area = AreaOf(rooms[i], floorHeight);

                // Containers placed with the room (chests) fill themselves when first opened, with this area's loot level.
                foreach (LootContainer container in rooms[i].GetComponentsInChildren<LootContainer>(true))
                {
                    container.Area = area;
                }

                // Keep the arrival room safe. No restrictions on room types or dungeon size.
                if (i > 0 && encounters != null && random.NextDouble() < encounterChance)
                {
                    // A bigger party meets more groups in the same room; a party of one rolls exactly once, as it always did.
                    int groups = SimpleRPG.Delve.PartyScaling.Groups(expectedPartySize, extraEncountersPerExtraPlayer, random);
                    for (int group = 0; group < groups; group++)
                        foreach (LootDrop encounter in encounters.GetDrops(random))
                            if (encounter.Entry is EncounterEntry enemy && enemy.EnemyPrefab != null && TryFindPosition(rooms[i], cellSize, random, true, out Vector3 point))
                            {
                                GameObject spawned = Spawn(enemy.EnemyPrefab, point, Quaternion.Euler(0, (float)random.NextDouble() * 360, 0));

                                // What the enemy drops when it dies is as strong as this area allows, and as much better or worse as the encounter says.
                                if (spawned.TryGetComponent(out LootContainer corpse))
                                {
                                    corpse.Area = area;
                                    corpse.ItemLevelOffset += encounter.LevelOffset;
                                }

                                occupied.Add(point);
                                EnemyCount++;
                            }
                }
                if (treasure == null) continue;
                for (int roll = 0; roll < treasureRollsPerRoom; roll++)
                    foreach (LootDrop drop in treasure.GetDrops(random))
                        if (drop.Entry is LootableItem entry && entry.Item != null && entry.Item.WorldPrefab != null && TryFindPosition(rooms[i], cellSize, random, true, out Vector3 point))
                        {
                            Vector3 ground = Physics.Raycast(point + Vector3.up * 0.5f, Vector3.down, out RaycastHit hit, 2f, geometryLayers, QueryTriggerInteraction.Ignore)
                                ? hit.point : point;
                            GameObject placed = Spawn(entry.Item.WorldPrefab, ground + Vector3.up * 0.01f, Quaternion.Euler(0, (float)random.NextDouble() * 360, 0));
                            if (placed.TryGetComponent(out WorldItemPickup pickup)) pickup.ItemLevel = area.LevelFor(drop.LevelOffset);
                            occupied.Add(point);
                            TreasureCount++;
                        }
            }
        }

        /// <summary>
        /// Picks the extraction point: the furthest from the arrival of one reachable spot in each of the last few rooms, so getting out takes
        /// crossing the dungeon.
        /// </summary>
        private bool TryFindExtraction(IReadOnlyList<RoomBlueprint> rooms, float cellSize, System.Random random, out Vector3 best)
        {
            const int RoomsToTry = 8;
            best = default;
            bool found = false;
            float bestDistance = -1f;
            int tried = 0;
            for (int i = rooms.Count - 1; i > 0 && tried < RoomsToTry; i--, tried++)
            {
                if (!TryFindPosition(rooms[i], cellSize, random, true, out Vector3 candidate))
                {
                    continue;
                }

                float distance = (candidate - PlayerSpawnPosition).sqrMagnitude;
                if (distance > bestDistance)
                {
                    bestDistance = distance;
                    best = candidate;
                    found = true;
                }
            }

            return found;
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

        // The loot area of a room: that of the floor it is on, which is found from its height.
        private LootArea AreaOf(RoomBlueprint room, float floorHeight)
        {
            if (floorLoot == null || floorLoot.Length == 0)
            {
                return LootArea.Lowest;
            }

            int floor = floorHeight > 0f ? Mathf.Max(0, Mathf.RoundToInt(room.transform.position.y / floorHeight)) : 0;
            return floorLoot[Mathf.Min(floor, floorLoot.Length - 1)];
        }

        private static GameObject Spawn(GameObject prefab, Vector3 position, Quaternion rotation)
        {
            var instance = Instantiate(prefab, position, rotation);
            instance.GetComponent<NetworkObject>().Spawn(true);
            return instance;
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
