using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

// Copy of RetopoHarness for the smoothed-hauberk trial (Assets/ClothFitTest): adds the harness frame 784 raised-knee shot and a straight rear view. Not part of the game.
// Fixed 60 fps clock, so the cloth run and the ordinary-skinning run reach exactly the same animation frames.
// Timeline: idle 0-4 s, walk 4-9 s, run 9-16 s. It renders screenshots at scheduled frames and logs per-frame deformation and clipping numbers.
public class FitHarness : MonoBehaviour
{
    public bool clothOn = true;
    public string outDir = "";
    public float walkSpeed = 1.4f, runSpeed = 3.2f;
    // diagnostics (investigation of the frame-784 failure): hold the animation at freezeFrame while Cloth keeps simulating; run in place; stop early; skip screenshots
    public int freezeFrame = -1, endFrame = 16 * 60, dumpFrame = -1, dumpFrame2 = -1, perFrameFrom = 700;
    public bool inPlace, shoot = true;
    private bool frozen;
    public int CurrentFrame => frame - 1;   // index of the frame being rendered (read by ClothValidator)

    private Animator animator;
    private SkinnedMeshRenderer hauberk;
    private Cloth cloth;
    private SkinnedMeshRenderer leggings;
    private int frame;
    private string phase = "";
    private Camera cam;
    private RenderTexture rt;
    private StringBuilder csv = new StringBuilder();

    // Particle bookkeeping (unique welded positions in first-occurrence order = Unity's Cloth particle order).
    private List<int> first = new List<int>();
    private Vector3[] meshVerts;
    private BoneWeight[] weights;
    private Matrix4x4[] bind;
    private List<(int a, int b, float rest)> edges = new List<(int, int, float)>();
    private List<(int a, int b, int c, float rest)> tris = new List<(int, int, int, float)>();
    private bool[] free;

    private struct View { public string name; public Vector3 pos, target; public float fov; public int hide; }   // hide: 0 none, 1 all but leggings+hauberk, 2 all but hauberk

    private static readonly View[] AllViews =
    {
        new View { name = "front34", pos = new Vector3(1.1f, 1.1f, 1.6f), target = new Vector3(0, 0.95f, 0), fov = 40 },
        new View { name = "rear34", pos = new Vector3(-1.1f, 1.1f, -1.6f), target = new Vector3(0, 0.95f, 0), fov = 40 },
        new View { name = "rear", pos = new Vector3(0, 1.0f, -2.0f), target = new Vector3(0, 0.95f, 0), fov = 38 },
        new View { name = "side", pos = new Vector3(2.0f, 0.95f, 0), target = new Vector3(0, 0.95f, 0), fov = 38 },
        new View { name = "front", pos = new Vector3(0, 1.0f, 2.0f), target = new Vector3(0, 0.95f, 0), fov = 38 },
        new View { name = "skirt_front_close", pos = new Vector3(0.55f, 0.8f, 1.0f), target = new Vector3(0, 0.78f, 0.05f), fov = 40 },
        new View { name = "skirt_side_close", pos = new Vector3(1.05f, 0.78f, 0.45f), target = new Vector3(0, 0.78f, 0.0f), fov = 40 },
        new View { name = "waist_close", pos = new Vector3(0.7f, 1.0f, 0.95f), target = new Vector3(0, 0.95f, 0.05f), fov = 35 },
        new View { name = "under_front", pos = new Vector3(0, 0.28f, 0.95f), target = new Vector3(0, 0.85f, 0), fov = 55 },
        new View { name = "interior_skirt_from_below", pos = new Vector3(0.0f, 0.25f, 0.8f), target = new Vector3(0, 0.95f, 0.0f), fov = 60, hide = 2 },
        new View { name = "interior_skirt_rear_below", pos = new Vector3(0.0f, 0.25f, -0.8f), target = new Vector3(0, 0.95f, 0.0f), fov = 60, hide = 2 },
        new View { name = "interior_sleeve_L", pos = new Vector3(-0.42f, 0.45f, 0.18f), target = new Vector3(-0.30f, 1.25f, 0.0f), fov = 55, hide = 2 },
        new View { name = "interior_sleeve_R", pos = new Vector3(0.42f, 0.45f, 0.18f), target = new Vector3(0.30f, 1.25f, 0.0f), fov = 55, hide = 2 },
        new View { name = "interior_collar_down", pos = new Vector3(0, 1.95f, 0.35f), target = new Vector3(0, 1.1f, 0.0f), fov = 60, hide = 2 },
    };

    private static readonly string[] SeqViews = { "front34", "side", "rear34", "skirt_front_close" };

    private HashSet<int> keyFrames = new HashSet<int> { 3 * 60, 7 * 60, 12 * 60, 784, 772, 796 };
    private HashSet<int> seqFrames = new HashSet<int>();

    private void Start()
    {
        Application.runInBackground = true;
        Time.captureFramerate = 60;
        animator = GetComponent<Animator>();
        animator.enabled = true;
        hauberk = GetComponentInChildren<Cloth>(true).GetComponent<SkinnedMeshRenderer>();
        cloth = hauberk.GetComponent<Cloth>();
        foreach (var s in GetComponentsInChildren<SkinnedMeshRenderer>(true)) if (s.name.StartsWith("ReinforcedLeatherLeggings")) leggings = s;
        cloth.enabled = clothOn;

        // walk: four phases of the 1.2 s stride; run: eight samples across the 0.667 s stride
        for (int k = 1; k < 4; k++) seqFrames.Add(7 * 60 + k * 18);
        for (int k = 1; k < 8; k++) seqFrames.Add(12 * 60 + Mathf.RoundToInt(k * 40f / 8f));
        for (int k = 0; k < 8; k++) seqFrames.Add(13 * 60 + Mathf.RoundToInt(k * 40f / 8f));

        if (freezeFrame >= 0) { keyFrames.Add(freezeFrame + 120); keyFrames.Add(freezeFrame + 30); }
        Mesh mesh = hauberk.sharedMesh;
        meshVerts = mesh.vertices; weights = mesh.boneWeights; bind = mesh.bindposes;
        var seen = new Dictionary<(int, int, int), int>();
        var map = new int[meshVerts.Length];
        for (int i = 0; i < meshVerts.Length; i++)
        {
            var k = (Mathf.RoundToInt(meshVerts[i].x * 10000f), Mathf.RoundToInt(meshVerts[i].y * 10000f), Mathf.RoundToInt(meshVerts[i].z * 10000f));
            if (!seen.TryGetValue(k, out int p)) { p = first.Count; seen[k] = p; first.Add(i); }
            map[i] = p;
        }

        var co = cloth.coefficients;
        free = new bool[first.Count];
        for (int p = 0; p < first.Count; p++) free[p] = co[p].maxDistance > 0f;
        var edgeSet = new HashSet<long>();
        int[] t = mesh.triangles;
        for (int i = 0; i < t.Length; i += 3)
        {
            int a = map[t[i]], b = map[t[i + 1]], c = map[t[i + 2]];
            tris.Add((a, b, c, Vector3.Cross(meshVerts[first[b]] - meshVerts[first[a]], meshVerts[first[c]] - meshVerts[first[a]]).magnitude * 0.5f));
            foreach (var (u, v) in new[] { (a, b), (b, c), (c, a) })
            {
                long key = u < v ? (long)u * 100000 + v : (long)v * 100000 + u;
                if (edgeSet.Add(key)) edges.Add((u, v, (meshVerts[first[u]] - meshVerts[first[v]]).magnitude));
            }
        }

        // Keep the scene camera on and simulate off-screen, as ClothGarment does, so Unity's Cloth never sleeps because the renderer is culled.
        hauberk.updateWhenOffscreen = true;
        var camObject = new GameObject("HarnessCamera");
        cam = camObject.AddComponent<Camera>();
        cam.enabled = false;
        cam.nearClipPlane = 0.02f;
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0.12f, 0.14f, 0.17f);
        rt = new RenderTexture(1024, 1024, 24, RenderTextureFormat.ARGB32);
        cam.targetTexture = rt;
        Directory.CreateDirectory(outDir);
        csv.AppendLine("frame,phase,devMean_mm,devMax_mm,edgeMax,edgeMin,edgesStretched,edgesCompressed,trisCollapsed,penMax_mm,pen3mm,pen10mm,penFrontMax_mm,penFront3mm,penFreeMax_mm,worstEdgeA,worstEdgeB,worstA_restXYZ,worstB_restXYZ,worstA_maxDist_mm,worstB_maxDist_mm,worstA_skinToCloth_mm,worstB_skinToCloth_mm,overLeashN,overLeashWorstExcess_mm,overLeashWorstId,freeClothInsideColliderN,freeSkinInsideColliderN,deepestSkinInsideCollider_mm,deepestClothInsideCollider_mm");
        animator.Play("Idle");
    }

    private void OnDestroy() { Time.captureFramerate = 0; }

    private void Update()
    {
        string next = frame < 4 * 60 ? "idle" : frame < 9 * 60 ? "walk" : "run";
        if (next != phase)
        {
            phase = next;
            animator.CrossFadeInFixedTime(phase == "idle" ? "Idle" : phase == "walk" ? "Walk" : "Run", 0.25f);
        }

        if (freezeFrame >= 0 && frame == freezeFrame) { frozen = true; animator.speed = 0f; }
        if (!inPlace && !frozen)
        {
            if (phase == "walk") transform.position += transform.forward * (walkSpeed / 60f);
            if (phase == "run") transform.position += transform.forward * (runSpeed / 60f);
        }
        StartCoroutine(EndOfFrame(frame));
        frame++;
    }

    private IEnumerator EndOfFrame(int f)
    {
        yield return new WaitForEndOfFrame();
        if (f >= 4 && (f % 2 == 0 || f >= perFrameFrom)) Measure(f);
        if (f == dumpFrame || f == dumpFrame2) Dump(f);
        if (!shoot && !keyFrames.Contains(f)) { }
        else if (keyFrames.Contains(f)) foreach (var v in AllViews) Shoot(v, f);
        else if (seqFrames.Contains(f)) foreach (var v in AllViews) if (System.Array.IndexOf(SeqViews, v.name) >= 0) Shoot(v, f);
        if (f == endFrame)
        {
            File.WriteAllText(Path.Combine(outDir, "metrics.csv"), csv.ToString());
            File.WriteAllText(Path.Combine(outDir, "DONE.txt"), "done " + f);
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#endif
        }
    }

    private void Shoot(View v, int f)
    {
        var renderers = new List<Renderer>();
        if (v.hide != 0)
            foreach (var r in GetComponentsInChildren<Renderer>())
            {
                if (r.gameObject == hauberk.gameObject) continue;
                if (v.hide == 1 && r == leggings) continue;
                if (r.enabled) { renderers.Add(r); r.enabled = false; }
            }

        cam.transform.position = transform.TransformPoint(v.pos);
        cam.transform.LookAt(transform.TransformPoint(v.target));
        cam.fieldOfView = v.fov;
        cam.Render();
        foreach (var r in renderers) r.enabled = true;

        var prev = RenderTexture.active;
        RenderTexture.active = rt;
        var tex = new Texture2D(rt.width, rt.height, TextureFormat.RGB24, false);
        tex.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
        tex.Apply();
        RenderTexture.active = prev;
        string dir = Path.Combine(outDir, v.name);
        Directory.CreateDirectory(dir);
        File.WriteAllBytes(Path.Combine(dir, phase + "_f" + f.ToString("D4") + ".png"), tex.EncodeToPNG());
        Destroy(tex);
    }

    private Vector3 Skinned(int i, Matrix4x4[] bones)
    {
        BoneWeight bw = weights[i];
        Vector3 p = Vector3.zero;
        int[] idx = { bw.boneIndex0, bw.boneIndex1, bw.boneIndex2, bw.boneIndex3 };
        float[] wt = { bw.weight0, bw.weight1, bw.weight2, bw.weight3 };
        for (int q = 0; q < 4; q++) if (wt[q] > 0f) p += (bones[idx[q]] * bind[idx[q]]).MultiplyPoint3x4(meshVerts[i]) * wt[q];
        return p;
    }

    private void Measure(int f)
    {
        var smrBones = hauberk.bones;
        var bm = new Matrix4x4[smrBones.Length];
        for (int i = 0; i < bm.Length; i++) bm[i] = smrBones[i].localToWorldMatrix;
        Matrix4x4 root = hauberk.rootBone.localToWorldMatrix;
        int n = first.Count;
        var skin = new Vector3[n];
        var cur = new Vector3[n];
        Vector3[] cv = clothOn ? cloth.vertices : null;
        for (int p = 0; p < n; p++)
        {
            skin[p] = Skinned(first[p], bm);
            cur[p] = clothOn ? root.MultiplyPoint3x4(cv[p]) : skin[p];
        }

        float devSum = 0f, devMax = 0f; int devN = 0;
        for (int p = 0; p < n; p++) if (free[p]) { float d = (cur[p] - skin[p]).magnitude; devSum += d; devMax = Mathf.Max(devMax, d); devN++; }

        float eMax = 0f, eMin = 9f; int stretched = 0, compressed = 0; int worstA = -1, worstB = -1;
        foreach (var e in edges)
        {
            if (!free[e.a] && !free[e.b]) continue;
            if (e.rest < 0.002f) continue;
            float r = (cur[e.a] - cur[e.b]).magnitude / e.rest;
            if (r > eMax) { worstA = e.a; worstB = e.b; } eMax = Mathf.Max(eMax, r); eMin = Mathf.Min(eMin, r);
            if (r > 1.25f) stretched++;
            if (r < 0.75f) compressed++;
        }

        int collapsed = 0;
        foreach (var t in tris)
        {
            if (!free[t.a] && !free[t.b] && !free[t.c]) continue;
            if (t.rest < 1e-6f) continue;
            float area = Vector3.Cross(cur[t.b] - cur[t.a], cur[t.c] - cur[t.a]).magnitude * 0.5f;
            if (area / t.rest < 0.3f) collapsed++;
        }

        // Clipping against the leather leggings: nearest leggings vertex within 4 cm, signed by that vertex's normal (approximate).
        float penMax = 0f, penFront = 0f, penFree = 0f; int pen3 = 0, pen10 = 0, penF3 = 0;
        if (leggings != null)
        {
            var baked = new Mesh();
            leggings.BakeMesh(baked, true);
            Vector3[] lv = baked.vertices, ln = baked.normals;
            Transform lt = leggings.transform;
            var grid = new Dictionary<long, List<int>>();
            const float cell = 0.04f;
            var lw = new Vector3[lv.Length];
            for (int i = 0; i < lv.Length; i++)
            {
                lw[i] = lt.TransformPoint(lv[i]);
                long key = Key(lw[i], cell);
                if (!grid.TryGetValue(key, out var list)) grid[key] = list = new List<int>();
                list.Add(i);
            }

            Matrix4x4 rootInv = root.inverse;
            for (int p = 0; p < n; p++)
            {
                Vector3 w = cur[p];
                if (w.y > transform.position.y + 1.02f) continue;
                int best = -1; float bestD = cell * cell;
                int cx = Mathf.FloorToInt(w.x / cell), cy = Mathf.FloorToInt(w.y / cell), cz = Mathf.FloorToInt(w.z / cell);
                for (int dx = -1; dx <= 1; dx++) for (int dy = -1; dy <= 1; dy++) for (int dz = -1; dz <= 1; dz++)
                    if (grid.TryGetValue(Key(cx + dx, cy + dy, cz + dz), out var list))
                        foreach (int i in list) { float d2 = (lw[i] - w).sqrMagnitude; if (d2 < bestD) { bestD = d2; best = i; } }
                if (best < 0) continue;
                Vector3 nw = lt.TransformDirection(ln[best]).normalized;
                float depth = -Vector3.Dot(w - lw[best], nw);
                if (depth <= 0f) continue;
                penMax = Mathf.Max(penMax, depth);
                if (depth > 0.003f) pen3++;
                if (depth > 0.010f) pen10++;
                if (free[p]) penFree = Mathf.Max(penFree, depth);
                Vector3 local = transform.InverseTransformPoint(w);
                if (local.z > 0.02f && Mathf.Abs(local.x) < 0.2f) { penFront = Mathf.Max(penFront, depth); if (depth > 0.003f) penF3++; }
            }

            Object.Destroy(baked);
        }

        csv.AppendLine(string.Join(",", f, phase, (devN > 0 ? devSum / devN * 1000f : 0f).ToString("F2"), (devMax * 1000f).ToString("F1"), eMax.ToString("F3"), eMin.ToString("F3"),
            stretched, compressed, collapsed, (penMax * 1000f).ToString("F1"), pen3, pen10, (penFront * 1000f).ToString("F1"), penF3, (penFree * 1000f).ToString("F1"),
            worstA, worstB, worstA < 0 ? "" : V(meshVerts[first[worstA]]), worstB < 0 ? "" : V(meshVerts[first[worstB]]), worstA < 0 ? "" : (cloth.coefficients[worstA].maxDistance * 1000f).ToString("F1"), worstB < 0 ? "" : (cloth.coefficients[worstB].maxDistance * 1000f).ToString("F1"),
            worstA < 0 ? "" : ((cur[worstA] - skin[worstA]).magnitude * 1000f).ToString("F1"), worstB < 0 ? "" : ((cur[worstB] - skin[worstB]).magnitude * 1000f).ToString("F1"),
            ExtraColumns(cur, skin)));
    }


    // ---- investigation helpers ----------------------------------------------------------------------------------------------------------------------
    private struct Sp { public Vector3 a, b; public float ra, rb; }
    private Sp[] Spheres()
    {
        var l = new List<Sp>();
        foreach (var p in cloth.sphereColliders)
        {
            var s2 = p.second != null ? p.second : p.first;     // a pair without a second sphere is a single sphere
            l.Add(new Sp { a = p.first.transform.TransformPoint(p.first.center), b = s2.transform.TransformPoint(s2.center), ra = p.first.radius * p.first.transform.lossyScale.x, rb = s2.radius * s2.transform.lossyScale.x });
        }
        return l.ToArray();
    }
    private static float SdOne(Vector3 p, Sp s) { var ab = s.b - s.a; float u = ab.sqrMagnitude > 1e-12f ? Mathf.Clamp01(Vector3.Dot(p - s.a, ab) / ab.sqrMagnitude) : 0f; return Vector3.Distance(p, s.a + ab * u) - Mathf.Lerp(s.ra, s.rb, u); }
    private static float Sd(Vector3 p, Sp[] ss) { float b = float.MaxValue; foreach (var s in ss) b = Mathf.Min(b, SdOne(p, s)); return b; }

    private string ExtraColumns(Vector3[] cur, Vector3[] skin)
    {
        var co = cloth.coefficients; var ss = Spheres();
        int over = 0, clothIn = 0, skinIn = 0; float worstEx = 0f, deepSkin = 0f, deepCloth = 0f; int worstId = -1;
        for (int p = 0; p < cur.Length; p++)
        {
            if (!free[p]) continue;
            float ex = (cur[p] - skin[p]).magnitude - co[p].maxDistance;
            if (ex > 0.02f) over++;
            if (ex > worstEx) { worstEx = ex; worstId = p; }
            float sc = Sd(cur[p], ss), sk = Sd(skin[p], ss);
            if (sc < -0.005f) clothIn++;
            if (sk < -0.005f) skinIn++;
            deepCloth = Mathf.Max(deepCloth, -sc); deepSkin = Mathf.Max(deepSkin, -sk);
        }
        return string.Join(",", over, (worstEx * 1000f).ToString("F1"), worstId, clothIn, skinIn, (deepSkin * 1000f).ToString("F1"), (deepCloth * 1000f).ToString("F1"));
    }

    // Per-particle comparison of leggings surface, colliders, skinned and simulated positions at one frame.
    private void Dump(int f)
    {
        var smrBones = hauberk.bones; var bm = new Matrix4x4[smrBones.Length];
        for (int i = 0; i < bm.Length; i++) bm[i] = smrBones[i].localToWorldMatrix;
        Matrix4x4 root = hauberk.rootBone.localToWorldMatrix; var cv = cloth.vertices; var co = cloth.coefficients; var ss = Spheres();
        var baked = new Mesh(); leggings.BakeMesh(baked, true); var lv = baked.vertices; var ln = baked.normals; var lt = leggings.transform;
        var lw = new List<Vector3>(); var lnw = new List<Vector3>();
        for (int i = 0; i < lv.Length; i += 2) { lw.Add(lt.TransformPoint(lv[i])); lnw.Add(lt.TransformDirection(ln[i]).normalized); }
        float Depth(Vector3 p) { float bd = float.MaxValue; int bi = -1; for (int i = 0; i < lw.Count; i++) { float d = (lw[i] - p).sqrMagnitude; if (d < bd) { bd = d; bi = i; } } return bi < 0 || bd > 0.06f * 0.06f ? float.NaN : -Vector3.Dot(p - lw[bi], lnw[bi]); }
        var sb = new StringBuilder("id,restX,restY,restZ,maxDist_mm,skinToCloth_mm,excessOverLeash_mm,skinSd_mm,clothSd_mm,skinLeggingsDepth_mm,clothLeggingsDepth_mm,clothWorldX,clothWorldY,clothWorldZ,skinWorldX,skinWorldY,skinWorldZ\n");
        int n = first.Count; var cur = new Vector3[n]; var sk = new Vector3[n];
        for (int p = 0; p < n; p++)
        {
            sk[p] = Skinned(first[p], bm); cur[p] = root.MultiplyPoint3x4(cv[p]);
            if (!free[p]) continue;
            float dev = (cur[p] - sk[p]).magnitude;
            sb.AppendLine(string.Join(",", p, meshVerts[first[p]].x.ToString("F3"), meshVerts[first[p]].y.ToString("F3"), meshVerts[first[p]].z.ToString("F3"), (co[p].maxDistance * 1000f).ToString("F1"), (dev * 1000f).ToString("F1"), ((dev - co[p].maxDistance) * 1000f).ToString("F1"),
                (Sd(sk[p], ss) * 1000f).ToString("F1"), (Sd(cur[p], ss) * 1000f).ToString("F1"), (Depth(sk[p]) * 1000f).ToString("F1"), (Depth(cur[p]) * 1000f).ToString("F1"),
                cur[p].x.ToString("F3"), cur[p].y.ToString("F3"), cur[p].z.ToString("F3"), sk[p].x.ToString("F3"), sk[p].y.ToString("F3"), sk[p].z.ToString("F3")));
        }
        File.WriteAllText(Path.Combine(outDir, "dump_f" + f + ".csv"), sb.ToString());

        // leggings surface against the colliders in this pose: signed distance of leggings vertices to the collider union, for vertices within 15 cm of a collider
        var sds = new List<float>();
        foreach (var w in lw) { float s = Sd(w, ss); if (s > 0.15f) continue; sds.Add(s); }
        sds.Sort();
        var rep = new StringBuilder();
        rep.AppendLine("frame " + f + " phase " + phase + " frozen " + frozen);
        if (sds.Count > 0) rep.AppendLine("leggings vertices within 15 cm of a collider: " + sds.Count + "; signed distance to collider surface (mm, negative = leggings vertex inside the collider): p5 " + (sds[sds.Count / 20] * 1000f).ToString("F1") + ", p25 " + (sds[sds.Count / 4] * 1000f).ToString("F1") + ", median " + (sds[sds.Count / 2] * 1000f).ToString("F1") + ", p75 " + (sds[sds.Count * 3 / 4] * 1000f).ToString("F1") + ", p95 " + (sds[sds.Count * 19 / 20] * 1000f).ToString("F1"));
        for (int i = 0; i < ss.Length; i++) rep.AppendLine("pair " + i + ": a " + ss[i].a.ToString("F3") + " r " + (ss[i].ra * 1000f).ToString("F1") + " mm, b " + ss[i].b.ToString("F3") + " r " + (ss[i].rb * 1000f).ToString("F1") + " mm");
        File.WriteAllText(Path.Combine(outDir, "dump_f" + f + "_colliders.txt"), rep.ToString());
        Object.Destroy(baked);
    }

    private static string V(Vector3 v) => v.x.ToString("F3") + "|" + v.y.ToString("F3") + "|" + v.z.ToString("F3");
    private static long Key(Vector3 p, float cell) => Key(Mathf.FloorToInt(p.x / cell), Mathf.FloorToInt(p.y / cell), Mathf.FloorToInt(p.z / cell));
    private static long Key(int x, int y, int z) => ((long)(x + 4096) << 40) | ((long)(y + 4096) << 20) | (long)(z + 4096);
}
