using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace ClothDiagnostics
{
    /// <summary>
    /// Clearance study for thigh collider candidates, on the W2 reference pose, plus the W2 upper-body check.
    /// A collider is a tapered pair of spheres on the thigh bone: t0 and t1 are fractions of the hip-to-knee distance, r0 and r1 the radii there.
    /// </summary>
    public static class ColliderStudy
    {
        public struct Spec { public string name; public float t0, r0, t1, r1; }
        // The real leg, fitted to the leggings: 10.5 cm at 15% of the thigh, narrowing to 7.1 cm at 95%.
        public static readonly Spec RealLeg = new Spec { name = "real leg model", t0 = 0.15f, r0 = 0.105f, t1 = 0.95f, r1 = 0.071f };
        static float RealRadius(float t) => Mathf.Lerp(RealLeg.r0, RealLeg.r1, Mathf.Clamp01((t - RealLeg.t0) / (RealLeg.t1 - RealLeg.t0)));
        public static Spec Make(string name, float t0, float t1) => new Spec { name = name, t0 = t0, r0 = RealRadius(t0), t1 = t1, r1 = RealRadius(t1) };

        static Transform FindDeep(Transform root, string name) { if (root.name == name) return root; foreach (Transform c in root) { var f = FindDeep(c, name); if (f != null) return f; } return null; }

        // distance to a tapered capsule (sphere A to sphere B): positive outside, negative inside
        static float Signed(Vector3 p, Vector3 a, Vector3 b, float ra, float rb)
        {
            Vector3 ab = b - a; float u = Mathf.Clamp01(Vector3.Dot(p - a, ab) / Mathf.Max(ab.sqrMagnitude, 1e-8f));
            return Vector3.Distance(p, a + ab * u) - Mathf.Lerp(ra, rb, u);
        }

        public static string Analyze(Mesh variant, Spec[] candidates)
        {
            var ch = WeightStudy.CharacterObj; var skirt = WeightStudy.SkirtMask; var tris = WeightStudy.SkirtTrisList; var V = WeightStudy.Verts;
            var run = WeightStudy.RunClip; var idle = WeightStudy.IdleClip;
            Transform[] up = { FindDeep(ch.transform, "LeftUpperLeg"), FindDeep(ch.transform, "RightUpperLeg") }, kn = { FindDeep(ch.transform, "LeftLowerLeg"), FindDeep(ch.transform, "RightLowerLeg") };
            var poses = new List<(AnimationClip c, float t)> { (idle, 0f) }; for (float t = 0f; t < run.length; t += 1f / 60f) poses.Add((run, t));
            var sb = new StringBuilder($"== Collider clearance on the W2 reference pose, {poses.Count} poses (idle + one run cycle) ==\n");
            sb.AppendLine("depth = how far the garment's skinned reference is inside a surface; 'clip' = depth into the real leg that the candidate does NOT push out (assuming the cloth ends on the collider surface).");
            // Gather the skinned samples once.
            var samples = new List<Vector3[]>(); var legPoints = new List<(Vector3 a, Vector3 b)[]>();
            foreach (var (c, t) in poses)
            {
                var w = WeightStudy.Pose(variant, c, t); var list = new List<Vector3>();
                for (int i = 0; i < w.Length; i++) if (skirt[i]) list.Add(w[i]);
                foreach (var tr in tris) list.Add((w[tr[0]] + w[tr[1]] + w[tr[2]]) / 3f);
                samples.Add(list.ToArray());
                legPoints.Add(new[] { (up[0].position, kn[0].position), (up[1].position, kn[1].position) });
            }
            foreach (var spec in candidates.Prepend(RealLeg))
            {
                float restMax = 0; int restOver1 = 0, restOver3 = 0; float runMax = 0; int runOver2 = 0, total = 0; float clipMax = 0; int clipOver2 = 0, clipOver5 = 0; int everInside = 0;
                var inside = new HashSet<int>();
                for (int k = 0; k < samples.Count; k++)
                {
                    for (int si = 0; si < samples[k].Length; si++)
                    {
                        Vector3 p = samples[k][si]; float depthC = 0, depthReal = 0; float collRadiusAtHit = 0, realRadiusAtHit = 0;
                        for (int leg = 0; leg < 2; leg++)
                        {
                            Vector3 hip = legPoints[k][leg].a, knee = legPoints[k][leg].b; Vector3 ax = knee - hip;
                            Vector3 ca = hip + ax * spec.t0, cb = hip + ax * spec.t1; Vector3 ra = hip + ax * RealLeg.t0, rb = hip + ax * RealLeg.t1;
                            float sc = -Signed(p, ca, cb, spec.r0, spec.r1); if (sc > depthC) { depthC = sc; }
                            // real leg: depth, and where the real surface is at the contact
                            float sr = -Signed(p, ra, rb, RealLeg.r0, RealLeg.r1); if (sr > depthReal) depthReal = sr;
                        }
                        if (k == 0) { restMax = Mathf.Max(restMax, depthC); if (depthC > 0.01f) restOver1++; if (depthC > 0.03f) restOver3++; }
                        else { runMax = Mathf.Max(runMax, depthC); if (depthC > 0.02f) runOver2++; total++; if (depthC > 0.002f) inside.Add(si); }
                        // clip the collider leaves: if inside the collider the cloth ends on its surface, so whatever the real leg still exceeds there remains; otherwise the whole real depth remains
                        float clip = depthC > 0f ? Mathf.Max(0f, depthReal - depthC) : depthReal;
                        if (k > 0) { clipMax = Mathf.Max(clipMax, clip); if (clip > 0.02f) clipOver2++; if (clip > 0.05f) clipOver5++; }
                    }
                }
                sb.AppendLine($"{spec.name,-34} t {spec.t0:F2}-{spec.t1:F2}, r {spec.r0 * 100f:F1}->{spec.r1 * 100f:F1} cm | rest overlap max {restMax * 100f:F1} cm, {restOver1} samples > 1 cm, {restOver3} > 3 cm | run: reference inside it up to {runMax * 100f:F1} cm, {inside.Count} distinct samples touch it | clip left in the real leg: max {clipMax * 100f:F1} cm, {clipOver2} sample-poses > 2 cm, {clipOver5} > 5 cm");
            }
            return sb.ToString();
        }

        // ---------------------------------------------------------------- W2 upper-body check

        public static string VerifyUpperBody(Mesh original, Mesh w2)
        {
            var V = WeightStudy.Verts; var skirt = WeightStudy.SkirtMask; var sb = new StringBuilder("== W2 versus the original weights, ordinary skinning, per region (distance between the two skinned positions) ==\n");
            foreach (var (name, clip, time) in WeightStudy.Poses())
            {
                var a = WeightStudy.Pose(original, clip, time); var b = WeightStudy.Pose(w2, clip, time);
                string R(System.Func<int, bool> pred, string label)
                {
                    float max = 0, sum = 0; int n = 0; for (int i = 0; i < V.Length; i++) if (pred(i)) { float d = Vector3.Distance(a[i], b[i]); max = Mathf.Max(max, d); sum += d; n++; }
                    return $"{label}: n={n}, max {max * 1000f:F1} mm, mean {(n > 0 ? sum / n : 0) * 1000f:F2} mm";
                }
                sb.AppendLine($"  {name}:");
                sb.AppendLine("     " + R(i => V[i].z >= 1.10f, "torso and sleeves above z 1.10 (should be identical)"));
                sb.AppendLine("     " + R(i => V[i].z >= 0.99f && V[i].z < 1.10f, "belt-height torso, z 0.99-1.10 (re-weighted blend)"));
                sb.AppendLine("     " + R(i => skirt[i] && V[i].z >= 0.93f && V[i].z < 0.99f, "waist band, z 0.93-0.99"));
                sb.AppendLine("     " + R(i => skirt[i] && V[i].z < 0.93f, "lower skirt below z 0.93 (should be identical)"));
            }
            return sb.ToString();
        }

        /// <summary>Renders the original and W2 hauberk (torso and belt area) from the front, side and rear, at idle and at left stride.</summary>
        public static string RenderUpperBody(Mesh original, Mesh w2, string path)
        {
            var ch = WeightStudy.CharacterObj; var cam = Camera.main; cam.orthographic = false; cam.fieldOfView = 24f; var study = WeightStudy.StudyRenderer;
            var views = new (string, float)[] { ("front", 0f), ("side", 90f), ("rear", 180f) }; const int W = 320, H = 400;
            var rows = new List<Texture2D>(); var poses = new[] { ("idle", WeightStudy.IdleClip, 0f), ("left stride", WeightStudy.RunClip, 0.15f) };
            var tex = new Texture2D(W * views.Length, H * poses.Length * 2, TextureFormat.RGB24, false); int row = 0;
            foreach (var (pn, clip, time) in poses)
                foreach (var mesh in new[] { original, w2 })
                {
                    clip.SampleAnimation(ch, time); study.sharedMesh = mesh; study.forceRenderingOff = false;
                    Vector3 look = ch.transform.position + new Vector3(0, 1.0f, 0);
                    for (int v = 0; v < views.Length; v++)
                    {
                        cam.transform.position = look + Quaternion.Euler(0, views[v].Item2, 0) * new Vector3(0, 0.1f, 1.8f); cam.transform.LookAt(look); cam.aspect = (float)W / H;
                        var rt = RenderTexture.GetTemporary(W, H, 24); cam.targetTexture = rt; cam.Render(); RenderTexture.active = rt;
                        var t = new Texture2D(W, H, TextureFormat.RGB24, false); t.ReadPixels(new Rect(0, 0, W, H), 0, 0); t.Apply(); cam.targetTexture = null; RenderTexture.active = null; RenderTexture.ReleaseTemporary(rt);
                        tex.SetPixels(v * W, (poses.Length * 2 - 1 - row) * H, W, H, t.GetPixels()); Object.DestroyImmediate(t);
                    }
                    row++;
                }
            study.forceRenderingOff = true; tex.Apply(); File.WriteAllBytes(path, tex.EncodeToPNG()); Object.DestroyImmediate(tex);
            return "rows: idle original, idle W2, left stride original, left stride W2; columns front, side, rear";
        }
    }
}
