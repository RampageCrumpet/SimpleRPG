using SimpleRPG.Appearance;
using SimpleRPG.InventorySystem;
using SimpleRPG.ObjectInteractions;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Unity.Netcode;
using UnityEditor;
using UnityEngine;

namespace SimpleRPG.AppearanceTools
{
    /// <summary>
    /// Gives each piece of armor its world form: the pickup that lies on the ground when it is dropped, left by an enemy or placed in the dungeon.
    /// <para>
    /// By default the form is <b>generated</b> from the piece's worn art: the parts that dress the item are put on the body, baked, and folded into
    /// something that lies on a floor (see <see cref="GarmentFold"/>), not left in the pose of someone wearing them. A piece the artist has made a
    /// proper model for is <b>authored</b>: put a prefab named like the item in <see cref="OverridesFolder"/> and it is used in place of the
    /// generated model. Either way the pickup is the ordinary <see cref="WorldItemPickup"/>, so the very item instance and its affixes travel
    /// through a drop and a pickup unchanged.
    /// </para>
    /// Safe to run again: an item that already has a world form is left alone.
    /// </summary>
    public static class ArmorWorldFormSetup
    {
        public const string FormsFolder = "Assets/Art/WorldForms";
        public const string OverridesFolder = "Assets/Art/WorldForms/Overrides";
        public const string PrefabsFolder = "Assets/Prefabs/Interactables/Armor";
        private const string RigRootName = "Human_Male_Rig";
        private const string NetworkPrefabsPath = "Assets/DefaultNetworkPrefabs.asset";

        /// <summary>Head pieces stand on the floor; everything else is folded.</summary>
        public static bool IsFolded(EquipmentSlot slot) => slot != EquipmentSlot.head;

        [MenuItem("SimpleRPG/Items/Build Armor World Forms")]
        public static void Build()
        {
            Directory.CreateDirectory(FormsFolder);
            Directory.CreateDirectory(PrefabsFolder);
            var problems = new List<string>();
            int made = 0;

            NetworkPrefabsList networkPrefabs = AssetDatabase.LoadAssetAtPath<NetworkPrefabsList>(NetworkPrefabsPath);
            if (networkPrefabs == null) problems.Add($"No network prefab list at {NetworkPrefabsPath}: a world form that is not in it cannot be spawned.");

            foreach (string guid in AssetDatabase.FindAssets("t:ArmorData", new[] { ArmorSetup.ItemsFolder }))
            {
                var item = AssetDatabase.LoadAssetAtPath<ArmorData>(AssetDatabase.GUIDToAssetPath(guid));
                if (item.WorldPrefab != null)
                {
                    continue;
                }

                GameObject prefab = MakePrefab(item, problems);
                if (prefab == null)
                {
                    continue;
                }

                if (networkPrefabs != null && !networkPrefabs.Contains(prefab))
                {
                    networkPrefabs.Add(new NetworkPrefab { Prefab = prefab });
                    EditorUtility.SetDirty(networkPrefabs);
                }

                var serialized = new SerializedObject(item);
                serialized.FindProperty("<WorldPrefab>k__BackingField").objectReferenceValue = prefab;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(item);
                made++;
            }

            AssetDatabase.SaveAssets();
            var text = new System.Text.StringBuilder();
            text.AppendLine($"[ArmorWorld] {made} world form(s) made, {problems.Count} to look at.");
            foreach (string problem in problems) text.AppendLine("  - " + problem);
            Debug.Log(text.ToString());
        }

        private static GameObject MakePrefab(ArmorData item, List<string> problems)
        {
            var overridePrefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{OverridesFolder}/{item.name}.prefab");
            GameObject root = new GameObject(item.name + "_World");
            try
            {
                if (overridePrefab != null)
                {
                    ((GameObject)PrefabUtility.InstantiatePrefab(overridePrefab, root.transform)).name = "Model";
                }
                else if (!AddGeneratedModel(root.transform, item, problems))
                {
                    return null;
                }

                AddPickup(root, item);
                return PrefabUtility.SaveAsPrefabAsset(root, $"{PrefabsFolder}/{item.name}.prefab");
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        // The pieces of the item's parts, put on the body in its rest pose, baked, folded and written out as meshes under the root.
        private static bool AddGeneratedModel(Transform root, ArmorData item, List<string> problems)
        {
            var parts = AssetDatabase.FindAssets("t:BodyPartDefinition", new[] { ArmorSetup.PartsFolder })
                .Select(g => AssetDatabase.LoadAssetAtPath<BodyPartDefinition>(AssetDatabase.GUIDToAssetPath(g)))
                .Where(p => p.DressesItem == item).ToList();
            if (parts.Count == 0)
            {
                problems.Add($"{item.name}: no body part dresses it, so there is nothing to make a world form from. Make one by hand in {OverridesFolder}.");
                return false;
            }

            var rigPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(HumanMaleSetup.RigPrefabPath);
            GameObject rig = (GameObject)PrefabUtility.InstantiatePrefab(rigPrefab);
            try
            {
                var partsRoot = new GameObject("Parts").transform;
                partsRoot.SetParent(rig.transform, false);
                var bones = new RigBoneIndex(rig.transform.Find(RigRootName));
                foreach (BodyPartDefinition part in parts) BodyPartBinder.Create(part, bones, partsRoot);

                var sources = new List<(Mesh mesh, Material[] materials, Vector3[] vertices)>();
                foreach (Renderer renderer in rig.GetComponentsInChildren<Renderer>().Where(r => r is SkinnedMeshRenderer || r is MeshRenderer))
                {
                    Mesh source;
                    Vector3[] local;
                    if (renderer is SkinnedMeshRenderer skinned)
                    {
                        source = skinned.sharedMesh;
                        var baked = new Mesh();
                        skinned.BakeMesh(baked, true);
                        local = baked.vertices;
                        Object.DestroyImmediate(baked);
                    }
                    else
                    {
                        source = renderer.GetComponent<MeshFilter>().sharedMesh;
                        local = source.vertices;
                    }

                    var inRig = new Vector3[local.Length];
                    for (int i = 0; i < local.Length; i++) inRig[i] = rig.transform.InverseTransformPoint(renderer.transform.TransformPoint(local[i]));
                    sources.Add((source, renderer.sharedMaterials, inRig));
                }

                var pieces = sources.Select(s => s.vertices).ToList();
                if (IsFolded(item.WornSlot)) GarmentFold.Fold(pieces, GarmentFold.Settings.Default);
                else GarmentFold.Stand(pieces, GarmentFold.Settings.Default);

                for (int i = 0; i < sources.Count; i++)
                {
                    Mesh made = CopyWithVertices(sources[i].mesh, sources[i].vertices);
                    string path = $"{FormsFolder}/{item.name}_World_{i}.asset";
                    AssetDatabase.DeleteAsset(path);
                    AssetDatabase.CreateAsset(made, path);

                    var piece = new GameObject("Piece" + i, typeof(MeshFilter), typeof(MeshRenderer));
                    piece.transform.SetParent(root, false);
                    piece.GetComponent<MeshFilter>().sharedMesh = made;
                    piece.GetComponent<MeshRenderer>().sharedMaterials = sources[i].materials;
                }

                return true;
            }
            finally
            {
                Object.DestroyImmediate(rig);
            }
        }

        private static Mesh CopyWithVertices(Mesh source, Vector3[] vertices)
        {
            var made = new Mesh { name = source.name + "_World", indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
            made.vertices = vertices;
            made.uv = source.uv;
            made.subMeshCount = source.subMeshCount;
            for (int s = 0; s < source.subMeshCount; s++) made.SetTriangles(source.GetTriangles(s), s);
            made.RecalculateNormals();
            made.RecalculateBounds();
            return made;
        }

        // The ordinary pickup: a network object that hands the item to whoever picks it up, with a collider around whatever the model is.
        private static void AddPickup(GameObject root, ArmorData item)
        {
            root.AddComponent<NetworkObject>();
            var pickup = root.AddComponent<WorldItemPickup>();
            var serialized = new SerializedObject(pickup);
            serialized.FindProperty("item").objectReferenceValue = item;
            serialized.FindProperty("<InteractionRange>k__BackingField").floatValue = 2.5f;
            serialized.FindProperty("<InteractionName>k__BackingField").stringValue = item.ItemName;
            serialized.FindProperty("<InteractionText>k__BackingField").stringValue = "Pick up";
            serialized.FindProperty("<InteractionTime>k__BackingField").floatValue = 0.1f;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            Renderer[] renderers = root.GetComponentsInChildren<Renderer>();
            Bounds bounds = renderers.Length > 0 ? renderers[0].bounds : new Bounds(Vector3.zero, Vector3.one * 0.3f);
            foreach (Renderer renderer in renderers) bounds.Encapsulate(renderer.bounds);
            var box = root.AddComponent<BoxCollider>();
            box.center = root.transform.InverseTransformPoint(bounds.center);
            box.size = Vector3.Max(bounds.size, Vector3.one * 0.1f);
        }
    }
}
