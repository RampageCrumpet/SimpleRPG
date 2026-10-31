using System.Collections.Generic;
using UnityEngine;

namespace SimpleRPG.LevelGeneration
{
    /// <summary>
    /// Reads the drawn size of a room's meshes. Works on prefab assets as well as instances, because it uses mesh bounds and transforms
    /// rather than renderer bounds, which are not reliable on an asset that is not in a scene.
    /// </summary>
    public static class RoomGeometry
    {
        /// <summary>
        /// The world bounds of each mesh under a root, with the name of the object that carries it. Inactive objects are skipped.
        /// </summary>
        public static List<(string name, Bounds bounds)> MeshBounds(Transform root)
        {
            var result = new List<(string, Bounds)>();

            foreach (MeshFilter filter in root.GetComponentsInChildren<MeshFilter>())
            {
                if (filter.sharedMesh == null)
                {
                    continue;
                }

                Bounds local = filter.sharedMesh.bounds;
                Matrix4x4 toWorld = filter.transform.localToWorldMatrix;
                Bounds bounds = default;

                for (int corner = 0; corner < 8; corner++)
                {
                    Vector3 point = local.center + Vector3.Scale(local.extents, new Vector3(
                        (corner & 1) == 0 ? -1 : 1,
                        (corner & 2) == 0 ? -1 : 1,
                        (corner & 4) == 0 ? -1 : 1));
                    Vector3 world = toWorld.MultiplyPoint3x4(point);

                    if (corner == 0)
                    {
                        bounds = new Bounds(world, Vector3.zero);
                    }
                    else
                    {
                        bounds.Encapsulate(world);
                    }
                }

                result.Add((filter.name, bounds));
            }

            return result;
        }

        /// <summary>
        /// The cells a bounding box reaches into, as inclusive ranges in the room's packed cell coordinates (from its lowest corner).
        /// A box that only brushes a cell edge, within the tolerance, does not count as being in that cell.
        /// </summary>
        /// <param name="bounds"> World bounds.</param>
        /// <param name="roomRoot"> The room root's world position.</param>
        /// <param name="originOffset"> The room's packed origin offset, in cells.</param>
        /// <param name="cellSize"> World size of one cell.</param>
        /// <param name="tolerance"> World distance to ignore at each edge.</param>
        public static (int firstX, int lastX, int firstY, int lastY) CellRange(Bounds bounds, Vector3 roomRoot, Vector2Int originOffset, float cellSize, float tolerance)
        {
            return (
                Mathf.FloorToInt((bounds.min.x - roomRoot.x + tolerance) / cellSize) - originOffset.x,
                Mathf.CeilToInt((bounds.max.x - roomRoot.x - tolerance) / cellSize) - 1 - originOffset.x,
                Mathf.FloorToInt((bounds.min.z - roomRoot.z + tolerance) / cellSize) - originOffset.y,
                Mathf.CeilToInt((bounds.max.z - roomRoot.z - tolerance) / cellSize) - 1 - originOffset.y);
        }
    }
}
