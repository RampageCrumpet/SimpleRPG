using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;

// Diagnostic-only validator for Unity's native Cloth (Assets/ClothFitTest, not part of the game). Attach it (Tools > Cloth Validator > Attach To Selected, or AddComponent) to the character
// that owns the Cloth + SkinnedMeshRenderer, enter Play Mode, and it samples every frame in [startFrame, endFrame], then writes report.md and CSVs to outDir. It changes nothing.
// Frame index: FitHarness.CurrentFrame when a FitHarness is on the same object (so frame 784 means the same frame as in the harness runs), else its own counter.
// Checks: 1 particle penetration, 2 triangle penetration, 3 collider coverage vs visible equipment, 4 update synchronization, 5 self-collision / jitter / constraint violations, 6 constraint conflicts.
// Conventions: particle id = index of the unique welded position in first-occurrence order (Unity's Cloth particle order, same as FitHarness); triangle id = index into mesh.triangles / 3.
public class ClothValidator : MonoBehaviour
{
    [Header("Target (auto-filled)")]
    public Cloth cloth;
    public SkinnedMeshRenderer garment;
    public SkinnedMeshRenderer[] equipment;   // rendered equipment the cloth is compared with (default: every other enabled SkinnedMeshRenderer on the character)

    [Header("Run")]
    public int startFrame = 0;
    public int endFrame = 600;
    public int[] detailFrames = new int[0];   // frames that also get a per-id csv
    public string outDir = "";
    public bool quitPlayAtEnd = true;
    public int pauseAtFrame = -1;             // pauses the editor at the end of this frame (for scene-view captures of the gizmos)
    public bool drawGizmos = true;
    [Header("Collider-fit data export (optional)")]
    public int[] fitDumpFrames = new int[0];   // frames at which the animated equipment surface and the pinned garment particles are written, in bone-local axes, to fit_data.csv
    public string fitBoneKey = "UpperLeg";
    public string fitEquipKey = "Leggings";   // name fragment of the equipment renderer whose surface is dumped (fit_data.csv, coverage_f*.csv)
    public int[] meshCheckIds = new int[0];   // when not empty, only these pinned particle ids are tested
    public bool meshCheckNearThigh = false;   // only test pinned particles within 0.3 m of an upper-leg bone (fast enough for every frame of a run)
    public int[] meshCheckFrames = new int[0];   // frames at which every pinned particle is tested against the full rendered *Leggings* mesh (distance, signed distance, ray parity); written to mesh_check_f<frame>.csv
    public int fitMaxPointsPerLeg = 1500;
    public int[] coverageFrames = new int[0];   // frames at which the collider shapes and every vertex of the *Leggings* mesh (with its dominant bone) are written in world space to coverage_f<frame>.csv
    public bool clipLog = false;                // writes every clipping particle of every frame (world position, depth, distance to the nearest collider) to clip_particles.csv

    [Header("Thresholds (metres)")]
    public float penTol = 0.001f;      // particle / triangle counts as inside a collider beyond this depth
    public float triTol = 0.002f;      // triangle interior counts as inside a collider beyond this depth
    public float equipTol = 0.003f;    // cloth counts as clipping equipment beyond this depth
    public float leashTol = 0.005f;    // particle counts as beyond its Max Distance beyond this
    public float jitterTol = 0.004f;   // second difference of (cloth - skinned) above this counts as a jitter event

    struct Shape { public Vector3 a, b; public float ra, rb; public string name; }

    class FrameStat
    {
        public int frame, steps, bestLag, pIn, pInDeepId, triOnlyN, triOnlyDeepId, triVertN, clipP, clipT, clipDeepId, overLeash, overLeashId, edgeA, edgeB, stretched, over2x, collapsed, a2maxId, rev, skinIn, infLeash, infTether, core, divEdges, pinnedIn;
        public float pen0, penBest, pInDeep, triOnlyDeep, triVertDeep, clipDeep, overLeashMax, edgeMax, a2p95, a2max, pinnedErr, colliderMove, timeLag, eqGapP50, eqGapP90, eqGapMax, sampleMs;
        public float hemRise;   // mean over free particles of how far the simulated particle sits above its skinned position (mm, never negative): a skirt riding up
        public bool jitterValid; public float stateLag, pinnedRms0, pinnedRmsM, a2p95m, a2maxm, penM, pInDeepM, overLeashMaxM; public int pInM, overLeashM, tetherSkin; public float a2p50m, ringEdgeMax;
    }

    class TriAcc { public int frames, shape; public float maxDepth; public bool vertexAssisted; }

    // ---- static data ---------------------------------------------------------------------------------------------------------------------------------
    Animator animator;
    FitHarness harness;
    Mesh mesh;
    Vector3[] meshVerts; BoneWeight[] weights; Matrix4x4[] bind;
    List<int> first = new List<int>();
    int n; bool mappingOk;
    int[] triIdx;            // particle ids, 3 per triangle
    int[] triVertIdx;        // mesh vertex indices
    List<(int a, int b, float rest)> edges = new List<(int, int, float)>();
    List<int>[] adj;
    float[] maxD, csd, pathLen; int[] anchor; bool[] free;
    int freeCount;
    readonly List<string> notes = new List<string>();
    string selfInfo = "";

    // ---- accumulators --------------------------------------------------------------------------------------------------------------------------------
    readonly List<FrameStat> stats = new List<FrameStat>();
    int[] inFrames, leashFrames, revFrames, jitFrames, confLeash, confTether, confCore, clipFrames;
    float[] inMax, leashMax, a2sum, confDeficit, clipMax;
    readonly Dictionary<int, TriAcc> triOnly = new Dictionary<int, TriAcc>(), triVert = new Dictionary<int, TriAcc>(), triClip = new Dictionary<int, TriAcc>();
    readonly Dictionary<string, int[]> shapeIn = new Dictionary<string, int[]>();          // shape name -> {frames with a particle inside, deepest mm * 10}
    readonly Dictionary<string, List<float>> eqGapByShape = new Dictionary<string, List<float>>();
    readonly Dictionary<string, int> clipByEquip = new Dictionary<string, int>();
    readonly Dictionary<int, List<string>> detail = new Dictionary<int, List<string>>();
    float[] needMax, overReachMax; int[] needFrame, needFrames; string[] needShape; Vector3[] needDir;   // per free particle: exit clearance needed from the skinned pose over the sampled frames
    readonly List<Shape[]> hist = new List<Shape[]>(); readonly List<Vector3[]> skinHist = new List<Vector3[]>(), localSkinHist = new List<Vector3[]>(); readonly List<Matrix4x4> rootHist = new List<Matrix4x4>();
    Vector3[] devMPrev1, devMPrev2;
    Vector3[] devPrev1, devPrev2; int devPrev1Frame = -10, devPrev2Frame = -10;
    int fixedCount, ownFrame, lastSampled = -1; bool finished, started; float spaceErrMax; string shapesInfo = "";
    int sampledFrames;

    // viz (last sampled frame)
    readonly List<Vector3> vizIn = new List<Vector3>(), vizClip = new List<Vector3>(), vizLeashA = new List<Vector3>(), vizLeashB = new List<Vector3>(), vizTri = new List<Vector3>();
    Shape[] vizShapes = new Shape[0];

    // ==============================================================================================================================================================
    void Start()
    {
        harness = GetComponent<FitHarness>();
        animator = GetComponentInChildren<Animator>(true);
        if (!cloth) cloth = GetComponentInChildren<Cloth>(true);
        if (!cloth) { Debug.LogError("ClothValidator: no Cloth found"); enabled = false; return; }
        garment = cloth.GetComponent<SkinnedMeshRenderer>();
        if (equipment == null || equipment.Length == 0)
        {
            var l = new List<SkinnedMeshRenderer>();
            foreach (var s in GetComponentsInChildren<SkinnedMeshRenderer>(false)) if (s != garment && s.enabled && s.sharedMesh != null) l.Add(s);
            equipment = l.ToArray();
        }
        if (string.IsNullOrEmpty(outDir)) outDir = Path.Combine("ClothValidation", gameObject.scene.name + "_" + garment.name);
        Directory.CreateDirectory(outDir);
        Init();
        started = true;
    }

    void Init()
    {
        mesh = garment.sharedMesh; meshVerts = mesh.vertices; weights = mesh.boneWeights; bind = mesh.bindposes;
        var seen = new Dictionary<(int, int, int), int>(); var map = new int[meshVerts.Length];
        for (int i = 0; i < meshVerts.Length; i++)
        {
            var k = (Mathf.RoundToInt(meshVerts[i].x * 10000f), Mathf.RoundToInt(meshVerts[i].y * 10000f), Mathf.RoundToInt(meshVerts[i].z * 10000f));
            if (!seen.TryGetValue(k, out int p)) { p = first.Count; seen[k] = p; first.Add(i); }
            map[i] = p;
        }
        n = first.Count;
        var co = cloth.coefficients;
        mappingOk = co.Length == n && cloth.vertices.Length == n;
        if (!mappingOk) notes.Add($"Particle mapping NOT verified: {n} unique welded positions vs {co.Length} coefficients / {cloth.vertices.Length} cloth vertices. Every per-particle result below is unreliable.");
        maxD = new float[n]; csd = new float[n]; free = new bool[n];
        for (int p = 0; p < n && p < co.Length; p++) { maxD[p] = co[p].maxDistance; csd[p] = co[p].collisionSphereDistance; free[p] = co[p].maxDistance > 0f; if (free[p]) freeCount++; }

        int[] t = mesh.triangles; triVertIdx = t; triIdx = new int[t.Length];
        for (int i = 0; i < t.Length; i++) triIdx[i] = map[t[i]];
        adj = new List<int>[n]; for (int i = 0; i < n; i++) adj[i] = new List<int>();
        var es = new HashSet<long>();
        for (int i = 0; i < t.Length; i += 3)
            for (int q = 0; q < 3; q++)
            {
                int a = triIdx[i + q], b = triIdx[i + (q + 1) % 3]; if (a == b) continue;
                long key = a < b ? (long)a * 100000 + b : (long)b * 100000 + a;
                if (es.Add(key)) { edges.Add((a, b, (meshVerts[first[a]] - meshVerts[first[b]]).magnitude)); adj[a].Add(b); adj[b].Add(a); }
            }

        // surface path length (rest edge lengths) from each free particle to the nearest pinned one: the farthest a tether can let it travel from that anchor
        pathLen = new float[n]; anchor = new int[n];
        var open = new SortedSet<(float, int)>();
        for (int p = 0; p < n; p++) { pathLen[p] = free[p] ? float.MaxValue : 0f; anchor[p] = free[p] ? -1 : p; if (!free[p]) open.Add((0f, p)); }
        while (open.Count > 0)
        {
            var (d, i) = open.Min; open.Remove(open.Min);
            if (d > pathLen[i]) continue;
            foreach (int k in adj[i]) { float nd = d + (meshVerts[first[i]] - meshVerts[first[k]]).magnitude; if (nd < pathLen[k]) { pathLen[k] = nd; anchor[k] = anchor[i]; open.Add((nd, k)); } }
        }

        inFrames = new int[n]; leashFrames = new int[n]; revFrames = new int[n]; jitFrames = new int[n]; confLeash = new int[n]; confTether = new int[n]; confCore = new int[n]; clipFrames = new int[n];
        inMax = new float[n]; leashMax = new float[n]; a2sum = new float[n]; confDeficit = new float[n]; clipMax = new float[n]; needMax = new float[n]; needFrame = new int[n]; needShape = new string[n]; needFrames = new int[n]; overReachMax = new float[n]; needDir = new Vector3[n];
        devPrev1 = new Vector3[n]; devPrev2 = new Vector3[n]; devMPrev1 = new Vector3[n]; devMPrev2 = new Vector3[n];
        SelfCollisionStatic();
        var ss = GetShapes(); var sb = new StringBuilder();
        foreach (var s in ss) sb.AppendLine($"  - {s.name}: a {V(s.a)} r {s.ra * 1000f:F1} mm, b {V(s.b)} r {s.rb * 1000f:F1} mm");
        shapesInfo = sb.ToString();
    }

    // ---- per-frame hooks -----------------------------------------------------------------------------------------------------------------------------
    void FixedUpdate() { fixedCount++; }

    void LateUpdate()
    {
        if (!started || finished) return;
        int fi = harness ? harness.CurrentFrame : ownFrame++;
        var cur = GetShapes();
        hist.Insert(0, cur); if (hist.Count > 5) hist.RemoveAt(5);
        var sk0 = CpuSkin(); skinHist.Insert(0, sk0); if (skinHist.Count > 5) skinHist.RemoveAt(5);
        var rm0 = garment.rootBone.localToWorldMatrix; var inv0 = rm0.inverse; var loc0 = new Vector3[n]; for (int p = 0; p < n; p++) loc0[p] = inv0.MultiplyPoint3x4(sk0[p]);
        localSkinHist.Insert(0, loc0); if (localSkinHist.Count > 5) localSkinHist.RemoveAt(5);
        rootHist.Insert(0, rm0); if (rootHist.Count > 5) rootHist.RemoveAt(5);
        int steps = fixedCount; fixedCount = 0;
        if (fi >= startFrame && fi <= endFrame && (cloth.enabled)) StartCoroutine(Sample(fi, steps));
        if (fi > endFrame + 1) Finish();
    }

    void OnDisable() { if (started && !finished) Finish(); }

    IEnumerator Sample(int fi, int steps)
    {
        yield return new WaitForEndOfFrame();
        if (finished) yield break;
        var sw = System.Diagnostics.Stopwatch.StartNew();
        Evaluate(fi, steps);
        stats[stats.Count - 1].sampleMs = sw.ElapsedMilliseconds;
        sampledFrames++;
#if UNITY_EDITOR
        if (fi == pauseAtFrame) UnityEditor.EditorApplication.isPaused = true;
#endif
        if (fi >= endFrame) Finish();
    }

    // ==============================================================================================================================================================
    void Evaluate(int fi, int steps)
    {
        var fs = new FrameStat { frame = fi, steps = steps }; stats.Add(fs);
        bool wantDetail = Array.IndexOf(detailFrames, fi) >= 0; List<string> det = null;
        if (wantDetail) { det = new List<string>(); detail[fi] = det; }

        var smrBones = garment.bones; var bm = new Matrix4x4[smrBones.Length];
        for (int i = 0; i < bm.Length; i++) bm[i] = smrBones[i].localToWorldMatrix;
        Matrix4x4 root = garment.rootBone.localToWorldMatrix;
        var cv = cloth.vertices; var skin = skinHist.Count > 0 ? skinHist[0] : CpuSkin(); var cur = new Vector3[n];
        for (int p = 0; p < n; p++) cur[p] = root.MultiplyPoint3x4(cv[p]);
        var shapes = hist.Count > 0 ? hist[0] : GetShapes();
        vizShapes = shapes; vizIn.Clear(); vizClip.Clear(); vizLeashA.Clear(); vizLeashB.Clear(); vizTri.Clear();

        // space / mapping self-check: pinned particles must sit exactly on their skinned position
        float pe = 0f; int pc = 0; for (int p = 0; p < n; p++) if (!free[p]) { pe = Mathf.Max(pe, (cur[p] - skin[p]).magnitude); pc++; }
        fs.pinnedErr = pe * 1000f; spaceErrMax = Mathf.Max(spaceErrMax, pe);

        if (Array.IndexOf(fitDumpFrames, fi) >= 0) DumpFit(fi, skin);
        if (Array.IndexOf(meshCheckFrames, fi) >= 0) MeshCheck(fi, skin, cur);
        if (Array.IndexOf(coverageFrames, fi) >= 0) CoverageDump(fi, shapes);

        // ---- 1. particle penetration ------------------------------------------------------------------------------------------------------------------
        var sdP = new float[n]; var shP = new int[n];
        for (int p = 0; p < n; p++)
        {
            sdP[p] = SdAll(cur[p], shapes, out shP[p]);
            if (sdP[p] < -penTol)
            {
                float d = -sdP[p];
                if (!free[p]) { fs.pinnedIn++; continue; }
                fs.pIn++; inFrames[p]++; inMax[p] = Mathf.Max(inMax[p], d); vizIn.Add(cur[p]);
                if (d * 1000f > fs.pInDeep) { fs.pInDeep = d * 1000f; fs.pInDeepId = p; }
                string nm = shapes[shP[p]].name; if (!shapeIn.TryGetValue(nm, out var sa)) shapeIn[nm] = sa = new int[2];
                sa[0]++; sa[1] = Mathf.Max(sa[1], Mathf.RoundToInt(d * 10000f));
                det?.Add($"particle_inside,{p},{R(p)},{maxD[p] * 1000f:F1},{d * 1000f:F2},{nm},");
            }
        }

        // ---- 4a. time offset of the cloth read-back: the lag (in rendered frames, fractional) at which the pinned particles best match the animated skin ---------
        float bestLam = 0f, bestErr = float.MaxValue, err0 = 0f; int pcount = 0; for (int p = 0; p < n; p++) if (!free[p]) pcount++;
        for (int k = 0; k <= 16; k++)
        {
            float lam = k * 0.25f; if (lam > skinHist.Count - 1) break;
            var sl = LerpSkin(localSkinHist, lam); float e = 0f; for (int p = 0; p < n; p++) if (!free[p]) e += (cv[p] - sl[p]).sqrMagnitude;
            if (k == 0) err0 = e; if (e < bestErr) { bestErr = e; bestLam = lam; }
        }
        fs.stateLag = bestLam; fs.pinnedRms0 = pcount > 0 ? Mathf.Sqrt(err0 / pcount) * 1000f : 0f; fs.pinnedRmsM = pcount > 0 ? Mathf.Sqrt(bestErr / pcount) * 1000f : 0f;
        var localM = LerpSkin(localSkinHist, bestLam); var skinM = LerpSkin(skinHist, bestLam); var shapesM = LerpShapes(hist, bestLam);
        var rootM = LerpMat(rootHist, bestLam); var curM = new Vector3[n]; for (int p = 0; p < n; p++) curM[p] = rootM.MultiplyPoint3x4(cv[p]);   // where the solver thought the particles were, in the world of that moment
        for (int p = 0; p < n; p++)
        {
            if (!free[p]) continue;
            float s = SdAll(curM[p], shapesM, out _); if (s < -penTol) { fs.pInM++; fs.pInDeepM = Mathf.Max(fs.pInDeepM, -s * 1000f); }
            float ex = (curM[p] - skinM[p]).magnitude - maxD[p]; if (ex > leashTol) { fs.overLeashM++; fs.overLeashMaxM = Mathf.Max(fs.overLeashMaxM, ex * 1000f); }
        }
        if (hist.Count > 1 && hist[1].Length == hist[0].Length)
        {
            float mv = 0f; for (int i = 0; i < hist[0].Length; i++) { mv = Mathf.Max(mv, (hist[0][i].a - hist[1][i].a).magnitude, (hist[0][i].b - hist[1][i].b).magnitude); }
            fs.colliderMove = mv * 1000f;
        }
        fs.timeLag = (Time.time - Time.fixedTime) * 1000f;

        // ---- 2. triangle penetration (+ equipment sampling shares the sample points) ------------------------------------------------------------------
        var eq = BuildEquipmentGrid(out int eqVerts);
        int triCount = triIdx.Length / 3; var clipTriThisFrame = new HashSet<int>();
        var sampleBuf = new List<Vector3>(256);
        for (int t = 0; t < triCount; t++)
        {
            int a = triIdx[t * 3], b = triIdx[t * 3 + 1], c = triIdx[t * 3 + 2];
            bool anyFree = free[a] || free[b] || free[c];
            if (!anyFree) continue;
            Vector3 A = cur[a], B = cur[b], C = cur[c];
            bool vertIn = sdP[a] < -penTol || sdP[b] < -penTol || sdP[c] < -penTol;
            float maxEdge = Mathf.Max((A - B).magnitude, (B - C).magnitude, (C - A).magnitude);
            if (maxEdge < 1e-6f) continue;
            int nsub = Mathf.Clamp(Mathf.CeilToInt(maxEdge / 0.01f), 1, 14);
            float deep = 0f; int deepShape = 0; float eqDeep = 0f; int eqRend = -1;
            for (int i = 0; i <= nsub; i++)
                for (int j = 0; j <= nsub - i; j++)
                {
                    if ((i == 0 && j == 0) || (i == nsub && j == 0) || (i == 0 && j == nsub)) continue;   // the three corners are the particles, already tested
                    float u = (float)i / nsub, v = (float)j / nsub;
                    Vector3 P = A * (1f - u - v) + B * u + C * v;
                    float s = SdAll(P, shapes, out int sh);
                    if (-s > deep) { deep = -s; deepShape = sh; }
                    if (eq != null && s >= -penTol) { float ed = eq.Depth(P, out int er); if (ed > eqDeep) { eqDeep = ed; eqRend = er; } }
                }
            if (deep > triTol)
            {
                var dict = vertIn ? triVert : triOnly;
                if (!dict.TryGetValue(t, out var acc)) dict[t] = acc = new TriAcc { vertexAssisted = vertIn };
                acc.frames++; acc.maxDepth = Mathf.Max(acc.maxDepth, deep); acc.shape = deepShape;
                if (vertIn) { fs.triVertN++; fs.triVertDeep = Mathf.Max(fs.triVertDeep, deep * 1000f); }
                else
                {
                    fs.triOnlyN++; if (deep * 1000f > fs.triOnlyDeep) { fs.triOnlyDeep = deep * 1000f; fs.triOnlyDeepId = t; }
                    vizTri.Add(A); vizTri.Add(B); vizTri.Add(C);
                }
                det?.Add($"{(vertIn ? "triangle_vertex_assisted" : "triangle_only")},{t},{R(a)}|{R(b)}|{R(c)},{maxEdge * 1000f:F1},{deep * 1000f:F2},{shapes[deepShape].name},verts {a}/{b}/{c}");
            }
            if (eqDeep > equipTol)
            {
                if (!triClip.TryGetValue(t, out var acc)) triClip[t] = acc = new TriAcc();
                acc.frames++; acc.maxDepth = Mathf.Max(acc.maxDepth, eqDeep); clipTriThisFrame.Add(t);
                string en = equipment[eqRend].name; clipByEquip.TryGetValue(en, out int cnt); clipByEquip[en] = cnt + 1;
                det?.Add($"equip_clip_triangle,{t},{R(a)}|{R(b)}|{R(c)},{maxEdge * 1000f:F1},{eqDeep * 1000f:F2},{en},clear of colliders");
            }
        }
        fs.clipT = clipTriThisFrame.Count;

        // ---- 3. coverage: free particles clear of the colliders but inside visible equipment; collider-vs-equipment gap near the garment ---------------
        if (eq != null)
        {
            for (int p = 0; p < n; p++)
            {
                if (!free[p] || sdP[p] < -penTol) continue;
                float ed = eq.Depth(cur[p], out int er);
                if (ed > equipTol)
                {
                    fs.clipP++; clipFrames[p]++; clipMax[p] = Mathf.Max(clipMax[p], ed); vizClip.Add(cur[p]);
                    if (clipLog) clipCsv.AppendLine($"{fi},{p},{cur[p].x:F4},{cur[p].y:F4},{cur[p].z:F4},{ed * 1000f:F2},{sdP[p] * 1000f:F1},{shapes[shP[p]].name}");
                    if (ed * 1000f > fs.clipDeep) { fs.clipDeep = ed * 1000f; fs.clipDeepId = p; }
                    string en = equipment[er].name; clipByEquip.TryGetValue(en, out int cnt); clipByEquip[en] = cnt + 1;
                    det?.Add($"equip_clip_particle,{p},{R(p)},{maxD[p] * 1000f:F1},{ed * 1000f:F2},{en},collider sd +{sdP[p] * 1000f:F1} mm ({shapes[shP[p]].name})");
                }
            }
            // gap: equipment vertices within 6 cm of a free cloth particle; signed distance to the collider union (positive = equipment surface outside every collider)
            var pgrid = new Dictionary<long, List<int>>();
            for (int p = 0; p < n; p++) if (free[p]) { long k = Key(cur[p], 0.06f); if (!pgrid.TryGetValue(k, out var l)) pgrid[k] = l = new List<int>(); l.Add(p); }
            var gaps = new List<float>();
            for (int i = 0; i < eq.pos.Count; i++)
            {
                Vector3 q = eq.pos[i]; bool near = false;
                int cx = Mathf.FloorToInt(q.x / 0.06f), cy = Mathf.FloorToInt(q.y / 0.06f), cz = Mathf.FloorToInt(q.z / 0.06f);
                for (int dx = -1; dx <= 1 && !near; dx++) for (int dy = -1; dy <= 1 && !near; dy++) for (int dz = -1; dz <= 1 && !near; dz++)
                    if (pgrid.TryGetValue(Key(cx + dx, cy + dy, cz + dz), out var l)) foreach (int p in l) if ((cur[p] - q).sqrMagnitude < 0.06f * 0.06f) { near = true; break; }
                if (!near || shapes.Length == 0) continue;   // no colliders: nothing to measure a gap to
                float g = SdAll(q, shapes, out int sh);
                gaps.Add(g);
                string nm = shapes[sh].name; if (!eqGapByShape.TryGetValue(nm, out var gl)) eqGapByShape[nm] = gl = new List<float>();
                gl.Add(g);
            }
            if (gaps.Count > 0) { gaps.Sort(); fs.eqGapP50 = gaps[gaps.Count / 2] * 1000f; fs.eqGapP90 = gaps[gaps.Count * 9 / 10] * 1000f; fs.eqGapMax = gaps[gaps.Count - 1] * 1000f; }
        }

        // ---- 5. leash, edge strain, collapsed triangles, jitter -----------------------------------------------------------------------------------------
        var dev = new Vector3[n];
        for (int p = 0; p < n; p++)
        {
            dev[p] = cv[p] - localSkinHist[0][p];   // root-relative: independent of the character's world motion
            if (!free[p]) continue;
            fs.hemRise += Mathf.Max(0f, cur[p].y - skin[p].y) * 1000f / Mathf.Max(1, freeCount);
            float ex = (cur[p] - skin[p]).magnitude - maxD[p];
            if (ex > leashTol)
            {
                fs.overLeash++; leashFrames[p]++; leashMax[p] = Mathf.Max(leashMax[p], ex);
                if (ex * 1000f > fs.overLeashMax) { fs.overLeashMax = ex * 1000f; fs.overLeashId = p; }
                vizLeashA.Add(skin[p]); vizLeashB.Add(cur[p]);
                det?.Add($"over_leash,{p},{R(p)},{maxD[p] * 1000f:F1},{ex * 1000f:F2},,skin-to-cloth {dev[p].magnitude * 1000f:F1} mm");
            }
        }
        foreach (var e in edges)
        {
            if (!free[e.a] && !free[e.b]) continue;
            if (e.rest < 0.002f) continue;
            float r = (cur[e.a] - cur[e.b]).magnitude / e.rest;
            if (r > fs.edgeMax) { fs.edgeMax = r; fs.edgeA = e.a; fs.edgeB = e.b; }
            if (free[e.a] != free[e.b]) fs.ringEdgeMax = Mathf.Max(fs.ringEdgeMax, r);   // edges between a pinned and a free particle (waist ring)
            if (r > 1.25f) fs.stretched++;
            if (r > 2f) fs.over2x++;
        }
        for (int t = 0; t < triCount; t++)
        {
            int a = triIdx[t * 3], b = triIdx[t * 3 + 1], c = triIdx[t * 3 + 2];
            if (!free[a] && !free[b] && !free[c]) continue;
            float restArea = Vector3.Cross(meshVerts[first[b]] - meshVerts[first[a]], meshVerts[first[c]] - meshVerts[first[a]]).magnitude * 0.5f;
            if (restArea < 1e-6f) continue;
            if (Vector3.Cross(cur[b] - cur[a], cur[c] - cur[a]).magnitude * 0.5f / restArea < 0.3f) fs.collapsed++;
        }
        if (devPrev1Frame == fi - 1 && devPrev2Frame == fi - 2)
        {
            fs.jitterValid = true; var list = new List<float>();
            for (int p = 0; p < n; p++)
            {
                if (!free[p]) continue;
                Vector3 a2 = dev[p] - 2f * devPrev1[p] + devPrev2[p]; float m = a2.magnitude; list.Add(m);
                a2sum[p] += m * m;
                if (m > jitterTol) { jitFrames[p]++; }
                if (m > fs.a2max) { fs.a2max = m; fs.a2maxId = p; }
                Vector3 d1 = dev[p] - devPrev1[p], d0 = devPrev1[p] - devPrev2[p];
                if (d1.magnitude > 0.002f && d0.magnitude > 0.002f && Vector3.Dot(d1, d0) < 0f) { fs.rev++; revFrames[p]++; }
            }
            list.Sort(); fs.a2p95 = list.Count > 0 ? list[list.Count * 95 / 100] : 0f;
            fs.a2max *= 1000f; fs.a2p95 *= 1000f;
            var lm = new List<float>();
            for (int p = 0; p < n; p++) { if (!free[p]) continue; Vector3 dm = cv[p] - localM[p]; lm.Add((dm - 2f * devMPrev1[p] + devMPrev2[p]).magnitude); }
            lm.Sort(); fs.a2p50m = lm.Count > 0 ? lm[lm.Count / 2] * 1000f : 0f; fs.a2p95m = lm.Count > 0 ? lm[lm.Count * 95 / 100] * 1000f : 0f; fs.a2maxm = lm.Count > 0 ? lm[lm.Count - 1] * 1000f : 0f;
        }
        var devM = new Vector3[n]; for (int p = 0; p < n; p++) devM[p] = cv[p] - localM[p];
        devMPrev2 = devMPrev1; devMPrev1 = devM;
        devPrev2 = devPrev1; devPrev1 = dev; devPrev2Frame = devPrev1Frame; devPrev1Frame = fi;

        // ---- 6. constraint conflicts ---------------------------------------------------------------------------------------------------------------------
        var skinSd = new float[n]; var skinNormal = new Vector3[n]; var skinShape = new int[n]; var inSkin = new bool[n];
        for (int p = 0; p < n; p++)
        {
            skinSd[p] = SdAll(skin[p], shapes, out skinShape[p]);
            if (skinSd[p] >= -penTol) continue;
            inSkin[p] = true;
            if (!free[p]) { det?.Add($"conflict_pinned_inside_collider,{p},{R(p)},0.0,{-skinSd[p] * 1000f:F2},{shapes[skinShape[p]].name},pinned skinned position inside a collider: cannot be satisfied"); continue; }
            fs.skinIn++;
            ClosestOnAxis(skin[p], shapes[skinShape[p]], out Vector3 q, out float rad);
            Vector3 dir = skin[p] - q; float axisDist = dir.magnitude;
            skinNormal[p] = axisDist > 1e-4f ? dir / axisDist : Vector3.zero;
            float need = -skinSd[p] + csd[p];
            needFrames[p]++; if (need > needMax[p]) { needMax[p] = need; needFrame[p] = fi; needShape[p] = shapes[skinShape[p]].name; needDir[p] = skinNormal[p]; }
            float deficit = need - maxD[p];
            bool leashBad = deficit > 0.0005f;
            bool tetherBad = false;
            if (cloth.useTethers && anchor[p] >= 0 && skinNormal[p] != Vector3.zero)
            {
                Vector3 exit = skin[p] + skinNormal[p] * need;
                tetherBad = (exit - skin[anchor[p]]).magnitude > pathLen[p] + 0.001f;
                overReachMax[p] = Mathf.Max(overReachMax[p], (exit - skin[anchor[p]]).magnitude - pathLen[p]);
                if ((skin[p] - skin[anchor[p]]).magnitude > pathLen[p] + 0.001f) fs.tetherSkin++;
            }
            bool core = axisDist < 0.5f * rad;
            if (leashBad) { fs.infLeash++; confLeash[p]++; confDeficit[p] = Mathf.Max(confDeficit[p], deficit); }
            if (tetherBad) { fs.infTether++; confTether[p]++; }
            if (core) { fs.core++; confCore[p]++; }
            if (leashBad || tetherBad || core)
            {
                string why = (leashBad ? "leash<need;" : "") + (tetherBad ? "tether<exit;" : "") + (core ? "core(axis " + (axisDist * 1000f).ToString("F0") + " mm of r " + (rad * 1000f).ToString("F0") + ");" : "");
                det?.Add($"conflict,{p},{R(p)},{maxD[p] * 1000f:F1},{need * 1000f:F2},{shapes[skinShape[p]].name},{why} path {pathLen[p] * 1000f:F0} mm");
            }
        }
        foreach (var e in edges)
            if (inSkin[e.a] && inSkin[e.b] && free[e.a] && free[e.b] && skinNormal[e.a] != Vector3.zero && skinNormal[e.b] != Vector3.zero && Vector3.Dot(skinNormal[e.a], skinNormal[e.b]) < 0f) fs.divEdges++;
    }

    // ==============================================================================================================================================================
    // Fit-data export: for each collider bone whose name contains fitBoneKey, the vertices of the equipment mesh named *Leggings* that are weighted to that bone, and every pinned garment particle within 0.6 m of the bone,
    // as vectors in the bone's own axes (rotation only, metres): the frame a collider attached to that bone moves in.
    readonly StringBuilder fitCsv = new StringBuilder("leg,frame,kind,id,x,y,z\n"); Dictionary<string, List<int>> fitVerts;
    readonly StringBuilder clipCsv = new StringBuilder("frame,id,x,y,z,depth_mm,colliderSd_mm,nearestShape\n");
    readonly Dictionary<int, StringBuilder> covCsv = new Dictionary<int, StringBuilder>();
    // Collider shapes (S rows: name, a, ra, b, rb) and every *Leggings* vertex (V rows: index, position, dominant bone) in world space, as the validator sees them this frame. Read only.
    void CoverageDump(int fi, Shape[] shapes)
    {
        var sb = new StringBuilder("kind,name_or_index,x,y,z,r,x2,y2,z2,r2,bone\n"); covCsv[fi] = sb;
        foreach (var s in shapes) sb.AppendLine($"S,{s.name},{s.a.x:F4},{s.a.y:F4},{s.a.z:F4},{s.ra:F4},{s.b.x:F4},{s.b.y:F4},{s.b.z:F4},{s.rb:F4},");
        SkinnedMeshRenderer leg = null; foreach (var e in equipment) if (e && e.name.Contains(fitEquipKey)) leg = e;
        if (leg == null) return;
        var baked = new Mesh(); leg.BakeMesh(baked, true); var bv = baked.vertices; var bw = leg.sharedMesh.boneWeights; var lb = leg.bones; var lt = leg.transform;
        for (int i = 0; i < bv.Length; i++)
        {
            var w = bw[i]; int bi = w.boneIndex0; float best = w.weight0;
            if (w.weight1 > best) { best = w.weight1; bi = w.boneIndex1; }
            if (w.weight2 > best) { best = w.weight2; bi = w.boneIndex2; }
            if (w.weight3 > best) { best = w.weight3; bi = w.boneIndex3; }
            var wp = lt.TransformPoint(bv[i]);
            sb.AppendLine($"V,{i},{wp.x:F4},{wp.y:F4},{wp.z:F4},,,,,,{(bi >= 0 && bi < lb.Length && lb[bi] ? lb[bi].name : "?")}");
        }
        Destroy(baked);
    }

    void DumpFit(int fi, Vector3[] skin)
    {
        var bones = new List<Transform>();
        foreach (var pr in cloth.sphereColliders) { if (pr.first == null) continue; var b = pr.first.transform.parent; if (b && b.name.Contains(fitBoneKey) && !bones.Contains(b)) bones.Add(b); }
        SkinnedMeshRenderer leg = null; foreach (var e in equipment) if (e && e.name.Contains(fitEquipKey)) leg = e;
        if (leg == null) { notes.Add("fit export: no *Leggings* equipment found"); return; }
        if (fitVerts == null)
        {
            fitVerts = new Dictionary<string, List<int>>(); var bw = leg.sharedMesh.boneWeights; var lb = leg.bones;
            foreach (var b in bones)
            {
                var l = new List<int>(); int bi = Array.FindIndex(lb, x => x && x.name == b.name);
                for (int i = 0; i < bw.Length; i++)
                {
                    float w = (bw[i].boneIndex0 == bi ? bw[i].weight0 : 0f) + (bw[i].boneIndex1 == bi ? bw[i].weight1 : 0f) + (bw[i].boneIndex2 == bi ? bw[i].weight2 : 0f) + (bw[i].boneIndex3 == bi ? bw[i].weight3 : 0f);
                    if (bi >= 0 && w >= 0.4f) l.Add(i);
                }
                int stride = Mathf.Max(1, l.Count / fitMaxPointsPerLeg); var s = new List<int>(); for (int i = 0; i < l.Count; i += stride) s.Add(l[i]);
                fitVerts[b.name] = s;
            }
        }
        var baked = new Mesh(); leg.BakeMesh(baked, true); var bv = baked.vertices; var lt = leg.transform;
        foreach (var b in bones)
        {
            var inv = Quaternion.Inverse(b.rotation);
            foreach (int i in fitVerts[b.name]) { var l = inv * (lt.TransformPoint(bv[i]) - b.position); fitCsv.AppendLine($"{b.name},{fi},L,{i},{l.x:F4},{l.y:F4},{l.z:F4}"); }
            for (int p = 0; p < n; p++) { if (free[p]) continue; var l = inv * (skin[p] - b.position); if (l.magnitude < 0.6f) fitCsv.AppendLine($"{b.name},{fi},P,{p},{l.x:F4},{l.y:F4},{l.z:F4}"); }
        }
        Destroy(baked);
    }

    // Pinned particles against the full rendered leggings surface (baked mesh, world space): closest-point distance, sign from the interpolated vertex normal at the closest point,
    // and ray parity along +x/+y/+z (inside when at least 2 of 3 rays cross an odd number of triangles). Also written in the left-thigh bone's local axes, the space of fit_data.csv.
    void MeshCheck(int fi, Vector3[] skin, Vector3[] cur)
    {
        SkinnedMeshRenderer leg = null; foreach (var e in equipment) if (e && e.name.Contains(fitEquipKey)) leg = e;
        if (leg == null) { notes.Add("mesh check: no *Leggings* equipment found"); return; }
        var baked = new Mesh(); leg.BakeMesh(baked, true); var bv = baked.vertices; var bn = baked.normals; var tri = baked.triangles; var lt = leg.transform;
        var wv = new Vector3[bv.Length]; var wn = new Vector3[bv.Length];
        for (int i = 0; i < bv.Length; i++) { wv[i] = lt.TransformPoint(bv[i]); wn[i] = lt.TransformDirection(bn[i]).normalized; }
        Transform bone = null; foreach (var pr in cloth.sphereColliders) { var b = pr.first ? pr.first.transform.parent : null; if (b && b.name.Contains("LeftUpperLeg")) { bone = b; break; } }
        var sbm = new StringBuilder("id,wx,wy,wz,boneLocalX_mm,boneLocalY_mm,boneLocalZ_mm,pinnedErr_mm,dist_mm,signedNormal_mm,parityInside_of3,nearestVertex,tris\n");
        int nt = tri.Length / 3; var dirs = new[] { Vector3.right, Vector3.up, Vector3.forward };
        var thighBones = new List<Transform>(); foreach (var pr in cloth.sphereColliders) { var b = pr.first ? pr.first.transform.parent : null; if (b && b.name.Contains("UpperLeg") && !thighBones.Contains(b)) thighBones.Add(b); }
        for (int p = 0; p < n; p++)
        {
            if (free[p]) continue; var q = skin[p];
            if (meshCheckIds.Length > 0 && Array.IndexOf(meshCheckIds, p) < 0) continue;
            if (meshCheckNearThigh) { bool near = false; foreach (var tb in thighBones) if ((q - tb.position).sqrMagnitude < 0.09f) { near = true; break; } if (!near) continue; }
            float bd = float.MaxValue; int bt = -1; Vector3 bc = default; Vector3 bw = default;
            for (int t = 0; t < nt; t++)
            {
                Vector3 a = wv[tri[3 * t]], b = wv[tri[3 * t + 1]], c = wv[tri[3 * t + 2]];
                if (Mathf.Min(Mathf.Abs(a.y - q.y), Mathf.Abs(b.y - q.y), Mathf.Abs(c.y - q.y)) > 0.4f) continue;
                if (Mathf.Min(Mathf.Abs(a.x - q.x), Mathf.Abs(b.x - q.x), Mathf.Abs(c.x - q.x)) > 0.25f || Mathf.Min(Mathf.Abs(a.z - q.z), Mathf.Abs(b.z - q.z), Mathf.Abs(c.z - q.z)) > 0.25f) continue;
                var cp = ClosestOnTri(q, a, b, c, out var w); float d = (q - cp).sqrMagnitude; if (d < bd) { bd = d; bt = t; bc = cp; bw = w; }
            }
            if (bt < 0) continue;
            var nrm = wn[tri[3 * bt]] * bw.x + wn[tri[3 * bt + 1]] * bw.y + wn[tri[3 * bt + 2]] * bw.z; float sd = Mathf.Sqrt(bd) * Mathf.Sign(Vector3.Dot(q - bc, nrm) >= 0f ? 1f : -1f);
            int votes = 0;
            if (bd > 0.02f * 0.02f) continue;           // parity only matters near the surface; farther than 20 mm is not a contact either way
            foreach (var dir in dirs)
            {
                int hits = 0;
                for (int t = 0; t < nt; t++) if (RayTri(q, dir, wv[tri[3 * t]], wv[tri[3 * t + 1]], wv[tri[3 * t + 2]])) hits++;
                if ((hits & 1) == 1) votes++;
            }
            int nv = tri[3 * bt]; float bdv = float.MaxValue; for (int k = 0; k < 3; k++) { float d = (wv[tri[3 * bt + k]] - q).sqrMagnitude; if (d < bdv) { bdv = d; nv = tri[3 * bt + k]; } }
            var loc = bone ? Quaternion.Inverse(bone.rotation) * (q - bone.position) : Vector3.zero;
            sbm.AppendLine($"{p},{q.x:F4},{q.y:F4},{q.z:F4},{loc.x * 1000f:F1},{loc.y * 1000f:F1},{loc.z * 1000f:F1},{(cur[p] - q).magnitude * 1000f:F2},{Mathf.Sqrt(bd) * 1000f:F2},{sd * 1000f:F2},{votes},{nv},{nt}");
        }
        Directory.CreateDirectory(outDir); File.WriteAllText(Path.Combine(outDir, $"mesh_check_f{fi}.csv"), sbm.ToString()); Destroy(baked);
    }
    static Vector3 ClosestOnTri(Vector3 p, Vector3 a, Vector3 b, Vector3 c, out Vector3 bary)
    {
        Vector3 ab = b - a, ac = c - a, ap = p - a; float d1 = Vector3.Dot(ab, ap), d2 = Vector3.Dot(ac, ap);
        if (d1 <= 0 && d2 <= 0) { bary = new Vector3(1, 0, 0); return a; }
        Vector3 bp = p - b; float d3 = Vector3.Dot(ab, bp), d4 = Vector3.Dot(ac, bp);
        if (d3 >= 0 && d4 <= d3) { bary = new Vector3(0, 1, 0); return b; }
        float vc = d1 * d4 - d3 * d2; if (vc <= 0 && d1 >= 0 && d3 <= 0) { float v = d1 / (d1 - d3); bary = new Vector3(1 - v, v, 0); return a + v * ab; }
        Vector3 cpv = p - c; float d5 = Vector3.Dot(ab, cpv), d6 = Vector3.Dot(ac, cpv);
        if (d6 >= 0 && d5 <= d6) { bary = new Vector3(0, 0, 1); return c; }
        float vb = d5 * d2 - d1 * d6; if (vb <= 0 && d2 >= 0 && d6 <= 0) { float w = d2 / (d2 - d6); bary = new Vector3(1 - w, 0, w); return a + w * ac; }
        float va = d3 * d6 - d5 * d4; if (va <= 0 && (d4 - d3) >= 0 && (d5 - d6) >= 0) { float w = (d4 - d3) / ((d4 - d3) + (d5 - d6)); bary = new Vector3(0, 1 - w, w); return b + w * (c - b); }
        float den = 1f / (va + vb + vc); float vv = vb * den, ww = vc * den; bary = new Vector3(1 - vv - ww, vv, ww); return a + ab * vv + ac * ww;
    }
    static bool RayTri(Vector3 o, Vector3 d, Vector3 a, Vector3 b, Vector3 c)
    {
        Vector3 e1 = b - a, e2 = c - a, h = Vector3.Cross(d, e2); float det = Vector3.Dot(e1, h); if (Mathf.Abs(det) < 1e-9f) return false;
        float f = 1f / det; Vector3 s = o - a; float u = f * Vector3.Dot(s, h); if (u < 0f || u > 1f) return false;
        Vector3 qv = Vector3.Cross(s, e1); float v = f * Vector3.Dot(d, qv); if (v < 0f || u + v > 1f) return false;
        return f * Vector3.Dot(e2, qv) > 1e-6f;
    }

    // static self-collision / settings snapshot
    void SelfCollisionStatic()
    {
        var sb = new StringBuilder();
        var idx = new List<uint>(); cloth.GetSelfAndInterCollisionIndices(idx);
        float dist = cloth.selfCollisionDistance, stiff = cloth.selfCollisionStiffness;
        bool enabled = dist > 0f && stiff > 0f && idx.Count > 0;
        sb.AppendLine($"- selfCollisionDistance {dist * 1000f:F2} mm, selfCollisionStiffness {stiff:F2}, participating particle indices {idx.Count}");
        sb.AppendLine($"- **Self-collision is {(enabled ? "ENABLED" : "NOT enabled")}** (needs distance > 0, stiffness > 0 and at least one selected particle).");
        // nearest-neighbour spacing between rest positions (all particles, then the participating subset)
        float sc = garment.transform.lossyScale.x;
        var nn = new float[n]; int[] nnId = new int[n];
        for (int i = 0; i < n; i++) nn[i] = float.MaxValue;
        for (int i = 0; i < n; i++)
            for (int j = i + 1; j < n; j++)
            {
                float d = (meshVerts[first[i]] - meshVerts[first[j]]).sqrMagnitude;
                if (d < nn[i]) { nn[i] = d; nnId[i] = j; }
                if (d < nn[j]) { nn[j] = d; nnId[j] = i; }
            }
        var all = new List<float>(); for (int i = 0; i < n; i++) all.Add(Mathf.Sqrt(nn[i]) * sc); all.Sort();
        int minI = 0; for (int i = 1; i < n; i++) if (nn[i] < nn[minI]) minI = i;
        sb.AppendLine($"- rest spacing to nearest other particle, all {n} particles: min {all[0] * 1000f:F2} mm (ids {minI}/{nnId[minI]}), p1 {all[n / 100] * 1000f:F2}, p5 {all[n / 20] * 1000f:F2}, median {all[n / 2] * 1000f:F2} mm");
        if (idx.Count > 0)
        {
            var sub = new List<float>(); foreach (uint u in idx) if (u < n) sub.Add(Mathf.Sqrt(nn[u]) * sc); sub.Sort();
            if (sub.Count > 0) sb.AppendLine($"- same for the {sub.Count} participating particles: min {sub[0] * 1000f:F2} mm, median {sub[sub.Count / 2] * 1000f:F2} mm");
        }
        if (dist > 0f) sb.AppendLine($"- particles whose nearest neighbour is closer than the self-collision distance: {all.FindAll(x => x < dist).Count} of {n}");
        else sb.AppendLine($"- hypothetical: a self-collision distance above {all[0] * 1000f:F2} mm (min spacing) would exceed the closest pair; Unity advises staying below the minimum particle spacing, and the useful range here is much lower (p5 {all[n / 20] * 1000f:F2} mm).");
        selfInfo = sb.ToString();
        selfEnabled = enabled;
    }
    bool selfEnabled;

    // ==============================================================================================================================================================
    // helpers
    Shape[] GetShapes()
    {
        var l = new List<Shape>();
        foreach (var p in cloth.sphereColliders)
        {
            if (p.first == null) continue;
            var f = p.first; var s = p.second != null ? p.second : p.first;
            l.Add(new Shape { a = f.transform.TransformPoint(f.center), b = s.transform.TransformPoint(s.center), ra = f.radius * MaxScale(f.transform), rb = s.radius * MaxScale(s.transform), name = f.name + (p.second != null ? ">" + s.name : "") });
        }
        foreach (var c in cloth.capsuleColliders)
        {
            if (c == null) continue;
            var t = c.transform; var ls = t.lossyScale; int d = c.direction;
            Vector3 dirL = d == 0 ? Vector3.right : d == 1 ? Vector3.up : Vector3.forward;
            float sdir = Mathf.Abs(d == 0 ? ls.x : d == 1 ? ls.y : ls.z);
            float so = d == 0 ? Mathf.Max(Mathf.Abs(ls.y), Mathf.Abs(ls.z)) : d == 1 ? Mathf.Max(Mathf.Abs(ls.x), Mathf.Abs(ls.z)) : Mathf.Max(Mathf.Abs(ls.x), Mathf.Abs(ls.y));
            float r = c.radius * so, half = Mathf.Max(c.height * sdir * 0.5f - r, 0f);
            Vector3 ctr = t.TransformPoint(c.center), ax = t.TransformDirection(dirL).normalized;
            l.Add(new Shape { a = ctr - ax * half, b = ctr + ax * half, ra = r, rb = r, name = c.name });
        }
        return l.ToArray();
    }
    static float MaxScale(Transform t) { var s = t.lossyScale; return Mathf.Max(Mathf.Abs(s.x), Mathf.Abs(s.y), Mathf.Abs(s.z)); }

    // signed distance to a (possibly conical) capsule: axis distance minus the radius interpolated along the axis. The exact shape Cloth uses for a sphere pair is not documented; this is the same model as FitHarness.
    static float SdOne(Vector3 p, in Shape s)
    {
        var ab = s.b - s.a; float l2 = ab.sqrMagnitude; float u = l2 > 1e-12f ? Mathf.Clamp01(Vector3.Dot(p - s.a, ab) / l2) : 0f;
        return Vector3.Distance(p, s.a + ab * u) - Mathf.Lerp(s.ra, s.rb, u);
    }
    static float SdAll(Vector3 p, Shape[] ss, out int idx)
    {
        float b = float.MaxValue; idx = 0;
        for (int i = 0; i < ss.Length; i++) { float d = SdOne(p, ss[i]); if (d < b) { b = d; idx = i; } }
        return b;
    }
    static void ClosestOnAxis(Vector3 p, in Shape s, out Vector3 q, out float radius)
    {
        var ab = s.b - s.a; float l2 = ab.sqrMagnitude; float u = l2 > 1e-12f ? Mathf.Clamp01(Vector3.Dot(p - s.a, ab) / l2) : 0f;
        q = s.a + ab * u; radius = Mathf.Lerp(s.ra, s.rb, u);
    }

    Vector3 Skinned(int i, Matrix4x4[] bones)
    {
        BoneWeight bw = weights[i]; Vector3 p = Vector3.zero;
        if (bw.weight0 > 0f) p += (bones[bw.boneIndex0] * bind[bw.boneIndex0]).MultiplyPoint3x4(meshVerts[i]) * bw.weight0;
        if (bw.weight1 > 0f) p += (bones[bw.boneIndex1] * bind[bw.boneIndex1]).MultiplyPoint3x4(meshVerts[i]) * bw.weight1;
        if (bw.weight2 > 0f) p += (bones[bw.boneIndex2] * bind[bw.boneIndex2]).MultiplyPoint3x4(meshVerts[i]) * bw.weight2;
        if (bw.weight3 > 0f) p += (bones[bw.boneIndex3] * bind[bw.boneIndex3]).MultiplyPoint3x4(meshVerts[i]) * bw.weight3;
        return p;
    }

    Vector3[] CpuSkin()
    {
        var smrBones = garment.bones; var bm = new Matrix4x4[smrBones.Length];
        for (int i = 0; i < bm.Length; i++) bm[i] = smrBones[i].localToWorldMatrix;
        var s = new Vector3[n]; for (int p = 0; p < n; p++) s[p] = Skinned(first[p], bm); return s;
    }
    // history[0] = this frame, history[k] = k frames ago; lag may be fractional
    static Vector3[] LerpSkin(List<Vector3[]> h, float lag)
    {
        int i = Mathf.Min(Mathf.FloorToInt(lag), h.Count - 1), j = Mathf.Min(i + 1, h.Count - 1); float f = lag - Mathf.FloorToInt(lag); if (i == j) f = 0f;
        var r = new Vector3[h[i].Length]; for (int p = 0; p < r.Length; p++) r[p] = Vector3.Lerp(h[i][p], h[j][p], f); return r;
    }
    static Matrix4x4 LerpMat(List<Matrix4x4> h, float lag)
    {
        int i = Mathf.Min(Mathf.FloorToInt(lag), h.Count - 1), j = Mathf.Min(i + 1, h.Count - 1); float f = lag - Mathf.FloorToInt(lag); if (i == j) f = 0f;
        var r = new Matrix4x4(); for (int k = 0; k < 16; k++) r[k] = Mathf.Lerp(h[i][k], h[j][k], f); return r;
    }
    static Shape[] LerpShapes(List<Shape[]> h, float lag)
    {
        int i = Mathf.Min(Mathf.FloorToInt(lag), h.Count - 1), j = Mathf.Min(i + 1, h.Count - 1); float f = lag - Mathf.FloorToInt(lag); if (i == j || h[i].Length != h[j].Length) f = 0f;
        var r = new Shape[h[i].Length];
        for (int k = 0; k < r.Length; k++) r[k] = new Shape { a = Vector3.Lerp(h[i][k].a, h[j][k].a, f), b = Vector3.Lerp(h[i][k].b, h[j][k].b, f), ra = h[i][k].ra, rb = h[i][k].rb, name = h[i][k].name };
        return r;
    }

    string R(int p) { var v = meshVerts[first[p]]; return $"{v.x:F3}|{v.y:F3}|{v.z:F3}"; }
    static string V(Vector3 v) => $"({v.x:F3},{v.y:F3},{v.z:F3})";
    static long Key(Vector3 p, float cell) => Key(Mathf.FloorToInt(p.x / cell), Mathf.FloorToInt(p.y / cell), Mathf.FloorToInt(p.z / cell));
    static long Key(int x, int y, int z) => ((long)(x + 4096) << 40) | ((long)(y + 4096) << 20) | (long)(z + 4096);

    // Baked world-space vertices + normals of the visible equipment, in a 4 cm grid. Depth = how far a point lies behind the nearest equipment vertex's surface (nearest vertex + its normal: approximate near creases and edges).
    class EquipGrid
    {
        public List<Vector3> pos = new List<Vector3>(), nrm = new List<Vector3>(); public List<int> rend = new List<int>();
        public Dictionary<long, List<int>> grid = new Dictionary<long, List<int>>();
        const float Cell = 0.04f;
        public float Depth(Vector3 w, out int r)
        {
            r = -1; int best = -1; float bd = Cell * Cell;
            int cx = Mathf.FloorToInt(w.x / Cell), cy = Mathf.FloorToInt(w.y / Cell), cz = Mathf.FloorToInt(w.z / Cell);
            for (int dx = -1; dx <= 1; dx++) for (int dy = -1; dy <= 1; dy++) for (int dz = -1; dz <= 1; dz++)
                if (grid.TryGetValue(Key(cx + dx, cy + dy, cz + dz), out var l)) foreach (int i in l) { float d = (pos[i] - w).sqrMagnitude; if (d < bd) { bd = d; best = i; } }
            if (best < 0) return 0f;
            r = rend[best]; return Mathf.Max(0f, -Vector3.Dot(w - pos[best], nrm[best]));
        }
        public void Add(Vector3 p, Vector3 nrmW, int rd) { int i = pos.Count; pos.Add(p); nrm.Add(nrmW); rend.Add(rd); long k = Key(p, Cell); if (!grid.TryGetValue(k, out var l)) grid[k] = l = new List<int>(); l.Add(i); }
    }

    EquipGrid BuildEquipmentGrid(out int count)
    {
        count = 0; if (equipment == null || equipment.Length == 0) return null;
        var g = new EquipGrid(); var baked = new Mesh();
        for (int r = 0; r < equipment.Length; r++)
        {
            var s = equipment[r]; if (s == null || !s.enabled) continue;
            s.BakeMesh(baked, true); var v = baked.vertices; var nm = baked.normals; var t = s.transform;
            for (int i = 0; i < v.Length; i++) g.Add(t.TransformPoint(v[i]), t.TransformDirection(nm[i]).normalized, r);
        }
        Destroy(baked); count = g.pos.Count; return g;
    }

    // ==============================================================================================================================================================
    void OnDrawGizmos()
    {
        if (!drawGizmos || !Application.isPlaying || vizShapes == null) return;
        Gizmos.color = new Color(0.6f, 0.6f, 0.6f);
        foreach (var s in vizShapes) { Gizmos.DrawWireSphere(s.a, s.ra); Gizmos.DrawWireSphere(s.b, s.rb); Gizmos.DrawLine(s.a, s.b); }
        Gizmos.color = Color.red; foreach (var p in vizIn) Gizmos.DrawSphere(p, 0.006f);                       // free particle inside a collider
        Gizmos.color = new Color(1f, 0.55f, 0f); for (int i = 0; i < vizLeashA.Count; i++) Gizmos.DrawLine(vizLeashA[i], vizLeashB[i]);   // skinned position -> simulated, beyond Max Distance
        Gizmos.color = Color.magenta; for (int i = 0; i + 2 < vizTri.Count; i += 3) { Gizmos.DrawLine(vizTri[i], vizTri[i + 1]); Gizmos.DrawLine(vizTri[i + 1], vizTri[i + 2]); Gizmos.DrawLine(vizTri[i + 2], vizTri[i]); }   // triangle inside a collider, vertices outside
        Gizmos.color = Color.cyan; foreach (var p in vizClip) Gizmos.DrawSphere(p, 0.005f);                    // clear of colliders but inside visible equipment
    }

    // ==============================================================================================================================================================
    static float Pct(List<float> sorted, float q) => sorted.Count == 0 ? 0f : sorted[Mathf.Clamp(Mathf.FloorToInt(q * (sorted.Count - 1)), 0, sorted.Count - 1)];
    static string F(float v, string f = "F1") => v.ToString(f, CultureInfo.InvariantCulture);

    void Finish()
    {
        if (finished) return; finished = true;
        try { WriteReport(); } catch (Exception e) { Debug.LogError("ClothValidator report failed: " + e); }
        File.WriteAllText(Path.Combine(outDir, "DONE.txt"), "done frames " + sampledFrames);
#if UNITY_EDITOR
        if (quitPlayAtEnd) UnityEditor.EditorApplication.isPlaying = false;
#endif
    }

    void WriteReport()
    {
        var ci = CultureInfo.InvariantCulture;
        // frames.csv
        var csv = new StringBuilder("frame,steps,colliderMove_mm,timeSinceStep_ms,pinnedErr_mm,pIn,pInDeepest_mm,pInDeepestId,triOnlyN,triOnlyDeepest_mm,triOnlyDeepestId,triVertN,triVertDeepest_mm,clipParticles,clipTriangles,clipDeepest_mm,clipDeepestId,overLeashN,overLeashWorst_mm,overLeashWorstId,edgeMax,edgeA,edgeB,edges_gt1.25,edges_gt2,collapsedTris,jitterValid,a2p95_mm,a2max_mm,a2maxId,reversals,skinInside,infeasibleLeash,infeasibleTether,coreAnchors,divergentEdges,pinnedInside,eqGapP50_mm,eqGapP90_mm,eqGapMax_mm,sample_ms,stateLag_frames,pinnedRms_current_mm,pinnedRms_matched_mm,pInMatched,pInDeepMatched_mm,overLeashMatchedN,overLeashMatchedWorst_mm,a2p95Matched_mm,a2maxMatched_mm,tetherExceededBySkinAlone,ringEdgeMax,hemRise_mm\n");
        foreach (var s in stats)
            csv.AppendLine(string.Join(",", s.frame, s.steps, F(s.colliderMove), F(s.timeLag), F(s.pinnedErr, "F2"), s.pIn, F(s.pInDeep), s.pInDeepId, s.triOnlyN, F(s.triOnlyDeep), s.triOnlyDeepId, s.triVertN, F(s.triVertDeep), s.clipP, s.clipT, F(s.clipDeep), s.clipDeepId,
                s.overLeash, F(s.overLeashMax), s.overLeashId, F(s.edgeMax, "F3"), s.edgeA, s.edgeB, s.stretched, s.over2x, s.collapsed, s.jitterValid ? 1 : 0, F(s.a2p95, "F2"), F(s.a2max, "F2"), s.a2maxId, s.rev, s.skinIn, s.infLeash, s.infTether, s.core, s.divEdges, s.pinnedIn, F(s.eqGapP50), F(s.eqGapP90), F(s.eqGapMax), F(s.sampleMs, "F0"), F(s.stateLag, "F2"), F(s.pinnedRms0), F(s.pinnedRmsM), s.pInM, F(s.pInDeepM), s.overLeashM, F(s.overLeashMaxM), F(s.a2p95m, "F2"), F(s.a2maxm, "F2"), s.tetherSkin, F(s.ringEdgeMax, "F3"), F(s.hemRise)));
        File.WriteAllText(Path.Combine(outDir, "frames.csv"), csv.ToString());
        if (fitCsv.Length > 30) File.WriteAllText(Path.Combine(outDir, "fit_data.csv"), fitCsv.ToString());
        if (clipLog) File.WriteAllText(Path.Combine(outDir, "clip_particles.csv"), clipCsv.ToString());
        foreach (var kv in covCsv) File.WriteAllText(Path.Combine(outDir, "coverage_f" + kv.Key + ".csv"), kv.Value.ToString());

        // particles.csv (only particles with any flag)
        var pc = new StringBuilder("id,meshVertex,restX,restY,restZ,maxDist_mm,pathToPinned_mm,framesInsideCollider,maxInside_mm,framesOverLeash,maxOverLeash_mm,framesJitter,rmsSecondDiff_mm,reversalFrames,framesClipEquipment,maxClipEquipment_mm,framesLeashInfeasible,maxLeashDeficit_mm,framesTetherInfeasible,framesCoreAnchor\n");
        int jf = 0; foreach (var s in stats) if (s.jitterValid) jf++;
        for (int p = 0; p < n; p++)
        {
            if (inFrames[p] + leashFrames[p] + jitFrames[p] + clipFrames[p] + confLeash[p] + confTether[p] + confCore[p] == 0) continue;
            var v = meshVerts[first[p]];
            pc.AppendLine(string.Join(",", p, first[p], F(v.x, "F3"), F(v.y, "F3"), F(v.z, "F3"), F(maxD[p] * 1000f), free[p] ? F(pathLen[p] * 1000f) : "0", inFrames[p], F(inMax[p] * 1000f), leashFrames[p], F(leashMax[p] * 1000f), jitFrames[p], jf > 0 ? F(Mathf.Sqrt(a2sum[p] / jf) * 1000f, "F2") : "", revFrames[p], clipFrames[p], F(clipMax[p] * 1000f), confLeash[p], F(confDeficit[p] * 1000f), confTether[p], confCore[p]));
        }
        File.WriteAllText(Path.Combine(outDir, "particles.csv"), pc.ToString());

        // required_clearance.csv: for every free particle whose skinned pose is inside a collider at some sampled frame, the exit distance it needs (depth + collisionSphereDistance), against its Max Distance and tether reach
        var rc = new StringBuilder("id,restX,restY,restZ,maxDist_mm,needMax_mm,frameOfMax,framesInside,shapeAtMax,exitDirX,exitDirY,exitDirZ,pathToPinned_mm,exitBeyondTetherReach_mm\n");
        for (int p = 0; p < n; p++)
        {
            if (!free[p] || needFrames[p] == 0) continue;
            var v = meshVerts[first[p]]; var d = needDir[p];
            rc.AppendLine(string.Join(",", p, F(v.x, "F4"), F(v.y, "F4"), F(v.z, "F4"), F(maxD[p] * 1000f), F(needMax[p] * 1000f), needFrame[p], needFrames[p], needShape[p], F(d.x, "F3"), F(d.y, "F3"), F(d.z, "F3"), F(pathLen[p] * 1000f), F(overReachMax[p] * 1000f)));
        }
        File.WriteAllText(Path.Combine(outDir, "required_clearance.csv"), rc.ToString());

        // triangles.csv
        var tc = new StringBuilder("triangle,kind,particleIds,meshVertexIds,frames,maxDepth_mm,deepestShape\n");
        string Ids(int t) => $"{triIdx[t * 3]}|{triIdx[t * 3 + 1]}|{triIdx[t * 3 + 2]}";
        string Vids(int t) => $"{triVertIdx[t * 3]}|{triVertIdx[t * 3 + 1]}|{triVertIdx[t * 3 + 2]}";
        foreach (var kv in triOnly) tc.AppendLine($"{kv.Key},collider_triangle_only,{Ids(kv.Key)},{Vids(kv.Key)},{kv.Value.frames},{F(kv.Value.maxDepth * 1000f, "F2")},{(vizShapes.Length > kv.Value.shape ? vizShapes[kv.Value.shape].name : "")}");
        foreach (var kv in triVert) tc.AppendLine($"{kv.Key},collider_vertex_assisted,{Ids(kv.Key)},{Vids(kv.Key)},{kv.Value.frames},{F(kv.Value.maxDepth * 1000f, "F2")},{(vizShapes.Length > kv.Value.shape ? vizShapes[kv.Value.shape].name : "")}");
        foreach (var kv in triClip) tc.AppendLine($"{kv.Key},equipment_clip_clear_of_colliders,{Ids(kv.Key)},{Vids(kv.Key)},{kv.Value.frames},{F(kv.Value.maxDepth * 1000f, "F2")},");
        File.WriteAllText(Path.Combine(outDir, "triangles.csv"), tc.ToString());
        foreach (var kv in detail) File.WriteAllText(Path.Combine(outDir, "detail_f" + kv.Key + ".csv"), "kind,id,restXYZ,maxDist_mm,value_mm,shape_or_equipment,note\n" + string.Join("\n", kv.Value));

        // ---- report.md ----------------------------------------------------------------------------------------------------------------------------
        int N = stats.Count; var rep = new StringBuilder();
        rep.AppendLine($"# Cloth validation: {garment.name} ({gameObject.scene.name})");
        rep.AppendLine();
        rep.AppendLine($"Frames sampled: {N} ({(N > 0 ? stats[0].frame : 0)}..{(N > 0 ? stats[N - 1].frame : 0)}), harness frame index {(harness ? "FitHarness.CurrentFrame" : "own counter")}. Particles {n} ({freeCount} free, {n - freeCount} pinned), triangles {triIdx.Length / 3}, edges {edges.Count}. Particle mapping verified: **{(mappingOk ? "yes" : "NO")}**. Pinned-particle position check (cloth vs CPU-skinned, should be ~0): max {F(spaceErrMax * 1000f, "F2")} mm.");
        foreach (var nt in notes) rep.AppendLine("- " + nt);
        rep.AppendLine();
        rep.AppendLine("Cloth settings: stretch " + cloth.stretchingStiffness + ", bend " + cloth.bendingStiffness + ", damping " + cloth.damping + ", worldVelocityScale " + cloth.worldVelocityScale + ", worldAccelerationScale " + cloth.worldAccelerationScale + ", solverFrequency " + cloth.clothSolverFrequency + ", sleepThreshold " + cloth.sleepThreshold + ", useTethers " + cloth.useTethers + ", virtualParticleWeights/useVirtualParticles " + cloth.useVirtualParticles + ", enableContinuousCollision " + cloth.enableContinuousCollision + ", useGravity " + cloth.useGravity + ", collisionMassScale " + cloth.collisionMassScale + ", fixedDeltaTime " + Time.fixedDeltaTime + " s, Physics.simulationMode " + Physics.simulationMode + ", animator updateMode " + (animator ? animator.updateMode.ToString() : "n/a") + ", captureFramerate " + Time.captureFramerate + ".");
        rep.AppendLine();
        rep.AppendLine("Colliders (world pose at the last sampled frame):");
        rep.AppendLine(shapesInfo);
        rep.AppendLine("Equipment compared: " + (equipment == null ? "none" : string.Join(", ", Array.ConvertAll(equipment, e => e.name))));
        rep.AppendLine();

        // 1
        int fIn = 0; float worstIn = 0f; int worstInId = -1, worstInFrame = -1; long sumIn = 0;
        foreach (var s in stats) { if (s.pIn > 0) fIn++; sumIn += s.pIn; if (s.pInDeep > worstIn) { worstIn = s.pInDeep; worstInId = s.pInDeepId; worstInFrame = s.frame; } }
        rep.AppendLine("## 1. Particle penetration (simulated free particles inside their collider, depth > " + F(penTol * 1000f) + " mm)");
        rep.AppendLine($"Result: **{(fIn > 0 ? "VIOLATION OBSERVED" : "not observed")}**. Frames with at least one free particle inside: {fIn} of {N}; mean particles inside per frame {(N > 0 ? F((float)sumIn / N) : "0")}; deepest {F(worstIn)} mm (particle {worstInId}, frame {worstInFrame}).");
        if (fIn > 0) { rep.AppendLine("Deepest particles overall (id: frames inside / max depth mm): " + TopList(inMax, inFrames, 15, 1000f)); rep.AppendLine("By collider (frames-with-a-particle-count, deepest mm): " + DictList(shapeIn)); }
        int fPin = 0; foreach (var s in stats) if (s.pinnedIn > 0) fPin++;
        rep.AppendLine($"Pinned particles (Max Distance 0) inside a collider at the simulated position: {fPin} of {N} frames. Same count evaluated at the cloth's own time offset (colliders matched to the pose the solver saw, see section 4): frames with a free particle inside {Frames(s => s.pInM > 0)}, mean {F(MeanOf(s => s.pInM))} per frame, deepest {F(MaxF(s => s.pInDeepM))} mm; at the current pose: mean {F(MeanOf(s => s.pIn))} per frame.");
        rep.AppendLine();

        // 2
        int fTo = 0, fTv = 0; float dTo = 0f, dTv = 0f; int dToId = -1, dToF = -1; foreach (var s in stats) { if (s.triOnlyN > 0) fTo++; if (s.triVertN > 0) fTv++; if (s.triOnlyDeep > dTo) { dTo = s.triOnlyDeep; dToId = s.triOnlyDeepId; dToF = s.frame; } dTv = Mathf.Max(dTv, s.triVertDeep); }
        rep.AppendLine("## 2. Triangle penetration (rendered triangle interior inside a collider, depth > " + F(0.002f * 1000f) + " mm; barycentric sampling at <= 10 mm spacing, 14 subdivisions max)");
        rep.AppendLine($"Result: triangle-only (all three vertices outside, interior inside): **{(fTo > 0 ? "VIOLATION OBSERVED" : "not observed")}**, frames {fTo} of {N}, deepest {F(dTo)} mm (triangle {dToId}, frame {dToF}), {triOnly.Count} distinct triangles. Triangles that also have an inside vertex (explained by the particle penetration above): frames {fTv} of {N}, deepest {F(dTv)} mm, {triVert.Count} distinct triangles.");
        if (triOnly.Count > 0) rep.AppendLine("Worst triangle-only (triangle: frames / max depth mm, particle ids): " + TriList(triOnly, 12));
        rep.AppendLine();

        // 3
        long cP = 0; float cDeep = 0f; int cDeepId = -1, cDeepF = -1; int fC = 0; foreach (var s in stats) { cP += s.clipP; if (s.clipP + s.clipT > 0) fC++; if (s.clipDeep > cDeep) { cDeep = s.clipDeep; cDeepId = s.clipDeepId; cDeepF = s.frame; } }
        var p50 = new List<float>(); var p90 = new List<float>(); float gmax = 0f; foreach (var s in stats) { if (s.eqGapMax != 0f || s.eqGapP50 != 0f) { p50.Add(s.eqGapP50); p90.Add(s.eqGapP90); gmax = Mathf.Max(gmax, s.eqGapMax); } }
        p50.Sort(); p90.Sort();
        rep.AppendLine("## 3. Collider coverage vs visible equipment (clipping of equipment where the cloth is clear of every collider; nearest-vertex depth, approximate near creases)");
        rep.AppendLine($"Result: **{(fC > 0 ? "VIOLATION OBSERVED" : "not observed")}**. Frames where free cloth particles or triangle interiors are clear of all colliders yet more than {F(equipTol * 1000f)} mm inside visible equipment: {fC} of {N}; free particles: mean {(N > 0 ? F((float)cP / N) : "0")} per frame, deepest {F(cDeep)} mm (particle {cDeepId}, frame {cDeepF}); distinct clipping triangles {triClip.Count}. By equipment (sample count): {IntList(clipByEquip)}.");
        if (clipFrames != null) rep.AppendLine("Most persistent clipping particles (id: frames / max depth mm): " + TopList(clipMax, clipFrames, 15, 1000f));
        rep.AppendLine($"Collider surface vs equipment surface, equipment vertices within 6 cm of a free cloth particle (signed distance to the collider union, mm; positive = equipment lies outside every collider): median of per-frame medians {F(Pct(p50, 0.5f))}, median of per-frame p90 {F(Pct(p90, 0.5f))}, largest {F(gmax)}.");
        foreach (var kv in eqGapByShape) { kv.Value.Sort(); rep.AppendLine($"- nearest collider {kv.Key}: {kv.Value.Count} vertex-samples, p50 {F(Pct(kv.Value, 0.5f) * 1000f)} mm, p90 {F(Pct(kv.Value, 0.9f) * 1000f)} mm, max {F(Pct(kv.Value, 1f) * 1000f)} mm, share outside the collider by more than 5 mm {F(100f * kv.Value.FindAll(x => x > 0.005f).Count / kv.Value.Count)} %"); }
        rep.AppendLine();

        // 4
        rep.AppendLine("## 4. Animation / update synchronization");
        rep.AppendLine("The cloth read-back (`cloth.vertices`) is compared with the CPU-skinned pose of the last 5 rendered frames. `stateLag` = the (fractional) number of rendered frames at which the pinned particles, which Cloth drives exactly from the skin, best match the skin history. 0 = the cloth shows the current animation pose; larger = the cloth shows an older pose. `matched` metrics re-evaluate the colliders and skinned references at that stateLag, i.e. what the solver actually saw.");
        rep.AppendLine("| steps in frame | frames | mean stateLag (frames) | pinned RMS vs skin, current pose (mm) | pinned RMS, matched pose (mm) | free particles inside colliders, current pose | inside, matched pose | beyond leash, current | beyond leash, matched | mean max collider move per frame (mm) |");
        rep.AppendLine("|---|---|---|---|---|---|---|---|---|---|");
        for (int k = 0; k <= 2; k++)
        {
            int c = 0; float lag = 0f, r0 = 0f, rm = 0f, mv = 0f; long pi = 0, pim = 0, ol = 0, olm = 0;
            foreach (var s in stats) if (Mathf.Min(s.steps, 2) == k) { c++; lag += s.stateLag; r0 += s.pinnedRms0; rm += s.pinnedRmsM; mv += s.colliderMove; pi += s.pIn; pim += s.pInM; ol += s.overLeash; olm += s.overLeashM; }
            if (c > 0) rep.AppendLine($"| {(k == 2 ? "2+" : k.ToString())} | {c} | {F(lag / c, "F2")} | {F(r0 / c)} | {F(rm / c)} | {F((float)pi / c)} | {F((float)pim / c)} | {F((float)ol / c)} | {F((float)olm / c)} | {F(mv / c)} |");
        }
        var zero = new List<int>(); foreach (var s in stats) if (s.steps == 0) zero.Add(s.frame);
        rep.AppendLine($"Frames with no simulation step: {zero.Count} of {N}" + (zero.Count > 0 ? "; first: " + string.Join(", ", zero.GetRange(0, Mathf.Min(24, zero.Count))) : "") + $". Time from the last fixed step to render, mean {F(MeanOf(s => s.timeLag))} ms.");
        var lagList = new List<float>(); foreach (var s in stats) lagList.Add(s.stateLag); lagList.Sort();
        rep.AppendLine($"stateLag over all frames: median {F(Pct(lagList, 0.5f), "F2")}, p90 {F(Pct(lagList, 0.9f), "F2")}, max {F(Pct(lagList, 1f), "F2")} frames; frames at lag > 0.5: {Frames(s => s.stateLag > 0.5f)}. Pinned-particle mismatch left at the best lag (mean RMS) {F(MeanOf(s => s.pinnedRmsM))} mm.");
        rep.AppendLine($"Result: **{(MaxF(s => s.stateLag) > 0.5f ? "VIOLATION OBSERVED" : "not observed")}**: on {Frames(s => s.stateLag > 0.5f)} frames the displayed cloth is a whole rendered frame behind the animation; the garment is then {F(MeanOf(s => s.stateLag > 0.5f ? s.pinnedRms0 : 0f) * stats.Count / Mathf.Max(1, stats.FindAll(s => s.stateLag > 0.5f).Count))} mm RMS (worst frame {F(MaxF(s => s.pinnedRms0))} mm) from the animated skin on its pinned parts, against {F(MeanOf(s => s.stateLag <= 0.5f ? s.pinnedRmsM : 0f) * stats.Count / Mathf.Max(1, stats.FindAll(s => s.stateLag <= 0.5f).Count))} mm on frames with a fresh step.");
        rep.AppendLine();

        // 5
        rep.AppendLine("## 5. Self-collision, jitter, constraint violations");
        rep.AppendLine(selfInfo);
        int fOver = 0; float wOver = 0f; int wOverId = -1, wOverF = -1; foreach (var s in stats) { if (s.overLeash > 0) fOver++; if (s.overLeashMax > wOver) { wOver = s.overLeashMax; wOverId = s.overLeashId; wOverF = s.frame; } }
        float eMax = 0f; int eA = -1, eB = -1, eF = -1; var eList = new List<float>(); int cmax = 0, fColl = 0; foreach (var s in stats) { if (s.edgeMax > eMax) { eMax = s.edgeMax; eA = s.edgeA; eB = s.edgeB; eF = s.frame; } eList.Add(s.edgeMax); cmax = Mathf.Max(cmax, s.collapsed); if (s.collapsed > 0) fColl++; }
        eList.Sort();
        rep.AppendLine($"Max Distance (leash) violations, > {F(leashTol * 1000f)} mm beyond the limit: frames {fOver} of {N}, worst {F(wOver)} mm (particle {wOverId}, frame {wOverF}). Persistent: " + TopList(leashMax, leashFrames, 12, 1000f));
        rep.AppendLine($"Waist ring (edges between a pinned and a free particle): worst stretch {F(MaxF(s => s.ringEdgeMax), "F2")}x, median per-frame worst {F(MeanOf(s => s.ringEdgeMax), "F2")}x (mean).");
        rep.AppendLine($"Distance (edge) constraints: worst stretch {F(eMax, "F2")}x of rest (edge {eA}-{eB}, frame {eF}); median per-frame worst {F(Pct(eList, 0.5f), "F2")}x; max edges above 1.25x in a frame {MaxOf(s => s.stretched)}, above 2x {MaxOf(s => s.over2x)}; collapsed triangles (< 30 % of rest area) max {cmax}, frames {fColl} of {N}.");
        if (jf > 0)
        {
            var a95m = new List<float>(); foreach (var s in stats) if (s.jitterValid) a95m.Add(s.a2p95m); a95m.Sort();
            var a95 = new List<float>(); var amx = new List<float>(); long revT = 0; float aMaxAll = 0f; int aMaxId = -1, aMaxF = -1;
            foreach (var s in stats) if (s.jitterValid) { a95.Add(s.a2p95); amx.Add(s.a2max); revT += s.rev; if (s.a2max > aMaxAll) { aMaxAll = s.a2max; aMaxId = s.a2maxId; aMaxF = s.frame; } }
            a95.Sort(); amx.Sort();
            rep.AppendLine($"Oscillation (second difference of the cloth-minus-skinned offset, measured relative to the root so world motion drops out, over consecutive frames; {jf} frames): median p95 {F(Pct(a95, 0.5f), "F2")} mm, worst frame-p95 {F(Pct(a95, 1f), "F2")} mm, largest single {F(aMaxAll)} mm (particle {aMaxId}, frame {aMaxF}); reversals (direction flips, both steps > 2 mm) mean {F((float)revT / jf)} particles per frame.");
            rep.AppendLine($"Same oscillation measured against the skin pose matched to the cloth's own time offset (removes the display lag above): median p95 {F(Pct(a95m, 0.5f), "F2")} mm, worst frame-p95 {F(Pct(a95m, 1f), "F2")} mm; typical particle (median over free particles, averaged over frames) {F(MeanOf(s => s.a2p50m), "F2")} mm. Whatever remains is motion of the simulation itself.");
            rep.AppendLine("Most jittering particles (id: frames above " + F(jitterTol * 1000f) + " mm / reversal frames): " + TopListInt(jitFrames, revFrames, 15));
            rep.AppendLine("Jitter vs simulation steps (mean p95 second difference, mm): " + string.Join("; ", StepJitter()));
        }
        else rep.AppendLine("Oscillation: NOT EVALUATED (needs three consecutive sampled frames).");
        rep.AppendLine();

        // 6
        long sk = 0; int fCf = 0; float maxDef = 0f; int fInf = 0, fTe = 0, fCore = 0; int mxDiv = 0; foreach (var s in stats) { sk += s.skinIn; if (s.infLeash + s.infTether + s.core > 0) fCf++; if (s.infLeash > 0) fInf++; if (s.infTether > 0) fTe++; if (s.core > 0) fCore++; mxDiv = Mathf.Max(mxDiv, s.divEdges); }
        for (int p = 0; p < n; p++) maxDef = Mathf.Max(maxDef, confDeficit[p]);
        rep.AppendLine("## 6. Constraint conflicts (skinned reference position vs Max Distance vs assigned colliders)");
        rep.AppendLine($"Result: **{(fCf > 0 ? "VIOLATION OBSERVED" : "not observed")}**. Free particles whose skinned anchor is inside a collider: mean {(N > 0 ? F((float)sk / N) : "0")} per frame. A conflict is flagged when (a) the exit distance (depth + collisionSphereDistance) exceeds Max Distance, (b) tethers are on and the exit point lies farther from the pinned anchor than the surface path to it, or (c) the anchor lies within half a radius of the collider axis (escape direction ill-defined). Frames with (a): {fInf}, with (b): {fTe}, with (c): {fCore}; largest leash deficit {F(maxDef * 1000f)} mm; most adjacent inside-pairs with opposite escape directions in one frame: {mxDiv}. Context: a mean of {F(MeanOf(s => s.tetherSkin))} free particles per frame already have their skinned pose farther from their pinned anchor than the rest surface path (tether exceeded by the animation alone, not by a collider); that is a property of the skinning and applies with or without colliders.");
        rep.AppendLine("Particles by number of frames with a conflict (id: leash-infeasible frames / tether-infeasible frames / core frames / max deficit mm): " + ConflictList(20));
        rep.AppendLine();

        // limits
        rep.AppendLine("## Cannot be evaluated reliably");
        rep.AppendLine("- The exact collision shape Unity Cloth uses for a sphere-collider pair (cone versus capsule, radius blending) is not documented: depth values near the collider surface carry that uncertainty (same model as FitHarness).");
        rep.AppendLine("- `cloth.vertices` is a read-back of the solver, which runs on its own schedule: which physics step it reflects is inferred from the collider-pose lag test above, not read directly. Unity exposes no per-step cloth positions.");
        rep.AppendLine("- Per-substep solver state (how much collision correction was applied against how much constraint correction), tether scale and the particle collision radius are not exposed; the tether test assumes a scale of 1 and treats the particle as a point plus `collisionSphereDistance`.");
        rep.AppendLine("- Equipment depth uses the nearest baked vertex and its normal; it can mis-sign within about 5 mm near creases, rims and the crotch. Treat shallow clipping counts as indicative.");
        rep.AppendLine("- Triangle penetration is sampled (barycentric lattice, at most 14 subdivisions); very large stretched triangles can hide shallow intersections between samples.");
        rep.AppendLine("- Continuous collision, virtual particles: settings are reported, but whether a missed contact came from them cannot be observed.");
        rep.AppendLine("- Bending constraints are not evaluated.");
        rep.AppendLine("- The oscillation figures in section 5 cannot separate solver jitter from the animation's own acceleration while the character moves (a cloth that follows the skin has an offset whose second difference equals the skin's acceleration, tens of mm per frame for a swinging thigh). Only a held-pose run (FitHarness.freezeFrame) isolates solver jitter; treat in-motion values as an upper bound.");
        if (N > 1 && stats[N - 1].frame - stats[0].frame + 1 != N) rep.AppendLine("- Sampled frames are not contiguous: oscillation and lag results cover only consecutive runs.");
        File.WriteAllText(Path.Combine(outDir, "report.md"), rep.ToString());
    }

    float MeanOf(Func<FrameStat, float> f) { if (stats.Count == 0) return 0f; float s0 = 0f; foreach (var s in stats) s0 += f(s); return s0 / stats.Count; }
    float MaxF(Func<FrameStat, float> f) { float m = 0f; foreach (var s in stats) m = Mathf.Max(m, f(s)); return m; }
    string Frames(Func<FrameStat, bool> f) { int c = 0; foreach (var s in stats) if (f(s)) c++; return c + " of " + stats.Count; }
    int MaxOf(Func<FrameStat, int> f) { int m = 0; foreach (var s in stats) m = Mathf.Max(m, f(s)); return m; }

    List<string> StepJitter()
    {
        var res = new List<string>();
        for (int k = 0; k <= 2; k++)
        {
            float sum = 0f; int c = 0; foreach (var s in stats) if (s.jitterValid && Mathf.Min(s.steps, 2) == k) { sum += s.a2p95; c++; }
            if (c > 0) res.Add($"{(k == 2 ? "2+" : k.ToString())} steps: {F(sum / c, "F2")} ({c} frames)");
        }
        return res;
    }

    string TopList(float[] depth, int[] frames, int count, float scale)
    {
        var ids = new List<int>(); for (int p = 0; p < n; p++) if (frames[p] > 0) ids.Add(p);
        ids.Sort((a, b) => frames[b].CompareTo(frames[a]) != 0 ? frames[b].CompareTo(frames[a]) : depth[b].CompareTo(depth[a]));
        var sb = new StringBuilder(); for (int i = 0; i < Mathf.Min(count, ids.Count); i++) sb.Append($"{ids[i]}: {frames[ids[i]]} / {F(depth[ids[i]] * scale)}; "); return ids.Count == 0 ? "none" : sb.ToString() + $"({ids.Count} particles in total)";
    }
    string TopListInt(int[] a, int[] b, int count)
    {
        var ids = new List<int>(); for (int p = 0; p < n; p++) if (a[p] > 0) ids.Add(p);
        ids.Sort((x, y) => a[y].CompareTo(a[x]));
        var sb = new StringBuilder(); for (int i = 0; i < Mathf.Min(count, ids.Count); i++) sb.Append($"{ids[i]}: {a[ids[i]]} / {b[ids[i]]}; "); return ids.Count == 0 ? "none" : sb.ToString() + $"({ids.Count} particles in total)";
    }
    string ConflictList(int count)
    {
        var ids = new List<int>(); for (int p = 0; p < n; p++) if (confLeash[p] + confTether[p] + confCore[p] > 0) ids.Add(p);
        ids.Sort((x, y) => (confLeash[y] + confTether[y] + confCore[y]).CompareTo(confLeash[x] + confTether[x] + confCore[x]));
        var sb = new StringBuilder(); for (int i = 0; i < Mathf.Min(count, ids.Count); i++) { int p = ids[i]; sb.Append($"{p}: {confLeash[p]}/{confTether[p]}/{confCore[p]}/{F(confDeficit[p] * 1000f)} (rest {R(p)}, maxD {F(maxD[p] * 1000f)}); "); }
        return ids.Count == 0 ? "none" : sb.ToString() + $"({ids.Count} particles in total; full list in particles.csv)";
    }
    string TriList(Dictionary<int, TriAcc> d, int count)
    {
        var l = new List<KeyValuePair<int, TriAcc>>(d); l.Sort((a, b) => b.Value.maxDepth.CompareTo(a.Value.maxDepth));
        var sb = new StringBuilder(); for (int i = 0; i < Mathf.Min(count, l.Count); i++) sb.Append($"{l[i].Key}: {l[i].Value.frames} / {F(l[i].Value.maxDepth * 1000f)} ({triIdx[l[i].Key * 3]},{triIdx[l[i].Key * 3 + 1]},{triIdx[l[i].Key * 3 + 2]}); ");
        return sb.ToString();
    }
    static string DictList(Dictionary<string, int[]> d) { var sb = new StringBuilder(); foreach (var kv in d) sb.Append($"{kv.Key} ({kv.Value[0]}, {kv.Value[1] / 10f:F1} mm); "); return sb.ToString(); }
    static string IntList(Dictionary<string, int> d) { if (d.Count == 0) return "none"; var sb = new StringBuilder(); foreach (var kv in d) sb.Append($"{kv.Key} {kv.Value}; "); return sb.ToString(); }
}
