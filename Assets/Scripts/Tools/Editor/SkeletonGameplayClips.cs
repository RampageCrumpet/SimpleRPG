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
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(Folder + "/SkeletonFootman.controller");
            foreach (string name in new[] { "Idle_Blocking", "Walk", "Ready", "Overhead", "Slash", "Die", "Rise" })
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
                                // Locomotion FBXs use centimetres inside their 100x model root.
                                for (int k = 0; k < keys.Length; k++)
                                {
                                    keys[k].value *= 100;
                                    keys[k].inTangent *= 100;
                                    keys[k].outTangent *= 100;
                                    if (name == "Walk" && binding.path == "Root/Hips" && binding.propertyName == "m_LocalPosition.z")
                                    {
                                        // The selected cycle starts after one 0.8m stride. Navigation supplies travel.
                                        keys[k].value -= 0.8f + keys[k].time * (0.8f / source.length);
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
            EditorUtility.SetDirty(controller);
            AssetDatabase.SaveAssets();
        }
    }
}
