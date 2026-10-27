using SimpleRPG.Appearance;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace SimpleRPG.AppearanceTools
{
    /// <summary>
    /// Turns the sliced human male model into the game's parts: one <see cref="BodyPartDefinition"/> per mesh piece and a bare rig prefab for
    /// characters to nest. Safe to run again after re-exporting the model: existing assets are updated in place, so ids and references survive.
    /// </summary>
    public static class HumanMaleSetup
    {
        private const string ModelPath = "Assets/Art/3d Models/Human/Human_Male.fbx";
        private const string EyebrowsPath = "Assets/Art/3d Models/Human/Human_Male_Eyebrows.fbx";
        public const string PartsFolder = "Assets/Art/BodyParts/HumanMale";
        public const string RigPrefabPath = "Assets/Prefabs/Characters/Human/HumanMale_Rig.prefab";
        private const string RigRootName = "Human_Male_Rig";
        private const string HeadBone = "Head";

        private static readonly Dictionary<string, BodyPartSlot> SlotsByPiece = new Dictionary<string, BodyPartSlot>
        {
            { "Human_Male_Head", BodyPartSlot.Head },
            { "Human_Male_Chest", BodyPartSlot.Torso },
            { "Human_Male_Pants", BodyPartSlot.Legs },
            { "Human_Male_Boot_L", BodyPartSlot.FootLeft },
            { "Human_Male_Boot_R", BodyPartSlot.FootRight },
            { "Human_Male_Glove_L", BodyPartSlot.HandLeft },
            { "Human_Male_Glove_R", BodyPartSlot.HandRight },
        };

        [MenuItem("SimpleRPG/Appearance/Build Human Male Parts And Rig")]
        public static void Build()
        {
            GameObject model = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
            GameObject eyebrows = AssetDatabase.LoadAssetAtPath<GameObject>(EyebrowsPath);
            Directory.CreateDirectory(PartsFolder);

            foreach (SkinnedMeshRenderer piece in model.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                if (!SlotsByPiece.TryGetValue(piece.name, out BodyPartSlot slot))
                {
                    Debug.LogWarning("[Appearance] No slot for model piece " + piece.name + "; skipped.");
                    continue;
                }

                string[] bones = piece.bones.Select(b => b.name).ToArray();
                Configure(piece.name, slot, piece.sharedMesh, piece.sharedMaterials, bones, piece.rootBone.name, piece.localBounds, "", Vector3.zero, Quaternion.identity, Vector3.one);
            }

            BuildEyebrows(model, eyebrows);
            BuildRigPrefab(model);
            AssetDatabase.SaveAssets();
            EditorApplication.ExecuteMenuItem("SimpleRPG/Definitions/Rebuild Definition Registry");
            Debug.Log("[Appearance] Human male parts and rig prefab are up to date.");
        }

        private static void BuildEyebrows(GameObject model, GameObject eyebrows)
        {
            Transform head = model.GetComponentsInChildren<Transform>(true).First(t => t.name == HeadBone);
            MeshFilter filter = eyebrows.GetComponentInChildren<MeshFilter>(true);
            MeshRenderer renderer = filter.GetComponent<MeshRenderer>();

            // The brows were modelled in the same space as the head, so their pose relative to the head bone is fixed once, here.
            Matrix4x4 local = head.worldToLocalMatrix * filter.transform.localToWorldMatrix;
            Configure(eyebrows.name, BodyPartSlot.Eyebrows, filter.sharedMesh, renderer.sharedMaterials, null, "", new Bounds(),
                HeadBone, local.GetPosition(), local.rotation, local.lossyScale);
        }

        private static void Configure(string name, BodyPartSlot slot, Mesh mesh, Material[] materials, string[] bones, string rootBone, Bounds bounds,
            string attachBone, Vector3 attachPosition, Quaternion attachRotation, Vector3 attachScale)
        {
            string path = PartsFolder + "/" + name + ".asset";
            BodyPartDefinition part = AssetDatabase.LoadAssetAtPath<BodyPartDefinition>(path);
            if (part == null)
            {
                part = ScriptableObject.CreateInstance<BodyPartDefinition>();
                part.EnsureId();
                AssetDatabase.CreateAsset(part, path);
            }

            part.Configure(slot, mesh, materials, bones, rootBone, bounds, attachBone, attachPosition, attachRotation, attachScale);
            EditorUtility.SetDirty(part);
        }

        // The rig is the model with its mesh pieces and own animator removed: the character owns the animator, and CharacterAppearance owns the meshes.
        private static void BuildRigPrefab(GameObject model)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(RigPrefabPath));
            GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(model);
            PrefabUtility.UnpackPrefabInstance(instance, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            instance.name = "HumanMale_Rig";

            foreach (SkinnedMeshRenderer piece in instance.GetComponentsInChildren<SkinnedMeshRenderer>(true).ToArray())
            {
                Object.DestroyImmediate(piece.gameObject);
            }

            Animator animator = instance.GetComponent<Animator>();
            if (animator != null) Object.DestroyImmediate(animator);

            if (instance.transform.Find(RigRootName) == null) Debug.LogError("[Appearance] The model has no " + RigRootName + " child.");
            PrefabUtility.SaveAsPrefabAsset(instance, RigPrefabPath);
            Object.DestroyImmediate(instance);
        }
    }
}
