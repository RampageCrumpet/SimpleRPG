using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace ClothDiagnostics
{
    /// <summary>
    /// Skin-weight study of the hauberk's lower skirt, in edit mode with deterministic clip sampling. Works on the scene's renderer and on duplicate meshes only:
    /// the FBX, the Blender source and the production assets are never written.
    /// </summary>
    public static class WeightStudy
    {
        const string RunClipFbx = "Assets/Blink/Art/Animations/Animations_Starter_Pack/Movement/RunForward.fbx";
        const string IdleClipFbx = "Assets/Blink/Art/Animations/Animations_Starter_Pack/Movement/Idle.fbx";
        public const string OutDir = "Assets/ClothDiagnostics/Results/";
        const float Cx = 0.006f;

        static GameObject character; static SkinnedMeshRenderer source, study; static Mesh original; static AnimationClip idle, run; static Transform hips;
        static Vector3[] V; static BoneWeight[] BW; static bool[] skirt; static Transform[] bones; static List<int[]> skirtTris; static List<(int p, int q, float rest)> edges; static List<CapsuleCollider> caps = new List<CapsuleCollider>();
        static Matrix4x4 M;

        static AnimationClip LoadClip(string fbx) => AssetDatabase.LoadAllAssetsAtPath(fbx).OfType<AnimationClip>().First(c => !c.name.StartsWith("__preview"));
        static Transform FindDeep(Transform root, string name) { if (root.name == name) return root; foreach (Transform c in root) { var f = FindDeep(c, name); if (f != null) return f; } return null; }

        static string BoneClass(string n) => n == "Hips" ? "Hips" : n.Contains("Spine") || n.Contains("Chest") ? "Spine/Chest" : n == "LeftUpperLeg" ? "LeftUpperLeg" : n == "RightUpperLeg" ? "RightUpperLeg" : n.Contains("LowerLeg") ? "LowerLeg" : "other:" + n;

        public static string Setup()
        {
            EditorApplication.ExecuteMenuItem("SimpleRPG/Items/Open Hauberk Cloth Review");
            character = GameObject.Find("Character");
            source = null; foreach (var s in character.GetComponentsInChildren<SkinnedMeshRenderer>(true)) if (s.sharedMesh != null && s.sharedMesh.name == "GAME_MailHauberk") source = s;
            original = source.sharedMesh; bones = source.bones; hips = FindDeep(character.transform, "Hips");
            var animator = character.GetComponent<Animator>(); if (animator != null) animator.enabled = false; // pose by clip sampling only
            V = original.vertices; BW = original.boneWeights; M = bones[0].localToWorldMatrix * original.bindposes[0];
            skirt = new bool[V.Length];
            for (int i = 0; i < V.Length; i++)
            {
                skirt[i] = V[i].z < 0.99f && Mathf.Abs(V[i].x - Cx) < 0.30f && V[i].z > 0.6f;
                if (skirt[i]) { string top = bones[TopBone(BW[i])].name; if (top.Contains("Arm") || top.Contains("Hand") || top.Contains("Shoulder") || top.Contains("Clavicle") || top.Contains("Thumb") || top.Contains("Index") || top.Contains("Middle") || top.Contains("Ring") || top.Contains("Pinky") || top.Contains("Little")) skirt[i] = false; }
            }
            var tris = original.triangles; skirtTris = new List<int[]>(); var seen = new HashSet<long>(); edges = new List<(int, int, float)>();
            for (int t = 0; t < tris.Length; t += 3)
            {
                int a = tris[t], b = tris[t + 1], c = tris[t + 2]; if (!(skirt[a] && skirt[b] && skirt[c])) continue; skirtTris.Add(new[] { a, b, c });
                foreach (var (p, q) in new[] { (a, b), (b, c), (c, a) }) { long k = p < q ? ((long)p << 32) | (uint)q : ((long)q << 32) | (uint)p; float rest = Vector3.Distance(V[p], V[q]); if (rest >= 0.02f && seen.Add(k)) edges.Add((p, q, rest)); }
            }
            idle = LoadClip(IdleClipFbx); run = LoadClip(RunClipFbx);
            // Study renderer: a duplicate that shares the rig, so variant meshes can be skinned without touching the scene's own renderer.
            var go = Object.Instantiate(source.gameObject, source.transform.parent); go.name = "Weight study renderer"; study = go.GetComponent<SkinnedMeshRenderer>(); study.forceRenderingOff = true; study.updateWhenOffscreen = true;
            source.forceRenderingOff = true;
            return $"skirt vertices {skirt.Count(x => x)}, skirt triangles {skirtTris.Count}, edges >= 2 cm {edges.Count}, bones {bones.Length}";
        }

        static int TopBone(BoneWeight w) { var l = new[] { (w.boneIndex0, w.weight0), (w.boneIndex1, w.weight1), (w.boneIndex2, w.weight2), (w.boneIndex3, w.weight3) }; return l.OrderByDescending(x => x.Item2).First().Item1; }

        // ---------------------------------------------------------------- 1 and 2: the weights

        static Dictionary<string, float> Influence(BoneWeight w)
        {
            var d = new Dictionary<string, float>();
            void Add(int i, float wt) { if (wt <= 0f) return; string c = BoneClass(bones[i].name); d[c] = (d.TryGetValue(c, out var v) ? v : 0f) + wt; }
            Add(w.boneIndex0, w.weight0); Add(w.boneIndex1, w.weight1); Add(w.boneIndex2, w.weight2); Add(w.boneIndex3, w.weight3); return d;
        }

        static string Summarize(IEnumerable<int> idx, string label)
        {
            var list = idx.ToList(); if (list.Count == 0) return $"   {label}: no vertices\n";
            var sum = new Dictionary<string, float>(); foreach (int i in list) foreach (var kv in Influence(BW[i])) sum[kv.Key] = (sum.TryGetValue(kv.Key, out var v) ? v : 0f) + kv.Value;
            string txt = string.Join(", ", sum.OrderByDescending(k => k.Value).Take(6).Select(k => $"{k.Key} {k.Value / list.Count * 100f:F0}%"));
            return $"   {label} ({list.Count} vertices): {txt}\n";
        }

        public static string ReportWeights()
        {
            var sb = new StringBuilder("== Lower-skirt skin weights (mean influence per vertex, original accepted mesh) ==\n");
            var all = Enumerable.Range(0, V.Length).Where(i => skirt[i]).ToList();
            sb.Append(Summarize(all, "all skirt vertices"));
            foreach (var (lo, hi, name) in new[] { (0.93f, 0.99f, "waist band z 0.93-0.99"), (0.84f, 0.93f, "hip band z 0.84-0.93"), (0.75f, 0.84f, "upper skirt z 0.75-0.84"), (0.645f, 0.75f, "hem band z 0.645-0.75") })
                sb.Append(Summarize(all.Where(i => V[i].z >= lo && V[i].z < hi), name));
            sb.AppendLine("by region below the hips (z < 0.93):");
            Func(sb, all, "front, at the slit edges (|x| < 0.12)", i => V[i].z < 0.93f && V[i].y < -0.05f && Mathf.Abs(V[i].x - Cx) < 0.12f);
            Func(sb, all, "front, beyond the slit (|x| >= 0.12)", i => V[i].z < 0.93f && V[i].y < -0.05f && Mathf.Abs(V[i].x - Cx) >= 0.12f);
            Func(sb, all, "left/right sides (|y| < 0.05)", i => V[i].z < 0.93f && Mathf.Abs(V[i].y) <= 0.05f);
            Func(sb, all, "back, at the slit edges", i => V[i].z < 0.93f && V[i].y > 0.05f && Mathf.Abs(V[i].x - Cx) < 0.12f);
            Func(sb, all, "back, beyond the slit", i => V[i].z < 0.93f && V[i].y > 0.05f && Mathf.Abs(V[i].x - Cx) >= 0.12f);
            // How many skirt vertices have a leg bone as a major influence
            int legMajor = 0, legAny = 0, hipsMajor = 0; foreach (int i in all)
            {
                var inf = Influence(BW[i]); float leg = (inf.TryGetValue("LeftUpperLeg", out var l) ? l : 0) + (inf.TryGetValue("RightUpperLeg", out var r) ? r : 0); float h = inf.TryGetValue("Hips", out var hh) ? hh : 0;
                if (leg > 0.5f) legMajor++; if (leg > 0.01f) legAny++; if (h > 0.5f) hipsMajor++;
            }
            sb.AppendLine($"vertices with more than half their weight on a thigh bone: {legMajor}/{all.Count}; with any thigh weight: {legAny}/{all.Count}; with more than half on Hips: {hipsMajor}/{all.Count}");
            return sb.ToString();
        }

        static void Func(StringBuilder sb, List<int> all, string name, System.Func<int, bool> pred) => sb.Append(Summarize(all.Where(pred), name));

        // ---------------------------------------------------------------- poses and measurements

        static Mesh bake;
        public static Vector3[] Pose(Mesh mesh, AnimationClip clip, float time)
        {
            clip.SampleAnimation(character, time);
            study.sharedMesh = mesh; study.bones = bones; study.BakeMesh(bake ??= new Mesh(), true);
            var local = bake.vertices; var w = new Vector3[local.Length]; for (int i = 0; i < w.Length; i++) w[i] = study.transform.TransformPoint(local[i]); return w;
        }

        public static (string name, AnimationClip clip, float time)[] Poses() => new[] { ("idle", idle, 0f), ("left stride (clip 0.15 s)", run, 0.15f), ("right stride (clip 0.483 s)", run, 0.483f) };

        static float SegDist(Vector3 p, Vector3 a, Vector3 b) { Vector3 ab = b - a; float t = Mathf.Clamp01(Vector3.Dot(p - a, ab) / Mathf.Max(ab.sqrMagnitude, 1e-8f)); return (p - (a + ab * t)).magnitude; }

        /// <summary>For one mesh in the three poses: 2 cm+ edge stretch, and how deep the visible mesh (vertices and triangle centres) is inside each thigh capsule, as given radii.</summary>
        public static string Compare(Mesh mesh, string label, float radiusLeft, float radiusRight, bool listWorst = false)
        {
            var sb = new StringBuilder($"-- {label}\n");
            Transform lu = FindDeep(character.transform, "LeftUpperLeg"), ll = FindDeep(character.transform, "LeftLowerLeg"), ru = FindDeep(character.transform, "RightUpperLeg"), rl = FindDeep(character.transform, "RightLowerLeg");
            foreach (var (name, clip, time) in Poses())
            {
                var w = Pose(mesh, clip, time); Vector3 la = lu.position, lb = ll.position, ra = ru.position, rb = rl.position;
                var ratios = new List<float>(); float worst = 0; int wp = 0, wq = 0;
                foreach (var (p, q, rest) in edges) { float r = Vector3.Distance(w[p], w[q]) / rest; ratios.Add(r); if (r > worst) { worst = r; wp = p; wq = q; } }
                ratios.Sort(); float P(float f) => ratios[Mathf.Min(ratios.Count - 1, (int)(ratios.Count * f))];
                float maxDepth = 0; int over2 = 0, over20 = 0, n = 0;
                void Test(Vector3 pt) { n++; float d = Mathf.Max(radiusLeft - SegDist(pt, la, lb), radiusRight - SegDist(pt, ra, rb)); if (d > maxDepth) maxDepth = d; if (d > 0.002f) over2++; if (d > 0.02f) over20++; }
                for (int i = 0; i < w.Length; i++) if (skirt[i]) Test(w[i]);
                foreach (var t in skirtTris) Test((w[t[0]] + w[t[1]] + w[t[2]]) / 3f);
                sb.AppendLine($"   {name,-26} edge stretch max {worst:F2}x p99 {P(0.99f):F2}x p95 {P(0.95f):F2}x | thigh penetration (radius {radiusLeft * 100f:F1}/{radiusRight * 100f:F1} cm): max {maxDepth * 100f:F1} cm, {over2}/{n} samples > 2 mm, {over20} > 2 cm");
                if (listWorst) sb.AppendLine($"        worst edge v{wp}-v{wq}: rest z {V[wp].z:F3}/{V[wq].z:F3}, x {V[wp].x:F3}/{V[wq].x:F3}, y {V[wp].y:F3}/{V[wq].y:F3}; weights {Describe(wp)} | {Describe(wq)}");
            }
            clip.SampleAnimation(character, 0f); idle.SampleAnimation(character, 0f);
            return sb.ToString();
        }

        static AnimationClip clip => idle;
        public static string Describe(int i) => string.Join("+", Influence(BW[i]).OrderByDescending(k => k.Value).Select(k => $"{k.Key.Replace("LeftUpperLeg", "LThigh").Replace("RightUpperLeg", "RThigh")} {k.Value * 100f:F0}%"));

        /// <summary>The stretched edges at left stride, with the weights of both ends, to see whether they straddle the hip/thigh transition.</summary>
        public static string WorstEdges(int count = 12)
        {
            var w = Pose(original, run, 0.15f); var list = new List<(float r, int p, int q)>();
            foreach (var (p, q, rest) in edges) list.Add((Vector3.Distance(w[p], w[q]) / rest, p, q)); list.Sort((a, b) => b.r.CompareTo(a.r));
            var sb = new StringBuilder("== The most stretched skirt edges at left stride (ordinary skinning, original weights) ==\n");
            int straddle = 0; for (int k = 0; k < count && k < list.Count; k++)
            {
                var e = list[k]; sb.AppendLine($"   {e.r:F2}x  v{e.p} (z {V[e.p].z:F3}, x {V[e.p].x:F3}, y {V[e.p].y:F3}) [{Describe(e.p)}]  --  v{e.q} (z {V[e.q].z:F3}, x {V[e.q].x:F3}, y {V[e.q].y:F3}) [{Describe(e.q)}]");
            }
            // How often does a stretched edge (>1.5x) have ends with different thigh influence?
            int stretched = 0, diff = 0; foreach (var e in list) { if (e.r <= 1.5f) break; stretched++; float lp = Influence(BW[e.p]).TryGetValue("LeftUpperLeg", out var a) ? a : 0, lq = Influence(BW[e.q]).TryGetValue("LeftUpperLeg", out var b) ? b : 0; float rp = Influence(BW[e.p]).TryGetValue("RightUpperLeg", out var c) ? c : 0, rq = Influence(BW[e.q]).TryGetValue("RightUpperLeg", out var d) ? d : 0; if (Mathf.Abs(lp - lq) > 0.25f || Mathf.Abs(rp - rq) > 0.25f) diff++; }
            sb.AppendLine($"   edges stretched over 1.5x at this pose: {stretched}; of those, ends differ by more than 25% in thigh weight: {diff}");
            return sb.ToString();
        }

        // ---------------------------------------------------------------- 3: the experimental weights

        /// <summary>A duplicate mesh whose lower skirt follows the Hips: full original weights above zUpper, all Hips below zLower, blended between. Positions, topology, UVs and materials are the original's.</summary>
        public static Mesh BuildPelvisMesh(float zUpper, float zLower, string assetName)
        {
            var mesh = Object.Instantiate(original); mesh.name = assetName;
            int hipsIndex = System.Array.FindIndex(bones, b => b.name == "Hips");
            var nbw = (BoneWeight[])BW.Clone(); int changed = 0;
            for (int i = 0; i < V.Length; i++)
            {
                if (!skirt[i] || V[i].z >= zUpper) continue;
                float t = Mathf.Clamp01((zUpper - V[i].z) / (zUpper - zLower)); t = t * t * (3f - 2f * t);
                var inf = new Dictionary<int, float>();
                void Add(int b, float wt) { if (wt > 0f) inf[b] = (inf.TryGetValue(b, out var v) ? v : 0f) + wt; }
                Add(BW[i].boneIndex0, BW[i].weight0 * (1f - t)); Add(BW[i].boneIndex1, BW[i].weight1 * (1f - t)); Add(BW[i].boneIndex2, BW[i].weight2 * (1f - t)); Add(BW[i].boneIndex3, BW[i].weight3 * (1f - t)); Add(hipsIndex, t);
                var top = inf.OrderByDescending(k => k.Value).Take(4).ToArray(); float sum = top.Sum(k => k.Value);
                var w = new BoneWeight();
                w.boneIndex0 = top[0].Key; w.weight0 = top[0].Value / sum;
                if (top.Length > 1) { w.boneIndex1 = top[1].Key; w.weight1 = top[1].Value / sum; }
                if (top.Length > 2) { w.boneIndex2 = top[2].Key; w.weight2 = top[2].Value / sum; }
                if (top.Length > 3) { w.boneIndex3 = top[3].Key; w.weight3 = top[3].Value / sum; }
                nbw[i] = w; changed++;
            }
            mesh.boneWeights = nbw;
            Directory.CreateDirectory("Assets/ClothDiagnostics/Meshes"); AssetDatabase.CreateAsset(mesh, $"Assets/ClothDiagnostics/Meshes/{assetName}.asset");
            return AssetDatabase.LoadAssetAtPath<Mesh>($"Assets/ClothDiagnostics/Meshes/{assetName}.asset");
        }

        public static Mesh Original => original;
        public static GameObject CharacterObj => character; public static bool[] SkirtMask => skirt; public static Vector3[] Verts => V; public static List<int[]> SkirtTrisList => skirtTris;
        public static AnimationClip RunClip => run; public static AnimationClip IdleClip => idle; public static SkinnedMeshRenderer StudyRenderer => study;

        // ---------------------------------------------------------------- the real leg, from the leggings

        public static string FitThighFromLeggings()
        {
            var legSmr = character.GetComponentsInChildren<SkinnedMeshRenderer>(true).Where(s => s.sharedMesh != null && s.sharedMesh.name.ToLower().Contains("legging")).ToList();
            var sb = new StringBuilder($"== Leg geometry from the leggings mesh ({string.Join(", ", legSmr.Select(s => s.sharedMesh.name))}) ==\n");
            idle.SampleAnimation(character, 0f);
            Transform lu = FindDeep(character.transform, "LeftUpperLeg"), ll = FindDeep(character.transform, "LeftLowerLeg");
            Vector3 a = lu.position, b = ll.position, axis = (b - a).normalized; float len = (b - a).magnitude;
            var pts = new List<Vector3>(); var m = new Mesh();
            foreach (var s in legSmr) { var prev = s.forceRenderingOff; s.BakeMesh(m, true); foreach (var p in m.vertices) pts.Add(s.transform.TransformPoint(p)); }
            // the thigh also includes the body-skin if leggings are separate; slice radially
            foreach (float t in new[] { 0.1f, 0.2f, 0.35f, 0.5f, 0.65f, 0.8f, 0.95f })
            {
                var rad = new List<float>(); Vector3 c = a + axis * (t * len);
                foreach (var p in pts) { float along = Vector3.Dot(p - a, axis); if (Mathf.Abs(along - t * len) > 0.02f) continue; Vector3 radial = p - (a + axis * along); if (radial.magnitude < 0.25f) rad.Add(radial.magnitude); }
                rad.Sort(); if (rad.Count == 0) { sb.AppendLine($"   t={t:F2}: no vertices"); continue; }
                sb.AppendLine($"   t={t:F2} along the thigh ({t * len * 100f:F0} cm below the hip joint): {rad.Count} vertices, radius min {rad[0] * 100f:F1} cm, median {rad[rad.Count / 2] * 100f:F1} cm, 90th percentile {rad[(int)(rad.Count * 0.9f)] * 100f:F1} cm, max {rad[rad.Count - 1] * 100f:F1} cm");
            }
            // the skirt's own closest approach to the thigh axis at rest
            float minSkirt = float.MaxValue; var w = Pose(original, idle, 0f); for (int i = 0; i < w.Length; i++) if (skirt[i]) minSkirt = Mathf.Min(minSkirt, SegDist(w[i], a, b));
            sb.AppendLine($"   the hauberk's closest approach to the thigh axis at rest: {minSkirt * 100f:F1} cm");
            return sb.ToString();
        }
    }
}
