using System.Collections.Generic;
using UnityEngine;

namespace SimpleRPG.LevelGeneration
{
    /// <summary>
    /// Stops rooms that share an edge from drawing the same piece twice. The wall pieces are flat planes standing on the grid line, so
    /// two rooms side by side each put one in exactly the same place, and the two z-fight. Of each such pair, the one in the lower-numbered
    /// room keeps drawing and the other's renderer is switched off (its collider stays).
    /// </summary>
    public static class CoincidentGeometry
    {
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
