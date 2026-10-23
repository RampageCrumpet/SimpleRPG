using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace SimpleRPG.EditorTools
{
    /// <summary>Adapts the supplied exports to the gameplay rig without humanoid translation loss.</summary>
    public static class SkeletonGameplayClips
    {
        private const string Folder = "Assets/Prefabs/Characters/Enemies";

        [MenuItem("SimpleRPG/Rebuild Skeleton Gameplay Clips")]
        public static void Rebuild()
        {
            RebuildClips(new[] { "Idle_Blocking", "Walk", "Ready", "Overhead", "Slash", "Die", "Rise" });
        }

        public static void RebuildApprovedOverhead() => RebuildClips(new[] { "Ready", "Overhead" });

        public static void RebuildApprovedSlash() => RebuildClips(new[] { "Slash" });

        public static void RebuildApprovedWalk() => RebuildClips(new[] { "Walk" });

        public static void RebuildApprovedDeath() => RebuildClips(new[] { "Die" });

        private static void RebuildClips(string[] names)
        {
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(Folder + "/SkeletonFootman.controller");
            foreach (string name in names)
            {
                string sourcePath = "Assets/Art/Animations/Skeleton/Skeleton_" + name + ".fbx";
                string originalImport = System.IO.File.ReadAllText(sourcePath + ".meta");
                var importer = (ModelImporter)AssetImporter.GetAtPath(sourcePath);
                try
                {
                    importer.animationType = ModelImporterAnimationType.Generic;
                    importer.avatarSetup = ModelImporterAvatarSetup.NoAvatar;
                    importer.motionNodeName = "Root";
                    importer.SaveAndReimport();
                    foreach (var source in AssetDatabase.LoadAllAssetsAtPath(importer.assetPath).OfType<AnimationClip>().Where(c => !c.name.StartsWith("__")))
                    {
                        bool pieces = name == "Die" || name == "Rise";
                        var clip = new AnimationClip { name = source.name, frameRate = source.frameRate };
                        AnimationUtility.SetAnimationClipSettings(clip, AnimationUtility.GetAnimationClipSettings(source));
                        AnimationUtility.SetAnimationEvents(clip, AnimationUtility.GetAnimationEvents(source));
                        foreach (var binding in AnimationUtility.GetCurveBindings(source))
                        {
                            if (binding.type != typeof(Transform)) continue;
                            // Disassembly exports contain an extra identity wrapper. The rig's axis conversion stays intact.
                            if (pieces && binding.path == "Skeleton_Rig") continue;
                            var destination = binding;
                            if (!pieces) destination.path = string.IsNullOrEmpty(binding.path) ? "Skeleton_Rig" : "Skeleton_Rig/" + binding.path;
                            if (binding.propertyName.StartsWith("m_LocalScale")) continue;
                            var curve = AnimationUtility.GetEditorCurve(source, binding);
                            if (!pieces && binding.propertyName.StartsWith("m_LocalPosition"))
                            {
                                var keys = curve.keys;
                                // Approved Blender exports use metres. Older Cascadeur sources
                                // keep centimetres beneath a 100x model root.
                                float units = name == "Walk" || name == "Overhead" || name == "Slash" ? 1f : 100f;
                                for (int k = 0; k < keys.Length; k++)
                                {
                                    keys[k].value *= units;
                                    keys[k].inTangent *= units;
                                    keys[k].outTangent *= units;
                                    if (name == "Walk" && binding.path == "Root/Hips" && binding.propertyName == "m_LocalPosition.z")
                                    {
                                        // The refined source starts at cycle zero. Navigation
                                        // supplies its 0.8m of travel; retain the authored sway.
                                        keys[k].value -= keys[k].time * (0.8f / source.length);
                                        keys[k].inTangent -= 0.8f / source.length;
                                        keys[k].outTangent -= 0.8f / source.length;
                                    }
                                }
                                curve.keys = keys;
                            }
                            AnimationUtility.SetEditorCurve(clip, destination, curve);
                        }
                        string path = Folder + "/Skeleton_" + source.name + ".anim";
                        var existing = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
                        if (existing == null) { AssetDatabase.CreateAsset(clip, path); existing = clip; }
                        else { EditorUtility.CopySerialized(clip, existing); Object.DestroyImmediate(clip); }
                        foreach (var state in controller.layers.SelectMany(l => l.stateMachine.states.Select(s => s.state)))
                            if (state.motion != null && state.motion.name == source.name) { state.motion = existing; EditorUtility.SetDirty(state); }
                    }
                }
                finally
                {
                    // Keep the original reusable humanoid exports and their preview controller intact.
                    System.IO.File.WriteAllText(sourcePath + ".meta", originalImport);
                    AssetDatabase.ImportAsset(sourcePath, ImportAssetOptions.ForceUpdate);
                }
            }
            if (names.Contains("Ready")) MatchApprovedReadyArm();
            if (names.Contains("Walk"))
            {
                ConfigureWalkWeaponLayer(controller);
                ConfigureWalkWeaponLayer(AssetDatabase.LoadAssetAtPath<AnimatorController>("Assets/Art/Animations/Skeleton/Skeleton_Test.controller"));
            }
            EditorUtility.SetDirty(controller);
            AssetDatabase.SaveAssets();
        }

        private static void ConfigureWalkWeaponLayer(AnimatorController controller)
        {
            var baseWalk = controller.layers[0].stateMachine.states.First(s => s.state.name == "Walk").state;
            baseWalk.speed = 1;
            baseWalk.speedParameterActive = false;
            EditorUtility.SetDirty(baseWalk);
            var machine = controller.layers.First(l => l.name == "Weapon_Arm").stateMachine;
            var ready = machine.states.First(s => s.state.name == "Ready").state;
            var walk = machine.states.Select(s => s.state).FirstOrDefault(s => s.name == "Walk")
                ?? machine.AddState("Walk", new Vector3(450, 100));
            walk.motion = baseWalk.motion;
            walk.speed = 1;
            walk.writeDefaultValues = true;
            // Use the authored walk arm instead of masking it with a static Ready.
            // Identical conditions and fixed blend times keep the base and arm in phase.
            void Transition(AnimatorState from, AnimatorState to, AnimatorConditionMode condition)
            {
                var transition = from.transitions.FirstOrDefault(t => t.destinationState == to
                    && t.conditions.Any(c => c.parameter == "Walk" && c.mode == condition));
                if (transition == null)
                {
                    transition = from.AddTransition(to);
                    transition.AddCondition(condition, 0, "Walk");
                }
                transition.hasExitTime = false;
                transition.hasFixedDuration = true;
                transition.duration = .25f;
                EditorUtility.SetDirty(transition);
            }
            Transition(ready, walk, AnimatorConditionMode.If);
            Transition(walk, ready, AnimatorConditionMode.IfNot);
            EditorUtility.SetDirty(walk);
            EditorUtility.SetDirty(controller);
        }

        private static void MatchApprovedReadyArm()
        {
            var ready = AssetDatabase.LoadAssetAtPath<AnimationClip>(Folder + "/Skeleton_Ready.anim");
            var overhead = AssetDatabase.LoadAssetAtPath<AnimationClip>(Folder + "/Skeleton_Overhead.anim");
            var scene = UnityEditor.SceneManagement.EditorSceneManager.NewPreviewScene();
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Folder + "/SkeletonFootman.prefab"), scene);
            var targetRotations = new System.Collections.Generic.Dictionary<string, Quaternion>();
            try
            {
                instance.GetComponent<Animator>().enabled = false;
                overhead.SampleAnimation(instance, 0);
                foreach (var bone in instance.GetComponentsInChildren<Transform>()) targetRotations[bone.name] = bone.localRotation;
            }
            finally
            {
                Object.DestroyImmediate(instance);
                UnityEditor.SceneManagement.EditorSceneManager.ClosePreviewScene(scene);
            }
            foreach (string bone in new[] { "RightUpperArm", "RightLowerArm", "RightHand" })
            {
                string path = AnimationUtility.GetCurveBindings(ready).First(b => b.path.EndsWith("/" + bone)).path;
                AnimationCurve[] curves = "xyzw".Select(axis => AnimationUtility.GetEditorCurve(ready,
                    EditorCurveBinding.FloatCurve(path, typeof(Transform), "m_LocalRotation." + axis))).ToArray();
                Quaternion Read(AnimationCurve[] c, float t) => new Quaternion(c[0].Evaluate(t), c[1].Evaluate(t), c[2].Evaluate(t), c[3].Evaluate(t)).normalized;
                Quaternion delta = targetRotations[bone] * Quaternion.Inverse(Read(curves, 0));
                var output = new[] { new AnimationCurve(), new AnimationCurve(), new AnimationCurve(), new AnimationCurve() };
                Quaternion previous = Quaternion.identity;
                int count = Mathf.RoundToInt(ready.length * ready.frameRate);
                for (int frame = 0; frame <= count; frame++)
                {
                    float time = frame / ready.frameRate;
                    Quaternion q = (delta * Read(curves, time)).normalized;
                    if (frame > 0 && Quaternion.Dot(previous, q) < 0) q = new Quaternion(-q.x, -q.y, -q.z, -q.w);
                    float[] values = { q.x, q.y, q.z, q.w };
                    for (int axis = 0; axis < 4; axis++) output[axis].AddKey(time, values[axis]);
                    previous = q;
                }
                foreach (var binding in AnimationUtility.GetCurveBindings(ready).Where(b => b.path == path && b.propertyName.Contains("Euler")))
                    AnimationUtility.SetEditorCurve(ready, binding, null);
                for (int axis = 0; axis < 4; axis++)
                {
                    for (int key = 0; key < output[axis].length; key++)
                    {
                        AnimationUtility.SetKeyLeftTangentMode(output[axis], key, AnimationUtility.TangentMode.Linear);
                        AnimationUtility.SetKeyRightTangentMode(output[axis], key, AnimationUtility.TangentMode.Linear);
                    }
                    AnimationUtility.SetEditorCurve(ready, EditorCurveBinding.FloatCurve(path, typeof(Transform), "m_LocalRotation." + "xyzw"[axis]), output[axis]);
                }
            }
            EditorUtility.SetDirty(ready);
        }
    }
}
