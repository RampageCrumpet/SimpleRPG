using System;
using UnityEngine;

namespace SimpleRPG.Appearance
{
    /// <summary>
    /// How one garment mesh is simulated with Unity's built-in Cloth: the component settings, the thigh colliders it collides with, and the
    /// baked per-particle Max Distance. Presentation only; the server never builds it. The coefficients are indexed by Cloth's particle order,
    /// which belongs to one exact mesh, so the profile records that mesh's fingerprint and <see cref="Problem"/> refuses any other.
    /// </summary>
    public sealed class ClothProfile : ScriptableObject
    {
        /// <summary>A tapered sphere pair on a thigh bone: radius <c>r0</c> at <c>center0</c>, <c>r1</c> at <c>center1</c>, the centres in the bone's own space (metres).</summary>
        [Serializable]
        public struct ThighCollider
        {
            public string bone;
            public Vector3 center0, center1;
            public float r0, r1;
        }

        [SerializeField, Tooltip("The mesh the baked data belongs to.")] private Mesh mesh;
        [SerializeField, Tooltip("Hash of the mesh's vertex positions and triangles when the data was baked.")] private string fingerprint;
        [SerializeField, Tooltip("Number of Cloth particles for that mesh; Cloth welds vertices that share a position.")] private int particleCount;
        [SerializeField] private float[] maxDistance = Array.Empty<float>();
        [SerializeField] private float[] collisionSphereDistance = Array.Empty<float>();
        [SerializeField] private ThighCollider[] colliders = Array.Empty<ThighCollider>();

        [Header("Cloth component")]
        [SerializeField] private float stretchingStiffness = 1f;
        [SerializeField] private float bendingStiffness = 0.3f;
        [SerializeField] private float damping = 0.6f;
        [SerializeField] private float friction = 0.5f;
        [SerializeField] private float worldVelocityScale = 0.3f;
        [SerializeField] private float worldAccelerationScale = 0.3f;
        [SerializeField] private float solverFrequency = 120f;
        [SerializeField] private float sleepThreshold;
        [SerializeField] private bool useTethers = true;
        [SerializeField] private bool useGravity = true;

        public Mesh Mesh => mesh;
        public string Fingerprint => fingerprint;
        public int ParticleCount => particleCount;
        public float StretchingStiffness => stretchingStiffness;
        public float BendingStiffness => bendingStiffness;
        public float Damping => damping;
        public float Friction => friction;
        public float WorldVelocityScale => worldVelocityScale;
        public float WorldAccelerationScale => worldAccelerationScale;
        public float SolverFrequency => solverFrequency;
        public float SleepThreshold => sleepThreshold;
        public bool UseTethers => useTethers;
        public bool UseGravity => useGravity;
        public ThighCollider[] Colliders => colliders ?? Array.Empty<ThighCollider>();

        /// <summary>The baked coefficients, one per Cloth particle, ready to assign to <see cref="Cloth.coefficients"/>.</summary>
        public ClothSkinningCoefficient[] BuildCoefficients()
        {
            var result = new ClothSkinningCoefficient[particleCount];
            for (int i = 0; i < result.Length; i++)
            {
                result[i].maxDistance = maxDistance[i];
                result[i].collisionSphereDistance = collisionSphereDistance[i];
            }

            return result;
        }

        /// <summary>
        /// Why this profile must not be applied to <paramref name="target"/>, or null if it may: another mesh, a mesh that changed since the bake
        /// (a reimport moves the particle order), or data that does not fit it. Never apply the coefficients when this returns a message.
        /// </summary>
        public string Problem(Mesh target)
        {
            if (target == null) return name + ": the piece has no mesh.";
            if (mesh == null) return name + " has no mesh. Rebake it (SimpleRPG > Items > Bake Hauberk Cloth Profile).";
            if (target != mesh) return name + " was baked for '" + mesh.name + "', not '" + target.name + "'.";
            if (maxDistance == null || collisionSphereDistance == null || maxDistance.Length != particleCount || collisionSphereDistance.Length != particleCount)
                return name + " has " + (maxDistance != null ? maxDistance.Length : 0) + " coefficients for " + particleCount + " particles. Rebake it.";
            string now = ComputeFingerprint(target);
            return now == fingerprint ? null : name + " was baked for a different version of '" + target.name + "' (fingerprint " + fingerprint + ", mesh is now " + now + "). The particle order may have moved; rebake it (SimpleRPG > Items > Bake Hauberk Cloth Profile).";
        }

        /// <summary>A hash of a mesh's vertex positions (to 0.1 mm) and triangle indices. Changes whenever a reimport could change Cloth's particle order.</summary>
        public static string ComputeFingerprint(Mesh target)
        {
            uint hash = 2166136261u;
            Vector3[] vertices = target.vertices;
            foreach (Vector3 v in vertices)
            {
                hash = Mix(hash, Mathf.RoundToInt(v.x * 10000f));
                hash = Mix(hash, Mathf.RoundToInt(v.y * 10000f));
                hash = Mix(hash, Mathf.RoundToInt(v.z * 10000f));
            }

            foreach (int index in target.triangles) hash = Mix(hash, index);
            return vertices.Length + "-" + hash.ToString("x8");
        }

        private static uint Mix(uint hash, int value)
        {
            unchecked
            {
                return (hash ^ (uint)value) * 16777619u;
            }
        }

#if UNITY_EDITOR
        /// <summary>Stores a bake. Editor tooling only; profiles are never written at runtime.</summary>
        public void SetBake(Mesh source, string meshFingerprint, float[] maxDistances, float[] collisionDistances, ThighCollider[] thighColliders)
        {
            mesh = source;
            fingerprint = meshFingerprint;
            particleCount = maxDistances.Length;
            maxDistance = maxDistances;
            collisionSphereDistance = collisionDistances;
            colliders = thighColliders;
        }
#endif
    }
}
