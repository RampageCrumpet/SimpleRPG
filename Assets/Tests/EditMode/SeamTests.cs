using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using SimpleRPG.LevelGeneration;
using UnityEditor;
using UnityEngine;

namespace SimpleRPG.Tests.EditMode
{
    /// <summary>
    /// Spawns dungeons from the real roomset and looks for the faults a player would see where rooms meet: pieces drawn on top of each
    /// other, doors that do not line up, and sealed doors with a room standing in their frame. Catches bad room art as well as generator bugs,
    /// so run it again whenever a room changes.
    /// </summary>
    public class SeamTests
    {
        private const string ModularDungeonPath = "Assets/Prefabs/World/TileSets/ModularDungeon/ModularDungeon.asset";
        private static readonly int[] RoomCounts = { 12, 40 };
        private const int Seeds = 25;

        /// <summary>Planes closer together than this, along their normal, are drawn in the same place and fight.</summary>
        private const float Coplanar = 0.005f;

        [Test]
        public void SpawnedDungeons_HaveNoSeams([ValueSource(nameof(RoomCounts))] int roomCount)
        {
            Roomset roomset = AssetDatabase.LoadAssetAtPath<Roomset>(ModularDungeonPath);
            Assert.IsNotNull(roomset, $"Could not load {ModularDungeonPath}.");
            List<RoomDefinition> definitions = RoomDefinitionBaker.Bake(roomset);
            var failures = new List<string>();

            for (int seed = 0; seed < Seeds; seed++)
            {
                GenerationResult result = new LayoutGenerator(seed, definitions).Generate(roomCount);
                if (!result.Success)
                {
                    failures.Add($"Seed {seed}: generation failed: {result}");
                    continue;
                }

                var parent = new GameObject("SeamTestDungeon");
                try
                {
                    var spawner = new LevelSpawner(roomset.RoomCollection, roomset.cellSize, roomset.floorHeight);
                    IReadOnlyList<RoomBlueprint> instances = spawner.Spawn(result.Layout, parent.transform);

                    failures.AddRange(FindOverlappingPieces(instances).Select(problem => $"Seed {seed}: {problem}"));
                    failures.AddRange(FindMisalignedDoors(result.Layout, instances, roomset.cellSize).Select(problem => $"Seed {seed}: {problem}"));
                    failures.AddRange(FindSealedDoorsFacingRooms(result.Layout).Select(problem => $"Seed {seed}: {problem}"));
                }
                finally
                {
                    Object.DestroyImmediate(parent);
                }
            }

            Assert.That(failures, Is.Empty, string.Join("\n", failures.Take(20)) + (failures.Count > 20 ? $"\n... and {failures.Count - 20} more" : ""));
        }

        /// <summary>
        /// Pieces of different rooms that are still drawn on top of each other once the spawner has finished: flat planes standing in the
        /// same place, and solid pieces with the same bounds.
        /// </summary>
        private static IEnumerable<string> FindOverlappingPieces(IReadOnlyList<RoomBlueprint> instances)
        {
            var planes = new Dictionary<(int axis, int step), List<(int room, Renderer renderer, Bounds bounds)>>();
            var solids = new Dictionary<Vector3Int, List<(int room, Renderer renderer, Bounds bounds)>>();

            for (int room = 0; room < instances.Count; room++)
            {
                foreach (MeshRenderer renderer in instances[room].GetComponentsInChildren<MeshRenderer>())
                {
                    if (!renderer.enabled || !renderer.gameObject.activeInHierarchy || CoincidentGeometry.SingleSided(renderer))
                    {
                        continue;
                    }

                    Bounds bounds = renderer.bounds;
                    int axis = CoincidentGeometry.FlatAxis(bounds);

                    if (axis >= 0)
                    {
                        Add(planes, (axis, Mathf.RoundToInt(bounds.center[axis] * 10f)), room, renderer, bounds);
                    }
                    else
                    {
                        Add(solids, new Vector3Int(Mathf.FloorToInt(bounds.center.x), Mathf.FloorToInt(bounds.center.y), Mathf.FloorToInt(bounds.center.z)), room, renderer, bounds);
                    }
                }
            }

            foreach (KeyValuePair<(int axis, int step), List<(int room, Renderer renderer, Bounds bounds)>> entry in planes)
            {
                int axis = entry.Key.axis;
                var candidates = new List<(int room, Renderer renderer, Bounds bounds)>(entry.Value);
                if (planes.TryGetValue((axis, entry.Key.step + 1), out List<(int room, Renderer renderer, Bounds bounds)> next))
                {
                    candidates.AddRange(next);
                }

                foreach ((int room, Renderer renderer, Bounds bounds) in entry.Value)
                {
                    foreach ((int otherRoom, Renderer other, Bounds otherBounds) in candidates)
                    {
                        if (otherRoom <= room || Mathf.Abs(otherBounds.center[axis] - bounds.center[axis]) > Coplanar)
                        {
                            continue;
                        }

                        bool overlaps = true;
                        for (int spread = 0; spread < 3 && overlaps; spread++)
                        {
                            overlaps = spread == axis || Mathf.Min(bounds.max[spread], otherBounds.max[spread]) - Mathf.Max(bounds.min[spread], otherBounds.min[spread]) > 0.01f;
                        }

                        if (overlaps)
                        {
                            yield return $"flat pieces '{renderer.name}' (room {room}) and '{other.name}' (room {otherRoom}) are drawn in the same place at {bounds.center}";
                        }
                    }
                }
            }

            foreach (KeyValuePair<Vector3Int, List<(int room, Renderer renderer, Bounds bounds)>> entry in solids)
            {
                for (int x = -1; x <= 1; x++)
                {
                    for (int y = -1; y <= 1; y++)
                    {
                        for (int z = -1; z <= 1; z++)
                        {
                            if (!solids.TryGetValue(entry.Key + new Vector3Int(x, y, z), out List<(int room, Renderer renderer, Bounds bounds)> neighbours))
                            {
                                continue;
                            }

                            foreach ((int room, Renderer renderer, Bounds bounds) in entry.Value)
                            {
                                foreach ((int otherRoom, Renderer other, Bounds otherBounds) in neighbours)
                                {
                                    if (otherRoom > room
                                        && (otherBounds.center - bounds.center).sqrMagnitude <= CoincidentGeometry.Tolerance * CoincidentGeometry.Tolerance
                                        && (otherBounds.size - bounds.size).sqrMagnitude <= CoincidentGeometry.Tolerance * CoincidentGeometry.Tolerance)
                                    {
                                        yield return $"solid pieces '{renderer.name}' (room {room}) and '{other.name}' (room {otherRoom}) have the same bounds at {bounds.center}";
                                    }
                                }
                            }
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Joined doors have to meet exactly in the world and face each other, and exactly one of each pair draws its door.
        /// </summary>
        private static IEnumerable<string> FindMisalignedDoors(Layout layout, IReadOnlyList<RoomBlueprint> instances, float cellSize)
        {
            foreach ((PlacedDoor a, PlacedDoor b) in LockSolver.Joins(layout.Rooms, layout.SealedDoors))
            {
                ConnectionBlueprint first = instances[a.Room.Id].Connections.ElementAt(a.Index);
                ConnectionBlueprint second = instances[b.Room.Id].Connections.ElementAt(b.Index);

                Vector3 farEnd = first.transform.position + first.transform.forward * a.LengthCells * cellSize;
                float gap = Vector3.Distance(farEnd, second.transform.position);
                if (gap > 0.05f)
                {
                    yield return $"{a} and {b} are {gap:0.###} units out of line.";
                }

                if (Vector3.Dot(first.transform.forward, second.transform.forward) > -0.99f)
                {
                    yield return $"{a} and {b} do not face each other.";
                }

                if (first.DoorObject != null && second.DoorObject != null && (!first.DoorObject.activeSelf || second.DoorObject.activeSelf))
                {
                    yield return $"{a} and {b} should draw one door: the lower-numbered room's, with the other's switched off.";
                }
            }
        }

        /// <summary>
        /// A sealed door's frame reaches into the cells in front of it, so no room may stand there.
        /// </summary>
        private static IEnumerable<string> FindSealedDoorsFacingRooms(Layout layout)
        {
            var occupant = new Dictionary<Vector3Int, PlacedRoom>();
            foreach (PlacedRoom room in layout.Rooms)
            {
                foreach (Vector3Int cell in room.Cells3D())
                {
                    occupant[cell] = room;
                }
            }

            foreach (PlacedDoor door in layout.SealedDoors)
            {
                foreach ((int slice, Vector3Int cell) in door.PassageCells3D())
                {
                    if (occupant.TryGetValue(cell, out PlacedRoom other) && other != door.Room)
                    {
                        yield return $"sealed {door} has {other} standing in front of it.";
                        break;
                    }
                }
            }
        }

        private static void Add<TKey>(Dictionary<TKey, List<(int room, Renderer renderer, Bounds bounds)>> map, TKey key, int room, Renderer renderer, Bounds bounds)
        {
            if (!map.TryGetValue(key, out List<(int room, Renderer renderer, Bounds bounds)> list))
            {
                map[key] = list = new List<(int, Renderer, Bounds)>();
            }

            list.Add((room, renderer, bounds));
        }
    }
}
