using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace SimpleRPG.RoomGeneration
{
    /// <summary>
    /// The shared geometry construction: turns resolved structural pieces into one mesh with a sub-mesh per kind of piece, so a room is one
    /// renderer and one collider however many walls, pillars and steps it has. No generator builds meshes of its own.
    /// </summary>
    /// <remarks>
    /// Plan axes (X, Y, Z up, millimetres) become Unity axes (x, y up, z) in metres: x = X, y = Z, z = Y.
    /// Faces are emitted only where they can be seen: a face is dropped when another piece covers it from the same plane
    /// (touching faces back to back, a face buried inside another piece, or an identical face of a piece drawn earlier or larger).
    /// Triangles wind clockwise seen from outside, which is Unity's front face, and each face has its own flat normal.
    /// </remarks>
    public static class RoomMeshBuilder
    {
        // Plan axis index -> Unity axis index.
        private static readonly int[] UnityAxis = { 0, 2, 1 };

        public sealed class Result
        {
            public Mesh Mesh;

            /// <summary>
            /// The kind of piece each sub-mesh holds, in sub-mesh order.
            /// </summary>
            public List<PieceKind> SubmeshKinds = new List<PieceKind>();

            public int FaceCount;
            public int HiddenFaceCount;
        }

        /// <param name="pieces"> The resolved pieces of a plan (<see cref="GeneratedRoom.ResolvePieces"/>).</param>
        /// <param name="metresPerUvTile"> How far a texture repeats.</param>
        public static Result Build(IReadOnlyList<StructuralPiece> pieces, float metresPerUvTile = 1f)
        {
            int count = pieces.Count;
            var min = new int[count][];
            var max = new int[count][];
            for (int i = 0; i < count; i++)
            {
                BoxMm box = pieces[i].Box;
                min[i] = new[] { box.Area.MinX, box.Area.MinY, box.MinZ };
                max[i] = new[] { box.Area.MaxX, box.Area.MaxY, box.MaxZ };
            }

            var vertices = new List<Vector3>();
            var normals = new List<Vector3>();
            var uvs = new List<Vector2>();
            var trianglesByKind = new List<int>[System.Enum.GetValues(typeof(PieceKind)).Length];
            var result = new Result();
            float uvScale = 0.001f / Mathf.Max(0.0001f, metresPerUvTile);

            for (int i = 0; i < count; i++)
            {
                if (pieces[i].Box.IsEmpty)
                {
                    continue;
                }

                int kind = (int)pieces[i].Kind;
                if (trianglesByKind[kind] == null)
                {
                    trianglesByKind[kind] = new List<int>();
                }

                for (int axis = 0; axis < 3; axis++)
                {
                    for (int side = 0; side < 2; side++)
                    {
                        if (IsHidden(i, axis, side, min, max))
                        {
                            result.HiddenFaceCount++;
                            continue;
                        }

                        AddFace(min[i], max[i], axis, side, uvScale, vertices, normals, uvs, trianglesByKind[kind]);
                        result.FaceCount++;
                    }
                }
            }

            var mesh = new Mesh { name = "Procedural room" };
            if (vertices.Count > 65535)
            {
                mesh.indexFormat = IndexFormat.UInt32;
            }

            mesh.SetVertices(vertices);
            mesh.SetNormals(normals);
            mesh.SetUVs(0, uvs);

            int submeshes = 0;
            for (int kind = 0; kind < trianglesByKind.Length; kind++)
            {
                if (trianglesByKind[kind] != null && trianglesByKind[kind].Count > 0)
                {
                    submeshes++;
                }
            }

            mesh.subMeshCount = submeshes;
            int index = 0;
            for (int kind = 0; kind < trianglesByKind.Length; kind++)
            {
                if (trianglesByKind[kind] == null || trianglesByKind[kind].Count == 0)
                {
                    continue;
                }

                mesh.SetTriangles(trianglesByKind[kind], index++);
                result.SubmeshKinds.Add((PieceKind)kind);
            }

            mesh.RecalculateBounds();
            result.Mesh = mesh;
            return result;
        }

        private static bool IsHidden(int self, int axis, int side, int[][] min, int[][] max)
        {
            int b = axis == 0 ? 1 : 0;
            int c = axis == 2 ? 1 : 2;
            int plane = side == 1 ? max[self][axis] : min[self][axis];
            long ownArea = (long)(max[self][b] - min[self][b]) * (max[self][c] - min[self][c]);

            for (int other = 0; other < min.Length; other++)
            {
                if (other == self)
                {
                    continue;
                }

                int[] oMin = min[other];
                int[] oMax = max[other];
                if (oMax[0] <= oMin[0] || oMax[1] <= oMin[1] || oMax[2] <= oMin[2])
                {
                    continue;
                }

                bool covers = oMin[b] <= min[self][b] && oMax[b] >= max[self][b] && oMin[c] <= min[self][c] && oMax[c] >= max[self][c];
                if (!covers)
                {
                    continue;
                }

                // Buried: the plane runs through the other piece's interior.
                if (oMin[axis] < plane && plane < oMax[axis])
                {
                    return true;
                }

                // Back to back: the other piece starts where this face ends.
                if (side == 1 ? oMin[axis] == plane : oMax[axis] == plane)
                {
                    return true;
                }

                // Same plane, same direction: keep only the larger face, or the earlier piece when they are equal.
                if (side == 1 ? oMax[axis] == plane : oMin[axis] == plane)
                {
                    long otherArea = (long)(oMax[b] - oMin[b]) * (oMax[c] - oMin[c]);
                    if (otherArea > ownArea || (otherArea == ownArea && other < self))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private static void AddFace(int[] min, int[] max, int axis, int side, float uvScale, List<Vector3> vertices, List<Vector3> normals, List<Vector2> uvs, List<int> triangles)
        {
            int b = axis == 0 ? 1 : 0;
            int c = axis == 2 ? 1 : 2;
            int plane = side == 1 ? max[axis] : min[axis];

            var normal = Vector3.zero;
            normal[UnityAxis[axis]] = side == 1 ? 1f : -1f;

            var corners = new Vector3[4];
            var corner = new int[3];
            int[] bs = { min[b], max[b], max[b], min[b] };
            int[] cs = { min[c], min[c], max[c], max[c] };
            for (int i = 0; i < 4; i++)
            {
                corner[axis] = plane;
                corner[b] = bs[i];
                corner[c] = cs[i];
                corners[i] = new Vector3(corner[0] * 0.001f, corner[2] * 0.001f, corner[1] * 0.001f);
            }

            // Front faces wind clockwise as seen from the front: (b - a) x (c - a) points towards the viewer.
            bool flip = Vector3.Dot(Vector3.Cross(corners[1] - corners[0], corners[2] - corners[0]), normal) < 0f;

            int first = vertices.Count;
            for (int i = 0; i < 4; i++)
            {
                vertices.Add(corners[i]);
                normals.Add(normal);
                uvs.Add(new Vector2(bs[i] * uvScale, cs[i] * uvScale));
            }

            if (flip)
            {
                triangles.AddRange(new[] { first, first + 2, first + 1, first, first + 3, first + 2 });
            }
            else
            {
                triangles.AddRange(new[] { first, first + 1, first + 2, first, first + 2, first + 3 });
            }
        }
    }
}
