using System.Collections.Generic;
using UnityEngine;

namespace SimpleRPG.AppearanceTools
{
    /// <summary>
    /// Whether a skinned mesh was bound to the same rest pose as the rig it is worn on. A mesh's bind poses are the world matrices of its bones
    /// at the pose it was skinned in, so comparing them with the rig's bones at rest says whether the two skeletons agree, with no second copy of
    /// the canonical rig to keep in step: the armor art is the reference. A body in a different pose from the armor (A-pose armor on a T-pose
    /// body) shows up as arms displaced by tens of centimetres.
    /// </summary>
    public static class RigConformance
    {
        /// <summary>The default allowance, in metres: modelling noise passes, a different arm pose does not.</summary>
        public const float DefaultTolerance = 0.02f;

        public readonly struct Deviation
        {
            public Deviation(string bone, float metres)
            {
                Bone = bone;
                Metres = metres;
            }

            public string Bone { get; }

            public float Metres { get; }

            public override string ToString() => Bone + " is " + (Metres * 100f).ToString("0.0") + " cm from where the mesh was bound";
        }

        /// <summary>
        /// Where a mesh's own space sits in the rig root's space. An FBX keeps the axis conversion of its mesh node (Blender's Z-up becomes Y-up
        /// there), so a mesh's bind poses are not in the rig's space and <see cref="Compare"/> needs this. It is read from a mesh already known to
        /// fit the rig (the body's own part), through its first bone the rig has: the bone's rest pose times the bind pose is the mesh-to-rig matrix.
        /// </summary>
        public static Matrix4x4 MeshToRig(Transform rigRoot, Mesh referenceMesh, IReadOnlyList<string> referenceBoneNames)
        {
            var bones = new Dictionary<string, Transform>();
            foreach (Transform t in rigRoot.GetComponentsInChildren<Transform>(true))
            {
                bones.TryAdd(t.name, t);
            }

            for (int i = 0; i < referenceBoneNames.Count && i < referenceMesh.bindposes.Length; i++)
            {
                if (bones.TryGetValue(referenceBoneNames[i], out Transform bone))
                {
                    return rigRoot.worldToLocalMatrix * bone.localToWorldMatrix * referenceMesh.bindposes[i];
                }
            }

            return Matrix4x4.identity;
        }

        /// <summary>
        /// The bones, out of those a mesh is bound to, that sit further than <paramref name="tolerance"/> from their bind position, worst first.
        /// <paramref name="rigRoot"/> is the transform the mesh's own space corresponds to (the root of the rig prefab), unless
        /// <paramref name="meshToRig"/> says where the mesh's space is in it (see <see cref="MeshToRig"/>). A bone the rig lacks is a deviation of
        /// infinity.
        /// </summary>
        public static List<Deviation> Compare(Transform rigRoot, Mesh mesh, IReadOnlyList<string> boneNames, float tolerance = DefaultTolerance, Matrix4x4? meshToRig = null)
        {
            Matrix4x4 toRig = meshToRig ?? Matrix4x4.identity;
            var bones = new Dictionary<string, Transform>();
            foreach (Transform t in rigRoot.GetComponentsInChildren<Transform>(true))
            {
                bones.TryAdd(t.name, t);
            }

            Matrix4x4[] bindPoses = mesh.bindposes;
            var result = new List<Deviation>();
            for (int i = 0; i < boneNames.Count && i < bindPoses.Length; i++)
            {
                if (!bones.TryGetValue(boneNames[i], out Transform bone))
                {
                    result.Add(new Deviation(boneNames[i], float.PositiveInfinity));
                    continue;
                }

                Vector3 boundAt = toRig.MultiplyPoint3x4(bindPoses[i].inverse.GetPosition());
                Vector3 restAt = rigRoot.InverseTransformPoint(bone.position);
                float distance = Vector3.Distance(boundAt, restAt);
                if (distance > tolerance) result.Add(new Deviation(boneNames[i], distance));
            }

            result.Sort((a, b) => b.Metres.CompareTo(a.Metres));
            return result;
        }
    }
}
