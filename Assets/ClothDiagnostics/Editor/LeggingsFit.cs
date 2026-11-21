using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace ClothDiagnostics
{
    /// <summary>
    /// Fits the tapered thigh sphere pair to the ANIMATED leggings mesh (not to anatomy): for every pose of several clips it measures how far the visible leggings
    /// surface is from each thigh axis, takes the largest radius seen at each point along the thigh, and finds the smallest tapered pair that encloses all of it.
    /// Run with the integrated review scene open.
    /// </summary>
    public static class LeggingsFit
    {
        const string Movement = "Assets/Blink/Art/Animations/Animations_Starter_Pack/Movement/";
        static readonly string[] Clips = { "Idle", "RunForward", "Sprint", "RunLeft", "RunRight", "StrafeLeft", "StrafeRight", "RunBackward" };
        const int Bins = 20;

        static Transform FindDeep(Transform root, string name) { if (root.name == name) return root; foreach (Transform c in root) { var f = FindDeep(c, name); if (f != null) return f; } return null; }

        static float SegDist(Vector3 p, Vector3 a, Vector3 b, out float t) { var ab = b - a; t = Mathf.Clamp01(Vector3.Dot(p - a, ab) / ab.sqrMagnitude); return Vector3.Distance(p, a + ab * t); }

        /// <summary>True if the vertex is on the OUTER surface of a thigh: nearest of the four leg segments is a thigh, within thigh range, and its normal points away from the axis.</summary>
        static bool Classify(Vector3 v, Vector3 n, Vector3[] hip, Vector3[] knee, Vector3[] ankle, out int leg, out float t, out float r)
        {
            leg = -1; t = 0; r = 0; float best = float.MaxValue; bool thigh = false; float bt = 0;
            for (int l = 0; l < 2; l++)
            {
                float d = SegDist(v, hip[l], knee[l], out float tt); if (d < best) { best = d; leg = l; bt = tt; thigh = true; }
                d = SegDist(v, knee[l], ankle[l], out _); if (d < best) { best = d; leg = l; thigh = false; }
            }
            if (!thigh || best > 0.17f || bt < 0.08f || bt > 0.97f) return false;
            var ax = knee[leg] - hip[leg]; float tu = Vector3.Dot(v - hip[leg], ax) / ax.sqrMagnitude; var radial = (v - (hip[leg] + ax * tu)).normalized;
            if (Vector3.Dot(radial, n) < 0.6f) return false;
            t = tu; r = best; return true;
        }

        [MenuItem("SimpleRPG/Cloth/Fit Thigh Colliders To Leggings")]
        public static void Run()
        {
            var character = GameObject.Find("Character");
            var leggings = character.GetComponentsInChildren<SkinnedMeshRenderer>(true).First(r => r.name.Contains("Leggings"));
            var animator = character.GetComponent<Animator>(); if (animator != null) animator.enabled = false;
            Transform[] up = { FindDeep(character.transform, "LeftUpperLeg"), FindDeep(character.transform, "RightUpperLeg") }, kn = { FindDeep(character.transform, "LeftLowerLeg"), FindDeep(character.transform, "RightLowerLeg") }, ft = { FindDeep(character.transform, "LeftFoot"), FindDeep(character.transform, "RightFoot") };
            var sb = new StringBuilder("== Thigh collider fit against the animated leggings ==\nrequired radius = distance from the thigh axis (hip bone to knee bone) to the visible leggings surface, nearest leg, bins of 5% of the thigh (t)\n\n");
            var global = new float[Bins]; var perClip = new Dictionary<string, float[]>(); var mesh = new Mesh(); int poses = 0;
            var samples = new List<(Vector3[] hip, Vector3[] knee, Vector3[] ankle, Vector3[] verts, Vector3[] norms, string clip)>();
            foreach (string name in Clips)
            {
                var clip = AssetDatabase.LoadAllAssetsAtPath(Movement + name + ".fbx").OfType<AnimationClip>().FirstOrDefault(c => !c.name.StartsWith("__preview"));
                if (clip == null) { sb.AppendLine("missing clip " + name); continue; }
                var table = new float[Bins]; perClip[name] = table;
                int n = name == "Idle" ? 1 : Mathf.Min(40, Mathf.Max(8, Mathf.CeilToInt(clip.length * 30f)));
                for (int f = 0; f < n; f++)
                {
                    clip.SampleAnimation(character, n == 1 ? 0f : clip.length * f / n);
                    leggings.BakeMesh(mesh, true); var m = leggings.transform.localToWorldMatrix; var bv = mesh.vertices;
                    var bn = mesh.normals; var verts = new Vector3[bv.Length / 2]; var norms = new Vector3[verts.Length];
                    for (int i = 0; i < verts.Length; i++) { verts[i] = m.MultiplyPoint3x4(bv[i * 2]); norms[i] = m.MultiplyVector(bn[i * 2]).normalized; }
                    var hip = new[] { up[0].position, up[1].position }; var knee = new[] { kn[0].position, kn[1].position }; var ankle = new[] { ft[0].position, ft[1].position };
                    for (int i = 0; i < verts.Length; i++)
                    {
                        if (!Classify(verts[i], norms[i], hip, knee, ankle, out int leg, out float t, out float r)) continue;
                        int b = Mathf.Min(Bins - 1, (int)(t * Bins));
                        table[b] = Mathf.Max(table[b], r); global[b] = Mathf.Max(global[b], r);
                    }
                    samples.Add((hip, knee, ankle, verts, norms, name)); poses++;
                }
            }

            sb.AppendLine($"poses analysed: {poses} ({string.Join(", ", perClip.Keys)})");
            sb.AppendLine("t      required radius (cm): " + string.Join(" | ", perClip.Keys));
            for (int b = 0; b < Bins; b++)
                sb.AppendLine($"{(b + 0.5f) / Bins:F3}  ALL {global[b] * 100f,5:F1}   " + string.Join("  ", perClip.Select(p => $"{p.Value[b] * 100f,5:F1}")));

            // Smallest tapered pair [t0,r0]-[t1,r1] whose linear radius covers every bin between t0 and t1 with a clearance.
            const float clearance = 0.004f;
            (float t0, float r0, float t1, float r1, float area) pick = (0, 0, 0, 0, float.MaxValue);
            foreach (float t0 in new[] { 0.35f, 0.40f, 0.45f })
                foreach (float t1 in new[] { 0.90f, 0.95f })
                {
                    float best = float.MaxValue, bR0 = 0, bR1 = 0;
                    for (float r1 = 0.04f; r1 < 0.16f; r1 += 0.0005f)
                    {
                        float r0 = 0.04f; bool ok = (global.Skip((int)(t1 * Bins)).Take(1).DefaultIfEmpty(0).First() + clearance) <= r1 + 1e-5f || true;
                        for (int b = 0; b < Bins; b++)
                        {
                            float t = (b + 0.5f) / Bins; if (t < 0.4f || t < t0 || t > t1 || global[b] <= 0f) continue; float u = (t - t0) / (t1 - t0);
                            if (u < 1f) r0 = Mathf.Max(r0, (global[b] + clearance - r1 * u) / (1f - u));
                        }
                        // end caps: the sphere at each end must also reach the nearest bins outside the span
                        float cap1 = global[Mathf.Min(Bins - 1, (int)(t1 * Bins))] + clearance; if (r1 < cap1 - 0.0001f) continue;
                        float area = r0 * r0 + r0 * r1 + r1 * r1; if (area < best) { best = area; bR0 = r0; bR1 = r1; }
                    }
                    if (best < pick.area) pick = (t0, bR0, t1, bR1, best);
                }
            sb.AppendLine($"\nsmallest pair covering the animated leggings (+{clearance * 1000f:F0} mm): {pick.r0 * 100f:F1} cm at {pick.t0 * 100f:F0}% -> {pick.r1 * 100f:F1} cm at {pick.t1 * 100f:F0}%   (current: 10.5 cm at 15% -> 8.8 cm at 80%)");

            // Exact check of the fitted and the current pair against every posed vertex.
            foreach (var (label, t0, r0, t1, r1) in new[] { ("current", 0.15f, 0.105f, 0.80f, 0.088f), ("fitted", pick.t0, pick.r0, pick.t1, pick.r1) })
            {
                float worst = 0, worstUpper = 0; var worstByClip = new Dictionary<string, float>(); int outside5 = 0, total = 0;
                foreach (var s in samples)
                    for (int vi = 0; vi < s.verts.Length; vi++)
                    {
                        var v = s.verts[vi];
                        if (!Classify(v, s.norms[vi], s.hip, s.knee, s.ankle, out int l, out float t, out _)) continue; total++;
                        var ax = s.knee[l] - s.hip[l]; var A = s.hip[l] + ax * t0; var B = s.hip[l] + ax * t1; var ab = B - A; float u = Mathf.Clamp01(Vector3.Dot(v - A, ab) / ab.sqrMagnitude);
                        float gap = Vector3.Distance(v, A + ab * u) - Mathf.Lerp(r0, r1, u); // positive = the leggings vertex lies outside the collider surface by this much
                        if (t < 0.4f) { worstUpper = Mathf.Max(worstUpper, gap); continue; } if (gap > 0.0005f) outside5++;
                        worst = Mathf.Max(worst, gap); worstByClip[s.clip] = Mathf.Max(worstByClip.TryGetValue(s.clip, out float w) ? w : 0f, gap);
                    }
                sb.AppendLine($"{label,-8} leggings vertices (lower thigh, t>=0.4) outside the collider surface: worst {worst * 100f:F1} cm; upper thigh/hip (t<0.4) worst {worstUpper * 100f:F1} cm; {outside5} of {total} by >0.5 mm; per clip: " + string.Join(", ", worstByClip.Select(p => $"{p.Key} {p.Value * 100f:F1}")));
            }

            Directory.CreateDirectory("Assets/ClothDiagnostics/Results");
            File.WriteAllText("Assets/ClothDiagnostics/Results/leggings_fit.txt", sb.ToString());
            Debug.Log(sb.ToString());
        }
    }
}
