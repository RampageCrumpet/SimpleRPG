using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using SimpleRPG.Appearance;
using UnityEngine;

// Disposable (Assets/ClothFitTest, not part of the game): plays the production human with the candidate hauberk skirt setup so it can be judged by eye,
// and (probe = true) measures what the eye sees: where the leg shows through the cloth.
// Candidate: flared and thigh-weighted hauberk copy, Max Distance ramp starting at pinAbove, one sphere pair per thigh. Nothing here writes to a production asset.
// Keys: 1 front 3/4, 2 side, 3 rear 3/4, 4 seat (rear, low), 5 front thigh close-up, 0 stop moving the camera, C double sided, Space restarts the cycle.
public class SkirtSwayDemo : MonoBehaviour
{
    public GameObject characterPrefab;
    public RuntimeAnimatorController controller;
    public Avatar avatar;
    public BodyPartDefinition[] parts;
    public Mesh flareMesh;
    public bool useFlareMesh = true;
    public bool clothOff = false;
    public float damping = -1f, stretching = -1f, bending = -1f, worldVelocityScale = -1f, worldAccelerationScale = -1f;   // negative = leave the profile's value
    public bool asBuilt = false;          // leave the production cloth exactly as the game builds it (no mesh swap, pin height or collider change)
    public float pinAbove = 0.78f, rampWidth = 0.13f, allowanceScale = 1f;   // allowanceScale multiplies the baked Max Distance of every free particle
    public Vector3 fitA = new Vector3(0.008f, 0.12f, -0.016f), fitB = new Vector3(0.008f, 0.32f, -0.016f);
    public float fitRa = 0.100f, fitRb = 0.095f;
    public Vector3[] chainCentres; public float[] chainRadii;   // when set, replaces fitA/fitB: spheres down the thigh, neighbours paired
    public bool useColliders = true;
    public bool signedX;   // chain/fit x is the left thigh's own x as measured (right side negated), instead of -|x| on the left
    public bool doubleSided = true;        // test only: draws both sides of the hauberk (the production material culls back faces)
    public float idleSeconds = 2f, walkSeconds = 3f, runSeconds = 3f, stopSeconds = 1f, lungeSeconds = 2.2f, walkSpeed = 1.5f, runSpeed = 3.2f;

    // exposure probe: renders flat colours (leg red, hauberk green) and counts leg pixels showing where the skinned (cloth-off) hauberk covers
    public int[] litFrames = { 168, 306, 348 };
    public bool probe = false; public string probeDir = ""; public int probeEvery = 6, probeSize = 384, probeShots = 10;

    Material hauberkMaterial;
    GameObject character; Animator animator; Cloth cloth; Camera cam;
    float clock; string phase = ""; int view = 1; int frameCount;
    static readonly (string name, Vector3 pos, Vector3 target, float fov)[] Views =
    {
        ("front 3/4", new Vector3(1.1f, 1.1f, 1.6f), new Vector3(0, 0.95f, 0), 40),
        ("side", new Vector3(2.0f, 0.95f, 0), new Vector3(0, 0.95f, 0), 38),
        ("rear 3/4", new Vector3(-1.1f, 1.1f, -1.6f), new Vector3(0, 0.95f, 0), 40),
        ("seat", new Vector3(0.3f, 0.75f, -0.85f), new Vector3(0, 0.78f, 0), 45),
        ("front thigh", new Vector3(0.55f, 0.8f, 1.0f), new Vector3(0, 0.78f, 0.05f), 40),
    };

    void SetDoubleSided(bool on) { doubleSided = on; if (hauberkMaterial != null) hauberkMaterial.SetFloat("_Cull", on ? 0f : 2f); }

    void Start()
    {
        Application.runInBackground = true;
        Time.fixedDeltaTime = 1f / 60f;
        if (probe) Time.captureFramerate = 60;
        character = Instantiate(characterPrefab, transform); character.name = "Character";
        var partsRoot = new GameObject("Parts").transform; partsRoot.SetParent(character.transform, false);
        var rig = new RigBoneIndex(character.transform.Find("Human_Male_Rig"));
        var appearance = new AppearanceView(rig, partsRoot);
        foreach (var p in parts) appearance.Show(p);
        if (!character.TryGetComponent(out animator)) animator = character.AddComponent<Animator>();
        animator.avatar = avatar; animator.runtimeAnimatorController = controller; animator.applyRootMotion = false; animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;

        cloth = character.GetComponentInChildren<Cloth>(true);
        var smr = cloth.GetComponent<SkinnedMeshRenderer>();
        if (!asBuilt)
        {
            var coefficients = cloth.coefficients; var original = cloth.sphereColliders;
            cloth.enabled = false;
            if (useFlareMesh && flareMesh != null)
            {
                // carry the production per-particle Max Distance over to the new mesh by nearest rest position
                var oldPos = Welded(smr.sharedMesh); var newPos = Welded(flareMesh);
                var mapped = new ClothSkinningCoefficient[newPos.Count];
                for (int i = 0; i < mapped.Length; i++)
                {
                    int best = 0; float bd = float.MaxValue;
                    for (int j = 0; j < oldPos.Count; j++) { float d = (oldPos[j] - newPos[i]).sqrMagnitude; if (d < bd) { bd = d; best = j; } }
                    mapped[i] = coefficients[best];
                }
                smr.sharedMesh = flareMesh; coefficients = mapped;
            }
            restZ = Welded(smr.sharedMesh).Select(v => v.z).ToArray();
            baseCoefficients = (ClothSkinningCoefficient[])coefficients.Clone(); baseMax = coefficients.Max(c => c.maxDistance);
            cloth.coefficients = RampedCoefficients();
            cloth.sphereColliders = useColliders ? BuildThighColliders(original) : new ClothSphereColliderPair[0];
            if (damping >= 0f) cloth.damping = damping;
            if (stretching >= 0f) cloth.stretchingStiffness = stretching;
            if (bending >= 0f) cloth.bendingStiffness = bending;
            if (worldVelocityScale >= 0f) cloth.worldVelocityScale = worldVelocityScale;
            if (worldAccelerationScale >= 0f) cloth.worldAccelerationScale = worldAccelerationScale;
            cloth.enabled = true;
        }
        if (clothOff) cloth.enabled = false;   // skin only: measures the skinned garment against the leg with no simulation
        hauberkMaterial = smr.material;   // an instance: the shared production material stays untouched
        SetDoubleSided(doubleSided);
        cam = Camera.main != null ? Camera.main : new GameObject("Camera").AddComponent<Camera>();
        cam.nearClipPlane = 0.02f;
        if (probe) SetUpProbe(smr);
        lastAllow = allowanceScale; lastPin = pinAbove;
        Restart();
    }

    float[] restZ; ClothSkinningCoefficient[] baseCoefficients; float baseMax;

    ClothSkinningCoefficient[] RampedCoefficients()
    {
        var co = (ClothSkinningCoefficient[])baseCoefficients.Clone();
        for (int i = 0; i < co.Length; i++)
        {
            if (co[i].maxDistance <= 0f) continue;   // torso, sleeves and the pinned top stay on the animation
            float t = Mathf.Clamp01((pinAbove - restZ[i]) / rampWidth);
            co[i].maxDistance = t * t * (3f - 2f * t) * baseMax * allowanceScale;
        }
        return co;
    }

    // live tuning panel (play mode only): changes apply at once; "Save settings" writes what you settled on next to the test results
    bool panel = true; float lastAllow, lastPin;
    void Panel()
    {
        if (probe || cloth == null || baseCoefficients == null) return;
        GUILayout.BeginArea(new Rect(Screen.width - 330, 8, 320, 330), GUI.skin.box);
        panel = GUILayout.Toggle(panel, "tuning panel (T)");
        if (panel)
        {
            allowanceScale = Slider("hem allowance", allowanceScale, 0f, 1f);
            pinAbove = Slider("pin height (z)", pinAbove, 0.70f, 0.93f);
            cloth.damping = Slider("damping", cloth.damping, 0f, 1f);
            cloth.stretchingStiffness = Slider("stretching stiffness", cloth.stretchingStiffness, 0f, 1f);
            cloth.bendingStiffness = Slider("bending stiffness", cloth.bendingStiffness, 0f, 1f);
            float w = Slider("world velocity/accel scale", cloth.worldVelocityScale, 0f, 1f); cloth.worldVelocityScale = w; cloth.worldAccelerationScale = w;
            if (!Mathf.Approximately(allowanceScale, lastAllow) || !Mathf.Approximately(pinAbove, lastPin)) { cloth.coefficients = RampedCoefficients(); lastAllow = allowanceScale; lastPin = pinAbove; }
            if (GUILayout.Button("Save settings")) SaveSettings();
        }
        GUILayout.EndArea();
    }

    static float Slider(string label, float v, float a, float b)
    {
        GUILayout.BeginHorizontal(); GUILayout.Label(label + " " + v.ToString("0.00"), GUILayout.Width(190)); v = GUILayout.HorizontalSlider(v, a, b); GUILayout.EndHorizontal(); return v;
    }

    void SaveSettings()
    {
        string dir = Path.Combine(Application.dataPath, "../ClothDebugHandoff/hauberk_fit"); Directory.CreateDirectory(dir);
        string text = "allowanceScale=" + allowanceScale + "\npinAbove=" + pinAbove + "\ndamping=" + cloth.damping + "\nstretching=" + cloth.stretchingStiffness + "\nbending=" + cloth.bendingStiffness + "\nworldScale=" + cloth.worldVelocityScale + "\n";
        // the collider spheres the cloth actually uses, as they are right now (so edits made to them in play mode are not lost): bone, position in the bone's space, radius, and which spheres are paired
        var pairs = cloth.sphereColliders;
        for (int p = 0; p < pairs.Length; p++)
            foreach (var sc in new[] { pairs[p].first, pairs[p].second })
            {
                if (sc == null) continue;
                Transform bone = sc.transform; while (bone != null && !bone.name.EndsWith("Leg") && !bone.name.EndsWith("Hips") && !bone.name.EndsWith("Spine")) bone = bone.parent;
                var local = bone != null ? bone.InverseTransformPoint(sc.transform.TransformPoint(sc.center)) : sc.transform.TransformPoint(sc.center);
                text += "pair" + p + " bone=" + (bone != null ? bone.name : "?") + " boneLocal=(" + local.x.ToString("F4") + "," + local.y.ToString("F4") + "," + local.z.ToString("F4") + ") worldRadiusMm=" + (sc.radius * sc.transform.lossyScale.x * 1000f).ToString("F1") + "\n";
            }
        File.WriteAllText(Path.Combine(dir, "user_tuned_settings.txt"), text); Debug.Log("Saved settings:\n" + text);
    }

    static List<Vector3> Welded(Mesh m)
    {
        var seen = new HashSet<(int, int, int)>(); var list = new List<Vector3>();
        foreach (var v in m.vertices) if (seen.Add((Mathf.RoundToInt(v.x * 10000f), Mathf.RoundToInt(v.y * 10000f), Mathf.RoundToInt(v.z * 10000f)))) list.Add(v);
        return list;
    }

    ClothSphereColliderPair[] BuildThighColliders(ClothSphereColliderPair[] original)
    {
        int layer = original != null && original.Length > 0 && original[0].first != null ? original[0].first.gameObject.layer : 0;
        var pairs = new List<ClothSphereColliderPair>();
        foreach (string side in new[] { "LeftUpperLeg", "RightUpperLeg" })
        {
            var bone = character.GetComponentsInChildren<Transform>(true).First(t => t.name == side);
            float mx = side.StartsWith("Left") ? -1f : 1f;
            var holder = new GameObject("ThighCollider " + side).transform; holder.SetParent(bone, false);
            SphereCollider Make(Vector3 p, float r)
            {
                var g = new GameObject("s").transform; g.SetParent(holder, false); g.localPosition = new Vector3(signedX ? (side.StartsWith("Left") ? p.x : -p.x) : mx * Mathf.Abs(p.x), p.y, p.z); g.gameObject.layer = layer;
                var sc = g.gameObject.AddComponent<SphereCollider>(); sc.radius = r / bone.lossyScale.x; return sc;
            }
            if (chainCentres != null && chainCentres.Length >= 2 && chainRadii != null && chainRadii.Length == chainCentres.Length)
            {
                // a chain of spheres down the thigh, each neighbouring pair joined by a cone (thigh-local, left side, x mirrored on the right)
                var spheres = new SphereCollider[chainCentres.Length];
                for (int i = 0; i < spheres.Length; i++) spheres[i] = Make(chainCentres[i], chainRadii[i]);
                for (int i = 0; i + 1 < spheres.Length; i++) pairs.Add(new ClothSphereColliderPair(spheres[i], spheres[i + 1]));
            }
            else pairs.Add(new ClothSphereColliderPair(Make(fitA, fitRa), Make(fitB, fitRb)));
        }
        return pairs.ToArray();
    }

    void Restart()
    {
        clock = 0f; phase = "";
        character.transform.position = Vector3.zero; character.transform.rotation = Quaternion.identity;
        animator.Rebind(); animator.Update(0f); animator.SetFloat("ForwardMovementSpeed", 0f);
        if (cloth != null) cloth.ClearTransformMotion();
    }

    void Update()
    {
        var kb = UnityEngine.InputSystem.Keyboard.current;
        if (kb != null)
        {
            if (kb.spaceKey.wasPressedThisFrame) Restart();
            if (kb.cKey.wasPressedThisFrame) SetDoubleSided(!doubleSided);
            if (kb.tKey.wasPressedThisFrame) panel = !panel;
            var digits = new[] { kb.digit0Key, kb.digit1Key, kb.digit2Key, kb.digit3Key, kb.digit4Key, kb.digit5Key };
            for (int i = 0; i < digits.Length; i++) if (digits[i].wasPressedThisFrame) view = i;
        }
        float total = idleSeconds + walkSeconds + runSeconds + stopSeconds + lungeSeconds;
        clock += Time.deltaTime;
        if (clock >= total) { if (probe) { FinishProbe(); return; } Restart(); return; }
        float speed = 0f; string next;
        if (clock < idleSeconds) next = "idle";
        else if (clock < idleSeconds + walkSeconds) { next = "walk"; speed = walkSpeed; }
        else if (clock < idleSeconds + walkSeconds + runSeconds) { next = "run"; speed = runSpeed; }
        else if (clock < idleSeconds + walkSeconds + runSeconds + stopSeconds) next = "stop";
        else next = "lunge";
        if (next != phase) { phase = next; if (phase == "lunge") animator.Play("Longsword_A3", animator.GetLayerIndex("Attack"), 0f); }
        animator.SetFloat("ForwardMovementSpeed", speed);
        character.transform.position += character.transform.forward * (speed * Time.deltaTime);
        if (probe && !probeDone && frameCount % probeEvery == 0) StartCoroutine(ProbeAtEndOfFrame(frameCount, phase));
        frameCount++;
    }

    void LateUpdate()
    {
        if (probe || view <= 0 || cam == null) return;
        var v = Views[view - 1];
        cam.transform.position = character.transform.TransformPoint(v.pos); cam.transform.LookAt(character.transform.TransformPoint(v.target)); cam.fieldOfView = v.fov;
    }

    void OnGUI()
    {
        Panel();
        GUI.Label(new Rect(10, 8, 800, 22), "phase: " + phase + "   view: " + (view > 0 ? Views[view - 1].name : "free") + "   double sided: " + (doubleSided ? "ON" : "off") + "   [1 front  2 side  3 rear  4 seat  5 front thigh  0 free camera  C double sided  Space restart]");
    }

    // ---- exposure probe ----------------------------------------------------------------------------------------------------------------------------------

    SkinnedMeshRenderer hauberkSmr, skinCopy; Renderer breechesRenderer; Renderer[] otherRenderers;
    Material flatRed, flatGreen, flatGray; RenderTexture rt; Camera probeCam; Texture2D readTex;
    readonly List<(int frame, string phase, string view, int exposed, int core, int skin)> rows = new List<(int, string, string, int, int, int)>();
    readonly List<(float score, string name, Texture2D tex)> worst = new List<(float, string, Texture2D)>();
    bool probeDone;

    void SetUpProbe(SkinnedMeshRenderer hauberk)
    {
        hauberkSmr = hauberk;
        var copy = Instantiate(hauberk.gameObject, hauberk.transform.parent); copy.name = "SkinOnlyCopy";
        DestroyImmediate(copy.GetComponent<Cloth>());
        skinCopy = copy.GetComponent<SkinnedMeshRenderer>(); skinCopy.enabled = false; skinCopy.updateWhenOffscreen = true;
        breechesRenderer = character.GetComponentsInChildren<SkinnedMeshRenderer>(true).First(r => r.name.Contains("Breeches"));
        otherRenderers = character.GetComponentsInChildren<Renderer>(true).Where(r => r != hauberkSmr && r != skinCopy && r != breechesRenderer).ToArray();
        var shader = Shader.Find("Universal Render Pipeline/Unlit");
        Material Flat(Color c) { var m = new Material(shader); m.SetColor("_BaseColor", c); m.SetFloat("_Cull", 2f); return m; }   // back faces culled like the game's material: the far panel seen through a slit is not "covered"
        flatRed = Flat(Color.red); flatGreen = Flat(Color.green); flatGray = Flat(new Color(0.4f, 0.4f, 0.4f));
        probeCam = new GameObject("ProbeCamera").AddComponent<Camera>(); probeCam.enabled = false; probeCam.clearFlags = CameraClearFlags.SolidColor; probeCam.backgroundColor = Color.black;
        probeCam.nearClipPlane = 0.02f; probeCam.allowMSAA = false;
        rt = new RenderTexture(probeSize, probeSize, 24, RenderTextureFormat.ARGB32); rt.antiAliasing = 1; probeCam.targetTexture = rt;
        readTex = new Texture2D(probeSize, probeSize, TextureFormat.RGBA32, false);
        Directory.CreateDirectory(probeDir);
        // welded particle -> first mesh vertex, to compare the simulated particles with the skinned (cloth-off) pose
        var seen = new HashSet<(int, int, int)>(); var map = new List<int>(); var mv = hauberk.sharedMesh.vertices;
        for (int i = 0; i < mv.Length; i++) { var v = mv[i]; if (seen.Add((Mathf.RoundToInt(v.x * 10000f), Mathf.RoundToInt(v.y * 10000f), Mathf.RoundToInt(v.z * 10000f)))) map.Add(i); }
        weldMap = map.ToArray(); swayBake = new Mesh();
    }

    int[] weldMap; Mesh swayBake; readonly List<(int frame, string phase, float swayMm, float riseMm, float meanMaxMm)> sway = new List<(int, string, float, float, float)>();

    void RecordSway(int frame, string ph)
    {
        if (cloth == null || !cloth.enabled || skinCopy == null) { sway.Add((frame, ph, 0f, 0f, 0f)); return; }
        var wasEnabled = skinCopy.enabled; skinCopy.enabled = true; skinCopy.BakeMesh(swayBake); skinCopy.enabled = wasEnabled;
        var sv = swayBake.vertices; var cv = cloth.vertices; var co = cloth.coefficients;
        var ct = cloth.transform; var st = skinCopy.transform;
        // the cloth read-back and the baked skinned mesh need not share an origin: the pinned particles follow the skin exactly, so their mean offset is the frame mismatch and is removed
        var offset = Vector3.zero; int pinned = 0;
        for (int i = 0; i < cv.Length && i < weldMap.Length; i++)
            if (co[i].maxDistance <= 0f) { offset += ct.TransformPoint(cv[i]) - st.TransformPoint(sv[weldMap[i]]); pinned++; }
        if (pinned > 0) offset /= pinned;
        int n = 0; float sum = 0f, rise = 0f, maxSum = 0f;
        for (int i = 0; i < cv.Length && i < weldMap.Length; i++)
        {
            if (co[i].maxDistance <= 0f) continue;
            var d = ct.TransformPoint(cv[i]) - st.TransformPoint(sv[weldMap[i]]) - offset;
            sum += d.magnitude * 1000f; rise += Mathf.Max(0f, d.y) * 1000f; maxSum += co[i].maxDistance * 1000f; n++;
        }
        sway.Add((frame, ph, n > 0 ? sum / n : 0f, n > 0 ? rise / n : 0f, n > 0 ? maxSum / n : 0f));
    }

    static Material[] Fill(Material m, int n) { var a = new Material[n]; for (int i = 0; i < n; i++) a[i] = m; return a; }

    Color32[] Capture()
    {
        probeCam.Render();
        var prev = RenderTexture.active; RenderTexture.active = rt;
        readTex.ReadPixels(new Rect(0, 0, probeSize, probeSize), 0, 0); readTex.Apply(false); RenderTexture.active = prev;
        return readTex.GetPixels32();
    }

    IEnumerator ProbeAtEndOfFrame(int frame, string ph)
    {
        yield return new WaitForEndOfFrame();
        if (probeDone) yield break;
        if (System.Array.IndexOf(litFrames, frame) >= 0)
        {
            // what the eye sees at the same frame and cameras, in the game's own materials (back faces culled unless doubleSided is on)
            probeCam.backgroundColor = new Color(0.12f, 0.14f, 0.17f);
            foreach (int vi in new[] { 3, 4 })
            {
                var lv = Views[vi]; probeCam.transform.position = character.transform.TransformPoint(lv.pos); probeCam.transform.LookAt(character.transform.TransformPoint(lv.target)); probeCam.fieldOfView = lv.fov;
                Capture(); File.WriteAllBytes(Path.Combine(probeDir, "lit_" + ph + "_f" + frame.ToString("D4") + "_" + lv.name.Replace(' ', '_') + ".png"), readTex.EncodeToPNG());
            }
            probeCam.backgroundColor = Color.black;
        }
        RecordSway(frame, ph);
        var origHauberk = hauberkSmr.sharedMaterials; var origBreeches = breechesRenderer.sharedMaterials; var origOthers = otherRenderers.Select(r => r.sharedMaterials).ToArray();
        breechesRenderer.sharedMaterials = Fill(flatRed, origBreeches.Length);
        for (int i = 0; i < otherRenderers.Length; i++) otherRenderers[i].sharedMaterials = Fill(flatGray, origOthers[i].Length);

        var views = new List<(string name, Vector3 pos, Vector3 target, float fov)>();
        for (int a = 0; a < 8; a++) { float r = a * Mathf.PI / 4f; views.Add(("ring" + a * 45, new Vector3(Mathf.Sin(r) * 1.5f, 1.0f, Mathf.Cos(r) * 1.5f), new Vector3(0, 0.8f, 0), 38f)); }
        views.Add(("seat", Views[3].pos, Views[3].target, Views[3].fov)); views.Add(("frontThigh", Views[4].pos, Views[4].target, Views[4].fov));

        foreach (var v in views)
        {
            probeCam.transform.position = character.transform.TransformPoint(v.pos); probeCam.transform.LookAt(character.transform.TransformPoint(v.target)); probeCam.fieldOfView = v.fov;
            hauberkSmr.enabled = true; skinCopy.enabled = false; hauberkSmr.sharedMaterials = Fill(flatGreen, origHauberk.Length);
            var clothPass = Capture();
            // the skinned garment alone: where it would be seen if the leg did not hide it. A leg in front of the garment (the skinned pose itself inside the leg) must count as exposure.
            hauberkSmr.enabled = false; skinCopy.enabled = true; skinCopy.sharedMaterials = Fill(flatGreen, origHauberk.Length);
            var otherWasEnabled = otherRenderers.Select(o => o.enabled).ToArray(); bool breechesWasEnabled = breechesRenderer.enabled;
            breechesRenderer.enabled = false; foreach (var o in otherRenderers) o.enabled = false;
            var skinPass = Capture();
            breechesRenderer.enabled = breechesWasEnabled; for (int i = 0; i < otherRenderers.Length; i++) otherRenderers[i].enabled = otherWasEnabled[i];
            // the leg as it shows with the skinned garment (cloth off): slits and the skinned pose's own overlap are the designed baseline, not a cloth defect
            hauberkSmr.enabled = false; skinCopy.enabled = true;
            var skinWithLeg = Capture();
            skinCopy.enabled = false; hauberkSmr.enabled = true;

            int n = probeSize, exposed = 0, skin = 0; var mask = new bool[n * n];
            for (int i = 0; i < clothPass.Length; i++)
            {
                bool skinGreen = skinPass[i].g > 150 && skinPass[i].r < 90 && skinPass[i].b < 90;
                bool legRed = clothPass[i].r > 150 && clothPass[i].g < 90 && clothPass[i].b < 90;
                bool legRedInSkin = skinWithLeg[i].r > 150 && skinWithLeg[i].g < 90 && skinWithLeg[i].b < 90;
                if (skinGreen) skin++;
                if (skinGreen && legRed && !legRedInSkin) { exposed++; mask[i] = true; }   // leg visible with the cloth where it is not visible with the skinned garment
            }
            int core = 0; const int h = 2;   // exposed pixels inside a 5x5 block that is all exposed: holes at least ~5 px wide, not slivers along the hem
            if (exposed > 0)
                for (int y = h; y < n - h; y++) for (int x = h; x < n - h; x++)
                {
                    if (!mask[y * n + x]) continue;
                    bool all = true;
                    for (int dy = -h; dy <= h && all; dy++) for (int dx = -h; dx <= h; dx++) if (!mask[(y + dy) * n + x + dx]) { all = false; break; }
                    if (all) core++;
                }
            rows.Add((frame, ph, v.name, exposed, core, skin));
            if (core > 0) KeepWorst(frame, ph, v.name, core, skin, clothPass, skinPass, mask);
        }
        hauberkSmr.sharedMaterials = origHauberk; breechesRenderer.sharedMaterials = origBreeches;
        for (int i = 0; i < otherRenderers.Length; i++) otherRenderers[i].sharedMaterials = origOthers[i];
    }

    void KeepWorst(int frame, string ph, string viewName, int core, int skin, Color32[] clothPass, Color32[] skinPass, bool[] mask)
    {
        float score = core;
        if (worst.Count >= probeShots && score <= worst.Min(w => w.score)) return;
        var tex = new Texture2D(probeSize, probeSize, TextureFormat.RGB24, false); var px = new Color32[clothPass.Length];
        for (int i = 0; i < px.Length; i++)
        {
            if (mask[i]) px[i] = new Color32(255, 40, 40, 255);                                   // leg showing where the skinned hauberk would cover it
            else if (clothPass[i].g > 150 && clothPass[i].r < 90) px[i] = new Color32(70, 130, 70, 255);  // cloth
            else if (clothPass[i].r > 150 && clothPass[i].g < 90) px[i] = new Color32(110, 70, 40, 255);  // leg, not under the skinned hauberk
            else if (clothPass[i].r > 90) px[i] = new Color32(90, 90, 90, 255);                    // rest of the body
            else px[i] = new Color32(15, 15, 20, 255);
        }
        tex.SetPixels32(px); tex.Apply(false);
        worst.Add((score, ph + "_f" + frame.ToString("D4") + "_" + viewName + "_core" + core, tex));
        if (worst.Count > probeShots) { int k = worst.IndexOf(worst.OrderBy(w => w.score).First()); Destroy(worst[k].tex); worst.RemoveAt(k); }
    }

    void FinishProbe()
    {
        if (probeDone) return;
        probeDone = true;
        var sb = new StringBuilder("frame,phase,view,exposed_px,core_px,skin_px\n");
        foreach (var r in rows) sb.Append(r.frame).Append(',').Append(r.phase).Append(',').Append(r.view).Append(',').Append(r.exposed).Append(',').Append(r.core).Append(',').Append(r.skin).Append('\n');
        File.WriteAllText(Path.Combine(probeDir, "exposure.csv"), sb.ToString());
        var sw = new StringBuilder("frame,phase,mean_distance_from_skin_mm,mean_rise_above_skin_mm,mean_leash_mm\n");
        foreach (var s in sway) sw.Append(s.frame).Append(',').Append(s.phase).Append(',').Append(s.swayMm.ToString("F2")).Append(',').Append(s.riseMm.ToString("F2")).Append(',').Append(s.meanMaxMm.ToString("F1")).Append('\n');
        File.WriteAllText(Path.Combine(probeDir, "sway.csv"), sw.ToString());
        foreach (var w in worst) File.WriteAllBytes(Path.Combine(probeDir, w.name + ".png"), w.tex.EncodeToPNG());
        File.WriteAllText(Path.Combine(probeDir, "DONE.txt"), "done " + rows.Count);
    }
}
