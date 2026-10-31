using System.Collections.Generic;
using UnityEngine;

namespace SimpleRPG.LevelGeneration
{
    /// <summary>
    /// Stops rooms that share an edge from z-fighting. The wall pieces are flat planes standing on the grid line, so two rooms side by side
    /// each put one in the same place. Two cases:
    /// <list type="bullet">
    /// <item>Solid pieces with identical bounds (trims, pillars): the one in the lower-numbered room keeps drawing and the other's renderer
    /// is switched off (its collider stays).</item>
    /// <item>Flat planes that overlap, wholly or in part (pieces are two cells wide, so rooms of different sizes meet them at odd offsets):
    /// each is moved a hair into its own room, so from either side the room's own wall is the nearer one.</item>
    /// </list>
    /// </summary>
    public static class CoincidentGeometry
    {
        /// <summary>How far, in world units, a flat plane is moved into its own room to separate it from one it overlaps.</summary>
        public const float Separation = 0.01f;

        /// <summary>A mesh thinner than this, along one axis, is a flat plane.</summary>
        public const float FlatThickness = 0.02f;

        /// <summary>How far, in world units, two bounding boxes may differ in position or size and still count as the same piece.</summary>
        public const float Tolerance = 0.05f;

        /// <summary>
        /// Switches off the renderers that duplicate one in an earlier room.
        /// </summary>
        /// <param name="rooms"> The spawned rooms, in layout order.</param>
        /// <returns> How many renderers were switched off.</returns>
        public static int HideDuplicates(IReadOnlyList<RoomBlueprint> rooms)
        {
            var kept = new Dictionary<Vector3Int, List<(int room, Bounds bounds)>>();
            int hidden = 0;

            for (int room = 0; room < rooms.Count; room++)
            {
                var drawnHere = new List<(Vector3Int key, Bounds bounds)>();

                foreach (MeshRenderer renderer in rooms[room].GetComponentsInChildren<MeshRenderer>())
                {
                    if (!renderer.enabled || !renderer.gameObject.activeInHierarchy || SingleSided(renderer))
                    {
                        continue;
                    }

                    Bounds bounds = renderer.bounds;
                    if (FlatAxis(bounds) >= 0)
                    {
                        continue;
                    }

                    Vector3Int key = Cell(bounds.center);

                    if (IsDuplicateOfEarlierRoom(kept, key, bounds, room))
                    {
                        renderer.enabled = false;
                        hidden++;
                    }
                    else
                    {
                        drawnHere.Add((key, bounds));
                    }
                }

                // Added only once the room is done, so two pieces of the same room never hide each other.
                foreach ((Vector3Int key, Bounds bounds) in drawnHere)
                {
                    if (!kept.TryGetValue(key, out List<(int room, Bounds bounds)> list))
                    {
                        kept[key] = list = new List<(int, Bounds)>();
                    }

                    list.Add((room, bounds));
                }
            }

            return hidden;
        }

        /// <summary>
        /// Moves each flat double-sided plane that overlaps a flat plane of another room a hair into its own room.
        /// </summary>
        /// <param name="rooms"> The spawned rooms, in layout order.</param>
        /// <returns> How many pieces were moved.</returns>
        public static int SeparateOverlappingPlanes(IReadOnlyList<RoomBlueprint> rooms)
        {
            // Flat planes by the axis they are flat in and where along it they stand, to the nearest 0.1.
            var planes = new Dictionary<(int axis, int position), List<(int room, Renderer renderer, Bounds bounds)>>();
            var centres = new Vector3[rooms.Count];

            for (int room = 0; room < rooms.Count; room++)
            {
                bool any = false;
                Bounds whole = default;

                foreach (MeshRenderer renderer in rooms[room].GetComponentsInChildren<MeshRenderer>())
                {
                    if (!renderer.enabled || !renderer.gameObject.activeInHierarchy)
                    {
                        continue;
                    }

                    Bounds bounds = renderer.bounds;
                    if (!any)
                    {
                        whole = bounds;
                        any = true;
                    }
                    else
                    {
                        whole.Encapsulate(bounds);
                    }

                    int axis = FlatAxis(bounds);
                    if (axis < 0 || SingleSided(renderer))
                    {
                        continue;
                    }

                    var key = (axis, Mathf.RoundToInt(bounds.center[axis] * 10f));
                    if (!planes.TryGetValue(key, out List<(int room, Renderer renderer, Bounds bounds)> list))
                    {
                        planes[key] = list = new List<(int, Renderer, Bounds)>();
                    }

                    list.Add((room, renderer, bounds));
                }

                centres[room] = any ? whole.center : rooms[room].transform.position;
            }

            var moved = new HashSet<Transform>();

            foreach (KeyValuePair<(int axis, int position), List<(int room, Renderer renderer, Bounds bounds)>> entry in planes)
            {
                int axis = entry.Key.axis;

                // Planes at 0.1 steps can straddle a step, so the next one along is looked at as well.
                var candidates = new List<(int room, Renderer renderer, Bounds bounds)>(entry.Value);
                if (planes.TryGetValue((axis, entry.Key.position + 1), out List<(int room, Renderer renderer, Bounds bounds)> next))
                {
                    candidates.AddRange(next);
                }

                foreach ((int room, Renderer renderer, Bounds bounds) in entry.Value)
                {
                    if (moved.Contains(renderer.transform) || !OverlapsAnotherRoom(room, bounds, axis, candidates))
                    {
                        continue;
                    }

                    // Into its own room: towards the room's centre along the axis the plane is flat in.
                    float side = Mathf.Sign(centres[room][axis] - bounds.center[axis]);
                    Vector3 shift = Vector3.zero;
                    shift[axis] = (side == 0f ? 1f : side) * Separation;
                    renderer.transform.position += shift;
                    moved.Add(renderer.transform);
                }
            }

            return moved.Count;
        }

        /// <summary>
        /// The axis a bounding box is flat in, or -1 if it is thick on every axis. A plane has one thin axis; a box thin on two is a line.
        /// </summary>
        private static int FlatAxis(Bounds bounds)
        {
            int flat = -1;
            for (int axis = 0; axis < 3; axis++)
            {
                if (bounds.size[axis] < FlatThickness)
                {
                    if (flat >= 0)
                    {
                        return -1;
                    }

                    flat = axis;
                }
            }

            return flat;
        }

        private static bool OverlapsAnotherRoom(int room, Bounds bounds, int axis, List<(int room, Renderer renderer, Bounds bounds)> candidates)
        {
            foreach ((int otherRoom, Renderer other, Bounds otherBounds) in candidates)
            {
                if (otherRoom == room || Mathf.Abs(otherBounds.center[axis] - bounds.center[axis]) > FlatThickness)
                {
                    continue;
                }

                // Overlap on the two axes the planes spread across, by more than a sliver.
                bool overlaps = true;
                for (int spread = 0; spread < 3 && overlaps; spread++)
                {
                    if (spread != axis)
                    {
                        overlaps = Mathf.Min(bounds.max[spread], otherBounds.max[spread]) - Mathf.Max(bounds.min[spread], otherBounds.min[spread]) > 0.01f;
                    }
                }

                if (overlaps)
                {
                    return true;
                }
            }

            return false;
        }

        private static Vector3Int Cell(Vector3 position)
        {
            return new Vector3Int(Mathf.FloorToInt(position.x), Mathf.FloorToInt(position.y), Mathf.FloorToInt(position.z));
        }

        private static bool IsDuplicateOfEarlierRoom(Dictionary<Vector3Int, List<(int room, Bounds bounds)>> kept, Vector3Int key, Bounds bounds, int room)
        {
            // A piece's centre can sit either side of a bucket edge, so the neighbouring buckets are looked at too.
            for (int x = -1; x <= 1; x++)
            {
                for (int y = -1; y <= 1; y++)
                {
                    for (int z = -1; z <= 1; z++)
                    {
                        if (!kept.TryGetValue(key + new Vector3Int(x, y, z), out List<(int room, Bounds bounds)> list))
                        {
                            continue;
                        }

                        foreach ((int otherRoom, Bounds other) in list)
                        {
                            if (otherRoom != room
                                && (other.center - bounds.center).sqrMagnitude <= Tolerance * Tolerance
                                && (other.size - bounds.size).sqrMagnitude <= Tolerance * Tolerance)
                            {
                                return true;
                            }
                        }
                    }
                }
            }

            return false;
        }

        /// <summary>
        /// True if every material draws only its front faces. Two such pieces facing opposite ways never show through each other, so they
        /// do not fight, and hiding one would leave a hole when looked at from the other room. A material with no cull setting is taken
        /// to be double sided, since that is what a flat wall piece needs to be seen from both rooms.
        /// </summary>
        private static bool SingleSided(Renderer renderer)
        {
            Material[] materials = renderer.sharedMaterials;
            if (materials.Length == 0)
            {
                return false;
            }

            foreach (Material material in materials)
            {
                if (material == null || !material.HasProperty("_Cull") || material.GetFloat("_Cull") == 0f)
                {
                    return false;
                }
            }

            return true;
        }
    }
}
