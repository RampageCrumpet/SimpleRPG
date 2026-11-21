using System;
using System.Collections.Generic;
using System.Linq;
using SimpleRPG.Appearance;
using UnityEditor;
using UnityEngine;

namespace SimpleRPG.AppearanceTools
{
    /// <summary>
    /// Bakes a <see cref="ClothProfile"/> for the hauberk: the per-particle Max Distance (zero down to the waist, growing to a free hem), the
    /// thigh colliders and the Cloth settings. Cloth keeps one particle, and one normal, per vertex position, so the mesh must have no coincident
    /// back-to-back faces and no custom normals (see Docs/Equipment.md); the export script's game mesh is built that way. Everything is computed
    /// here, at edit time, against the mesh as it is now; the profile records the mesh's fingerprint and Unity's particle count so a changed mesh is
    /// refused at runtime instead of mis-indexed.
    /// </summary>
    public static class ClothProfileBaker
    {
        public const string HauberkPartPath = "Assets/Art/BodyParts/Armor/MailHauberk_GAME_MailHauberk.asset";
        public const string HauberkProfilePath = "Assets/Art/BodyParts/Armor/MailHauberk_ClothProfile.asset";
        public const string HauberkMeshName = "GAME_MailHauberk";

        // Mesh-space numbers of the hauberk (metres, z up): a vertex follows the skin exactly at and above FixedAbove, and has its full Max Distance at and
        // below FreeBelow.
        private const float FixedAbove = 0.78f;
        private const float FreeBelow = 0.65f;
        private const float HemMaxDistance = 0.05f;

        // Cloth component settings, tuned by eye in the sway demo (Assets/ClothFitTest) with the colliders below.
        private const float Stretching = 0.84f, Bending = 0.10f, Damping = 0f, Friction = 0.5f, WorldVelocityScale = 0.3f, WorldAccelerationScale = 0.3f, SolverFrequency = 120f;

        // One tapered sphere pair per thigh, in the thigh bone's own space, fitted to the animated breeches (every 2nd frame of idle, walk, run, stop and lunge):
        // the smallest pair that holds every breeches vertex between z 0.60 and 0.96 with 3 mm to spare. The top sphere sits at the buttock and hip, the bottom at mid-thigh.
        private static readonly ClothProfile.ThighCollider[] Thighs =
        {
            new ClothProfile.ThighCollider { bone = "LeftUpperLeg", center0 = new Vector3(0.0378f, 0.0085f, -0.0175f), r0 = 0.1395f, center1 = new Vector3(-0.0120f, 0.2520f, -0.0133f), r1 = 0.1030f },
            new ClothProfile.ThighCollider { bone = "RightUpperLeg", center0 = new Vector3(-0.0378f, 0.0085f, -0.0175f), r0 = 0.1395f, center1 = new Vector3(0.0120f, 0.2520f, -0.0133f), r1 = 0.1030f },
        };

        [MenuItem("SimpleRPG/Items/Bake Hauberk Cloth Profile")]
        public static void BakeHauberk()
        {
            // The pieces come from the prefab; a bake needs them to exist, and the profile has to be linked to its piece afterwards.
            ArmorSetup.Build();
            var part = AssetDatabase.LoadAssetAtPath<BodyPartDefinition>(HauberkPartPath);
            if (part == null) { Debug.LogError("Bake Hauberk Cloth Profile: " + HauberkPartPath + " is missing. Run SimpleRPG > Items > Build Armor Items And Parts."); return; }

            BodyPartPiece piece = part.Pieces.FirstOrDefault(p => p.Mesh != null && p.Mesh.name == HauberkMeshName);
            if (piece == null) { Debug.LogError("Bake Hauberk Cloth Profile: " + part.name + " has no piece with the mesh '" + HauberkMeshName + "'. Export the hauberk with Authoring/export_armor.py."); return; }

            string error = Bake(piece.Mesh, out float[] maxDistance, out float[] collision, out int particles);
            if (error != null) { Debug.LogError("Bake Hauberk Cloth Profile: " + error); return; }

            var profile = AssetDatabase.LoadAssetAtPath<ClothProfile>(HauberkProfilePath);
            if (profile == null)
            {
                profile = ScriptableObject.CreateInstance<ClothProfile>();
                AssetDatabase.CreateAsset(profile, HauberkProfilePath);
            }

            profile.SetBake(piece.Mesh, ClothProfile.ComputeFingerprint(piece.Mesh), maxDistance, collision, Thighs);
            var so = new SerializedObject(profile);
            so.FindProperty("stretchingStiffness").floatValue = Stretching;
            so.FindProperty("bendingStiffness").floatValue = Bending;
            so.FindProperty("damping").floatValue = Damping;
            so.FindProperty("friction").floatValue = Friction;
            so.FindProperty("worldVelocityScale").floatValue = WorldVelocityScale;
            so.FindProperty("worldAccelerationScale").floatValue = WorldAccelerationScale;
            so.FindProperty("solverFrequency").floatValue = SolverFrequency;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(profile);
            AssetDatabase.SaveAssets();

            // Build Armor Items And Parts finds the profile again by mesh and puts it on the hauberk piece.
            ArmorSetup.Build();
            Debug.Log("Baked " + HauberkProfilePath + ": " + particles + " Cloth particles, " + maxDistance.Count(d => d > 0f) + " with Max Distance above 0, fingerprint " + profile.Fingerprint + ".");
        }

        /// <summary>The profile baked for this mesh, or null. Lets the armor tool keep the link when it rewrites parts from the art.</summary>
        public static ClothProfile FindFor(Mesh mesh)
        {
            if (mesh == null) return null;
            foreach (string guid in AssetDatabase.FindAssets("t:ClothProfile"))
            {
                var profile = AssetDatabase.LoadAssetAtPath<ClothProfile>(AssetDatabase.GUIDToAssetPath(guid));
                if (profile != null && profile.Mesh == mesh) return profile;
            }

            return null;
        }

        /// <summary>Computes the per-particle data for the hauberk mesh. Returns a problem, or null on success.</summary>
        public static string Bake(Mesh mesh, out float[] maxDistance, out float[] collision, out int particles)
        {
            maxDistance = collision = null;
            particles = 0;
            if (mesh == null) return "no mesh.";
            Vector3[] vertices = mesh.vertices;
            var used = new bool[vertices.Length];
            foreach (int index in mesh.triangles) used[index] = true;

            // Cloth welds vertices that share a position, in the order each position first appears in the mesh.
            var particleOf = new Dictionary<(int, int, int), int>();
            for (int i = 0; i < vertices.Length; i++)
            {
                Vector3 v = vertices[i];
                var key = (Mathf.RoundToInt(v.x * 10000f), Mathf.RoundToInt(v.y * 10000f), Mathf.RoundToInt(v.z * 10000f));
                if (!particleOf.ContainsKey(key)) particleOf.Add(key, particleOf.Count);
            }

            particles = particleOf.Count;
            int unity = UnityParticleCount(mesh);
            if (unity != particles) return "Unity builds " + unity + " Cloth particles for '" + mesh.name + "' but the vertex-position welding here finds " + particles + ". The particle order cannot be trusted; do not bake.";

            maxDistance = new float[particles];
            collision = new float[particles];
            foreach (var pair in particleOf)
            {
                // The height comes from the position the vertices share.
                float z = pair.Key.Item3 / 10000f;
                float t = Mathf.Clamp01((FixedAbove - z) / (FixedAbove - FreeBelow));
                maxDistance[pair.Value] = t * t * (3f - 2f * t) * HemMaxDistance;
            }

            return null;
        }

        /// <summary>How many particles Unity itself builds for the mesh: a throwaway Cloth on a throwaway renderer.</summary>
        public static int UnityParticleCount(Mesh mesh)
        {
            var go = new GameObject("cloth particle probe") { hideFlags = HideFlags.HideAndDontSave };
            try
            {
                var bone = go.transform;
                var renderer = go.AddComponent<SkinnedMeshRenderer>();
                renderer.sharedMesh = mesh;
                renderer.bones = Enumerable.Repeat(bone, mesh.bindposes.Length).ToArray();
                renderer.rootBone = bone;
                return go.AddComponent<Cloth>().coefficients.Length;
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(go);
            }
        }
    }
}
