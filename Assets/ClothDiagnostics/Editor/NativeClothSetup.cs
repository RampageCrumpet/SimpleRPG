using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace ClothDiagnostics
{
    /// <summary>Builds the native-Cloth test on a duplicate of the hauberk's renderer in the review scene. Nothing here touches the FBX, the mesh, the prefab or the part asset.</summary>
    public static class NativeClothSetup
    {
        public const string OutDir = "Assets/ClothDiagnostics/Results/";
        const string ControllerPath = "Assets/ClothDiagnostics/NativeClothTest.controller";
        const string RunClipFbx = "Assets/Blink/Art/Animations/Animations_Starter_Pack/Movement/RunForward.fbx";
        const string IdleClipFbx = "Assets/Blink/Art/Animations/Animations_Starter_Pack/Movement/Idle.fbx";

        public sealed class Config
        {
            public float fixedAbove = 0.93f;    // at and above this height every vertex follows the skin exactly
            public float freeBelow = 0.80f;     // below this the vertices have their full Max Distance
            public float hemMaxDistance = 0.15f;
            public float stretching = 1f, bending = 0.3f, damping = 0.3f, friction = 0.5f;
            public float worldVelocity = 0.3f, worldAcceleration = 0.3f, solverFrequency = 120f, sleep = 0f;
            public bool tethers = true;
        }

        static Transform FindDeep(Transform root, string name)
        {
            if (root.name == name) return root;
            foreach (Transform c in root) { var f = FindDeep(c, name); if (f != null) return f; }
            return null;
        }

        static float SegDist(Vector3 p, Vector3 a, Vector3 b)
        {
            Vector3 ab = b - a; float t = Mathf.Clamp01(Vector3.Dot(p - a, ab) / Mathf.Max(ab.sqrMagnitude, 1e-8f));
            return (p - (a + ab * t)).magnitude;
        }

        static AnimationClip LoadClip(string fbx) => AssetDatabase.LoadAllAssetsAtPath(fbx).OfType<AnimationClip>().First(c => !c.name.StartsWith("__preview"));


        /// <summary>
        /// The Max Distance each vertex needs so Cloth can always clear the leg: the deepest the plain skinned (reference) position is inside the tapered thigh collider
        /// over the whole run cycle and idle. A leash shorter than that makes the leash and the collider contradict each other.
        /// </summary>
        static float[] ComputeRequiredLeash(GameObject character, SkinnedMeshRenderer reference, Vector3[] V, bool[] skirt, List<SphereCollider> sa, List<SphereCollider> sb, System.Text.StringBuilder log)
        {
            var run = LoadClip(RunClipFbx); var idle = LoadClip(IdleClipFbx); var need = new float[V.Length]; var mesh = new Mesh(); int samples = 0;
            var prevAnimatorEnabled = character.GetComponent<Animator>().enabled; character.GetComponent<Animator>().enabled = false;
            void Sample(AnimationClip clip, float t)
            {
                clip.SampleAnimation(character, t); reference.BakeMesh(mesh, true); var local = mesh.vertices; samples++;
                for (int leg = 0; leg < sa.Count; leg++)
                {
                    Vector3 a = sa[leg].transform.TransformPoint(sa[leg].center), b = sb[leg].transform.TransformPoint(sb[leg].center); float ra = sa[leg].radius, rb = sb[leg].radius; Vector3 ab = b - a;
                    for (int i = 0; i < V.Length; i++)
                    {
                        if (!skirt[i]) continue; Vector3 w = reference.transform.TransformPoint(local[i]);
                        float u = Mathf.Clamp01(Vector3.Dot(w - a, ab) / Mathf.Max(ab.sqrMagnitude, 1e-8f)); float depth = Mathf.Lerp(ra, rb, u) - Vector3.Distance(w, a + ab * u);
                        if (depth > need[i]) need[i] = depth;
                    }
                }
            }
            Sample(idle, 0f); for (float t = 0f; t < run.length; t += 1f / 60f) Sample(run, t);
            idle.SampleAnimation(character, 0f); character.GetComponent<Animator>().enabled = prevAnimatorEnabled;
            int n = 0; float max = 0; foreach (int i in Enumerable.Range(0, V.Length).Where(i => skirt[i] && need[i] > 0.002f)) { n++; max = Mathf.Max(max, need[i]); }
            log.AppendLine($"required leash from the fitted thigh over {samples} poses: {n} skirt vertices must be able to move out of the leg; the deepest needs {max * 100f:F1} cm");
            return need;
        }

        public static string Build(Config cfg = null, Mesh variantMesh = null, ColliderStudy.Spec? sphereSpec = null)
        {
            cfg ??= new Config();
            Directory.CreateDirectory(OutDir);
            EditorApplication.ExecuteMenuItem("SimpleRPG/Items/Open Hauberk Cloth Review");
            var character = GameObject.Find("Character");
            SkinnedMeshRenderer original = null;
            foreach (var s in character.GetComponentsInChildren<SkinnedMeshRenderer>(true)) if (s.sharedMesh != null && s.sharedMesh.name == "GAME_MailHauberk") original = s;
            if (original == null) return "hauberk renderer not found";
            bool fittedSpheres = sphereSpec.HasValue; var log = new System.Text.StringBuilder(); float[] runner_required = null;

            // The original stays as the skinning-only reference; the Cloth goes on a duplicate of the renderer.
            var dupGo = Object.Instantiate(original.gameObject, original.transform.parent); dupGo.name = "Hauberk (native Cloth test)";
            dupGo.transform.SetPositionAndRotation(original.transform.position, original.transform.rotation);
            var dup = dupGo.GetComponent<SkinnedMeshRenderer>();
            original.name = "Hauberk (skinning only)"; original.forceRenderingOff = true;
            original.updateWhenOffscreen = true; dup.updateWhenOffscreen = true; // Cloth holds stale data while its renderer is not being updated

            // A variant mesh (a duplicate with different skin weights) is used by BOTH the Cloth renderer and the skinning-only reference, so the Cloth is judged against its own reference pose.
            if (variantMesh != null) { dup.sharedMesh = variantMesh; original.sharedMesh = variantMesh; log.AppendLine($"mesh variant: {variantMesh.name}"); }
            Mesh mesh = dup.sharedMesh; var V = mesh.vertices; var BW = mesh.boneWeights; var bind = mesh.bindposes; var bones = dup.bones;
            Matrix4x4 M = bones[0].localToWorldMatrix * bind[0];
            bool IsArm(int i)
            {
                var w = BW[i]; var list = new[] { (w.boneIndex0, w.weight0), (w.boneIndex1, w.weight1), (w.boneIndex2, w.weight2), (w.boneIndex3, w.weight3) }.OrderByDescending(p => p.Item2).First();
                string n = bones[list.Item1].name; return n.Contains("Arm") || n.Contains("Hand") || n.Contains("Shoulder") || n.Contains("Clavicle") || n.Contains("Thumb") || n.Contains("Index") || n.Contains("Middle") || n.Contains("Ring") || n.Contains("Pinky") || n.Contains("Little");
            }
            bool[] skirt = new bool[V.Length];
            for (int i = 0; i < V.Length; i++) skirt[i] = V[i].z < 0.99f && Mathf.Abs(V[i].x - 0.006f) < 0.30f && !IsArm(i);

            // Native Cloth settings. Max Distance is assigned per Cloth particle at run time (Cloth welds vertices that share a position,
            // so its arrays are indexed by particle, not by render vertex: assigning by render-vertex index lands on the wrong vertices).
            var cloth = dupGo.AddComponent<Cloth>();
            cloth.stretchingStiffness = cfg.stretching; cloth.bendingStiffness = cfg.bending; cloth.damping = cfg.damping; cloth.friction = cfg.friction;
            cloth.worldVelocityScale = cfg.worldVelocity; cloth.worldAccelerationScale = cfg.worldAcceleration; cloth.clothSolverFrequency = cfg.solverFrequency; cloth.sleepThreshold = cfg.sleep;
            cloth.useTethers = cfg.tethers; cloth.useGravity = true;
            log.AppendLine($"Cloth on the duplicate renderer: {V.Length} render vertices, {cloth.coefficients.Length} Cloth particles (fixedAbove {cfg.fixedAbove}, freeBelow {cfg.freeBelow}, hem {cfg.hemMaxDistance} m), tethers {cfg.tethers}");

            // Thigh capsules, sized from the rest geometry so nothing touches them at rest.
            var capsules = new List<CapsuleCollider>();
            foreach (string side in new[] { "Left", "Right" })
            {
                Transform thigh = FindDeep(character.transform, side + "UpperLeg"), knee = FindDeep(character.transform, side + "LowerLeg");
                Vector3 a = thigh.position, b = knee.position; float min = float.MaxValue;
                for (int i = 0; i < V.Length; i++) if (skirt[i] && V[i].z > 0.6f) min = Mathf.Min(min, SegDist(M.MultiplyPoint3x4(V[i]), a, b));
                float radius = min - 0.002f;
                Vector3 toKnee = thigh.InverseTransformPoint(knee.position); int axis = 0;
                for (int k = 1; k < 3; k++) if (Mathf.Abs(toKnee[k]) > Mathf.Abs(toKnee[axis])) axis = k;
                var cap = thigh.gameObject.AddComponent<CapsuleCollider>(); cap.direction = axis; cap.center = toKnee * 0.5f; cap.radius = radius / thigh.lossyScale.x; cap.height = toKnee.magnitude + 2f * cap.radius;
                capsules.Add(cap);
                log.AppendLine($"{side} thigh capsule: radius {radius:F3} m (smallest rest distance of any skirt vertex to the thigh), length {Vector3.Distance(a, b):F3} m, axis {axis}");
            }
            cloth.capsuleColliders = capsules.ToArray();
            var sphereA = new List<SphereCollider>(); var sphereB = new List<SphereCollider>();
            if (fittedSpheres)
            {
                // Thigh geometry measured from the leggings mesh: about 10.5 cm radius near the hip, 9.5 cm mid-thigh, 8.8 cm lower, narrowing to about 7 cm at the knee.
                foreach (string side in new[] { "Left", "Right" })
                {
                    Transform thigh = FindDeep(character.transform, side + "UpperLeg"), knee = FindDeep(character.transform, side + "LowerLeg"); Vector3 toKnee = thigh.InverseTransformPoint(knee.position);
                    var a = thigh.gameObject.AddComponent<SphereCollider>(); a.center = toKnee * sphereSpec.Value.t0; a.radius = sphereSpec.Value.r0 / thigh.lossyScale.x;
                    var b = thigh.gameObject.AddComponent<SphereCollider>(); b.center = toKnee * sphereSpec.Value.t1; b.radius = sphereSpec.Value.r1 / thigh.lossyScale.x;
                    sphereA.Add(a); sphereB.Add(b);
                    log.AppendLine($"{side} thigh sphere pair {sphereSpec.Value.name}: {sphereSpec.Value.r0 * 100f:F1} cm at {sphereSpec.Value.t0 * 100f:F0}% and {sphereSpec.Value.r1 * 100f:F1} cm at {sphereSpec.Value.t1 * 100f:F0}% of the thigh");
                }
            }

            if (fittedSpheres) runner_required = ComputeRequiredLeash(character, original, V, skirt, sphereA, sphereB, log);

            // Controller with the real Idle and RunForward clips.
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath) ?? AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
            var sm = controller.layers[0].stateMachine;
            foreach (var st in sm.states.ToArray()) sm.RemoveState(st.state);
            var idle = sm.AddState("Idle"); idle.motion = LoadClip(IdleClipFbx);
            var run = sm.AddState("Run"); run.motion = LoadClip(RunClipFbx);
            sm.defaultState = idle; EditorUtility.SetDirty(controller);
            var animator = character.GetComponent<Animator>(); animator.runtimeAnimatorController = controller; animator.applyRootMotion = false;

            var floor = GameObject.Find("Review floor"); if (floor != null) { floor.transform.localScale = Vector3.one * 6f; floor.transform.position = new Vector3(0f, 0f, 20f); }
            var camera = Camera.main; camera.orthographic = false;
            var holder = new GameObject("Native cloth test");
            var runner = holder.AddComponent<NativeClothRunner>();
            runner.skirtMask = skirt; runner.fixedAbove = cfg.fixedAbove; runner.freeBelow = cfg.freeBelow; runner.hemMax = cfg.hemMaxDistance; runner.cloth = cloth; runner.clothSmr = dup; runner.baseline = original; runner.animator = animator; runner.capsules = capsules.ToArray(); runner.sphereA = sphereA.ToArray(); runner.sphereB = sphereB.ToArray(); runner.useSpheres = fittedSpheres; runner.requiredLeash = runner_required; runner.thigh = new[] { FindDeep(character.transform, "LeftUpperLeg"), FindDeep(character.transform, "RightUpperLeg") }; runner.knee = new[] { FindDeep(character.transform, "LeftLowerLeg"), FindDeep(character.transform, "RightLowerLeg") }; runner.snapCamera = camera;
            log.AppendLine("controller: Idle (Idle.fbx) and Run (RunForward.fbx); characters stay in place.");
            Debug.Log(log.ToString());
            return log.ToString();
        }
    }
}
