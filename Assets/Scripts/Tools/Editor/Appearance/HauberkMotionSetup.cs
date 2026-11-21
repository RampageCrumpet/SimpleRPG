using System;
using System.Collections.Generic;
using System.Linq;
using SimpleRPG.Appearance;
using UnityEditor;
using UnityEngine;

namespace SimpleRPG.AppearanceTools
{
    /// <summary>
    /// Gives the mail hauberk's skirt its own hanging chains of simulated bones (see <see cref="CordBoneMotion"/>). The skirt hangs from the pelvis in
    /// panels instead of being skinned half to the hips and half to each thigh, so the hip crease never has to squash it: a panel is held off the
    /// thigh by an analytic capsule, and the mesh stretches nowhere. The canonical 52-bone rig and the exported FBX stay unchanged.
    /// </summary>
    public static class HauberkMotionSetup
    {
        public const string Folder = "Assets/Art/3d Models/Items/MailHauberk";
        private const string PartPath = "Assets/Art/BodyParts/Armor/MailHauberk_GAME_MailHauberk.asset";
        private const string ProfilePath = Folder + "/MailHauberkMotion.asset";

        // The mesh's own space is the Blender scene: x across, y front-to-back (front is negative), z up, in metres.
        private const int Columns = 20;
        private static readonly float[] Heights = { 0.94f, 0.876f, 0.812f, 0.748f, 0.684f, 0.62f };
        private const float SkirtTop = 0.98f;          // above this the mail follows the skin exactly
        private const float FreeBelow = 0.92f;         // below this it follows its chain exactly
        private const float SkirtHalfWidth = 0.27f;    // beyond this are the arms
        private static readonly Vector2 BodyCenter = new Vector2(0.006f, 0.011f);

        [MenuItem("SimpleRPG/Items/Build Hauberk Bone Motion")]
        public static void Build()
        {
            AssetDatabase.Refresh();
            string fbx = Folder + "/MailHauberk.fbx";
            var importer = (ModelImporter)AssetImporter.GetAtPath(fbx);
            if (!importer.isReadable)
            {
                importer.isReadable = true;
                importer.SaveAndReimport();
            }

            var instance = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(fbx));
            try
            {
                var renderer = instance.GetComponentsInChildren<SkinnedMeshRenderer>(true).Single();
                if (renderer.sharedMesh.bindposes.Length != 52) throw new InvalidOperationException("The hauberk must be skinned to the 52 canonical bones.");
                var profile = BuildProfile(renderer, new RigBoneIndex(instance.transform));
                var part = AssetDatabase.LoadAssetAtPath<BodyPartDefinition>(PartPath);
                part.Configure(part.Slot, renderer.sharedMesh, part.Materials, renderer.bones.Select(b => b.name).ToArray(), renderer.rootBone.name,
                    renderer.localBounds, "", Vector3.zero, Quaternion.identity, Vector3.one, profile);
                EditorUtility.SetDirty(part);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(instance);
            }

            AssetDatabase.SaveAssets();
            Debug.Log("[Armor] Hauberk: skirt hangs from " + Columns + " bone chains.");
        }

        private static CordMotionProfile BuildProfile(SkinnedMeshRenderer renderer, RigBoneIndex rig)
        {
            var profile = AssetDatabase.LoadAssetAtPath<CordMotionProfile>(ProfilePath);
            if (profile == null)
            {
                profile = ScriptableObject.CreateInstance<CordMotionProfile>();
                AssetDatabase.CreateAsset(profile, ProfilePath);
            }

            Mesh source = renderer.sharedMesh;
            Vector3[] vertices = source.vertices;
            BoneWeight[] weights = source.boneWeights;
            Matrix4x4 localToWorld = renderer.localToWorldMatrix;
            Transform anchor = rig.Require("Hips");

            // Which columns exist: those whose middle is not inside a slit (the front and back centre lines are open).
            var valid = new List<int>();
            for (int c = 0; c < Columns; c++)
            {
                float angle = c * Mathf.PI * 2f / Columns;
                float x = BodyCenter.x + 0.2f * Mathf.Cos(angle);
                if (Mathf.Abs(x) >= 0.07f) valid.Add(c);
            }

            // Each chain's points sit on the middle of the skirt's thickness at that angle and height.
            var chains = new CordMotionProfile.Chain[valid.Count];
            var restPoints = new Vector3[valid.Count][];
            for (int k = 0; k < valid.Count; k++)
            {
                float angle = valid[k] * Mathf.PI * 2f / Columns;
                var points = new Vector3[Heights.Length];
                var normals = new Vector3[Heights.Length];
                for (int j = 0; j < Heights.Length; j++)
                {
                    points[j] = SurfacePoint(vertices, angle, Heights[j]);
                    normals[j] = new Vector3(points[j].x - BodyCenter.x, points[j].y - BodyCenter.y, 0f).normalized;
                }

                restPoints[k] = points;
                chains[k] = new CordMotionProfile.Chain { name = "Hauberk_" + valid[k], attachment = "Hips", waist = false, points = new Vector3[points.Length], normals = new Vector3[points.Length] };
            }

            // Bone for each chain segment: bind pose puts the bone at the chain point, turned like the anchor (as the breeches do).
            var binds = source.bindposes.ToList();
            var offsets = new int[valid.Count];
            for (int k = 0; k < valid.Count; k++)
            {
                offsets[k] = binds.Count;
                for (int j = 0; j < Heights.Length; j++)
                {
                    Vector3 world = localToWorld.MultiplyPoint3x4(restPoints[k][j]);
                    if (j < Heights.Length - 1) binds.Add(Matrix4x4.TRS(world, anchor.rotation, Vector3.one).inverse * localToWorld);
                    chains[k].points[j] = anchor.InverseTransformPoint(world);
                    Vector3 outward = new Vector3(restPoints[k][j].x - BodyCenter.x, restPoints[k][j].y - BodyCenter.y, 0f).normalized;
                    chains[k].normals[j] = anchor.InverseTransformDirection(localToWorld.MultiplyVector(outward)).normalized;
                }
            }

            int bones = Heights.Length - 1;
            var skinned = (BoneWeight[])weights.Clone();
            float step = Mathf.PI * 2f / Columns;
            for (int i = 0; i < vertices.Length; i++)
            {
                Vector3 v = vertices[i];
                if (Mathf.Abs(v.x) >= SkirtHalfWidth || v.z >= SkirtTop) continue;
                float angle = Mathf.Atan2(v.y - BodyCenter.y, v.x - BodyCenter.x);
                if (angle < 0) angle += Mathf.PI * 2f;

                // The two valid columns either side of this angle (or just the nearest when it sits in a slit).
                int low = -1, high = -1;
                float lowAngle = 0, highAngle = 0, best = float.MaxValue, bestLow = float.MaxValue, bestHigh = float.MaxValue;
                for (int k = 0; k < valid.Count; k++)
                {
                    float a = valid[k] * step;
                    float delta = Mathf.DeltaAngle(angle * Mathf.Rad2Deg, a * Mathf.Rad2Deg) * Mathf.Deg2Rad; // + when column is ahead
                    if (delta >= 0 && delta < bestHigh) { bestHigh = delta; high = k; highAngle = delta; }
                    if (delta <= 0 && -delta < bestLow) { bestLow = -delta; low = k; lowAngle = -delta; }
                    if (Mathf.Abs(delta) < best) best = Mathf.Abs(delta);
                }

                float wLow, wHigh;
                if (low >= 0 && high >= 0 && lowAngle + highAngle <= step * 1.01f)
                {
                    float span = Mathf.Max(lowAngle + highAngle, 1e-5f);
                    wLow = highAngle / span;
                    wHigh = lowAngle / span;
                }
                else if (lowAngle <= highAngle || high < 0) { wLow = 1; wHigh = 0; }
                else { wLow = 0; wHigh = 1; }
                if (low < 0) { low = high; wLow = 0; wHigh = 1; }
                if (high < 0) { high = low; wHigh = 0; wLow = 1; }

                if (v.z > FreeBelow)
                {
                    // Blend zone: the two strongest skin bones fade out as the two top chain bones fade in.
                    float t = Mathf.Clamp01((SkirtTop - v.z) / (SkirtTop - FreeBelow));
                    float chain = t * t * (3f - 2f * t);
                    var (b0, w0, b1, w1) = TopTwo(weights[i]);
                    float norm = Mathf.Max(w0 + w1, 1e-5f);
                    skinned[i] = new BoneWeight
                    {
                        boneIndex0 = b0, weight0 = (1f - chain) * w0 / norm,
                        boneIndex1 = b1, weight1 = (1f - chain) * w1 / norm,
                        boneIndex2 = offsets[low], weight2 = chain * wLow,
                        boneIndex3 = offsets[high], weight3 = chain * wHigh,
                    };
                    continue;
                }

                float h = Mathf.Clamp01((Heights[0] - v.z) / (Heights[0] - Heights[Heights.Length - 1])) * bones;
                int lower = Mathf.Min(Mathf.FloorToInt(h), bones - 1);
                int upper = Mathf.Min(lower + 1, bones - 1);
                float blend = upper == lower ? 0f : h - lower;
                skinned[i] = new BoneWeight
                {
                    boneIndex0 = offsets[low] + lower, weight0 = wLow * (1f - blend),
                    boneIndex1 = offsets[low] + upper, weight1 = wLow * blend,
                    boneIndex2 = offsets[high] + lower, weight2 = wHigh * (1f - blend),
                    boneIndex3 = offsets[high] + upper, weight3 = wHigh * blend,
                };
            }

            // The thighs hold the panels off the legs. Radius is the most that leaves every panel where it already is at rest.
            var blockers = new List<CordMotionProfile.Blocker>();
            foreach (string side in new[] { "Left", "Right" })
            {
                Transform thigh = rig.Require(side + "UpperLeg");
                Transform knee = rig.Require(side + "LowerLeg");
                Vector3 a = thigh.position, c = knee.position;
                float radius = 0.2f;
                foreach (var points in restPoints)
                {
                    foreach (Vector3 p in points)
                    {
                        Vector3 world = localToWorld.MultiplyPoint3x4(p);
                        Vector3 axis = c - a;
                        float t = Mathf.Clamp01(Vector3.Dot(world - a, axis) / axis.sqrMagnitude);
                        radius = Mathf.Min(radius, Vector3.Distance(world, a + axis * t));
                    }
                }

                blockers.Add(new CordMotionProfile.Blocker { bone = side + "UpperLeg", start = Vector3.zero, end = thigh.InverseTransformPoint(c), radius = Mathf.Max(0.04f, radius - 0.004f) });
                Debug.Log("[Armor] Hauberk " + side + " thigh blocker radius " + blockers[blockers.Count - 1].radius.ToString("0.000"));
            }

            var generated = UnityEngine.Object.Instantiate(source);
            generated.name = "MailHauberk_SimulatedSkin";
            generated.bindposes = binds.ToArray();
            generated.boneWeights = skinned;
            Mesh visible = profile.VisibleMesh;
            if (visible == null)
            {
                visible = new Mesh();
                AssetDatabase.AddObjectToAsset(visible, profile);
            }

            EditorUtility.CopySerialized(generated, visible);
            Mesh hidden = profile.HiddenWaistMesh;
            if (hidden == null)
            {
                hidden = new Mesh();
                AssetDatabase.AddObjectToAsset(hidden, profile);
            }

            EditorUtility.CopySerialized(generated, hidden);
            UnityEngine.Object.DestroyImmediate(generated);
            profile.Configure(source, visible, hidden, chains);
            profile.SetBlockers(blockers.ToArray());
            profile.SetGravityScale(6f);
            EditorUtility.SetDirty(visible);
            EditorUtility.SetDirty(hidden);
            EditorUtility.SetDirty(profile);
            return profile;
        }

        // Middle of the skirt's thickness at an angle and height: the mean of the nearby vertices of both shells.
        private static Vector3 SurfacePoint(Vector3[] vertices, float angle, float height)
        {
            for (float reach = 0.12f; reach < 0.6f; reach += 0.1f)
            {
                Vector3 sum = Vector3.zero;
                int n = 0;
                foreach (Vector3 v in vertices)
                {
                    if (Mathf.Abs(v.x) >= SkirtHalfWidth || Mathf.Abs(v.z - height) > 0.035f) continue;
                    float a = Mathf.Atan2(v.y - BodyCenter.y, v.x - BodyCenter.x);
                    if (Mathf.Abs(Mathf.DeltaAngle(a * Mathf.Rad2Deg, angle * Mathf.Rad2Deg)) * Mathf.Deg2Rad > reach * 0.5f) continue;
                    sum += v;
                    n++;
                }

                if (n > 0)
                {
                    Vector3 mean = sum / n;
                    mean.z = height;
                    return mean;
                }
            }

            return new Vector3(BodyCenter.x + 0.2f * Mathf.Cos(angle), BodyCenter.y + 0.2f * Mathf.Sin(angle), height);
        }

        private static (int, float, int, float) TopTwo(BoneWeight w)
        {
            var list = new[] { (w.boneIndex0, w.weight0), (w.boneIndex1, w.weight1), (w.boneIndex2, w.weight2), (w.boneIndex3, w.weight3) };
            Array.Sort(list, (x, y) => y.Item2.CompareTo(x.Item2));
            return (list[0].Item1, list[0].Item2, list[1].Item1, list[1].Item2);
        }
    }
}
