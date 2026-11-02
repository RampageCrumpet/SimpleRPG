using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace SimpleRPG.Editor
{
    /// <summary>
    /// Puts the dungeon's pieces on the layers the game's physics expects. Everything under <c>Prefabs/World/LevelComponents</c> starts on
    /// Default, so a weapon cannot tell a wall from a prop, and the layer masks that name <c>World</c> and <c>Walkable</c> (population clearance,
    /// enemy grounding) match nothing. See <see cref="DungeonLayerRules"/> for which piece goes where.
    /// Only objects that have a solid collider are moved, and each prefab is only saved if something changed, so running it twice is harmless.
    /// </summary>
    public static class DungeonLayerAssigner
    {
        private const string Folder = "Assets/Prefabs/World/LevelComponents";

        [MenuItem("SimpleRPG/Level/Report Dungeon Layers (no changes)")]
        private static void Report() => Run(apply: false);

        [MenuItem("SimpleRPG/Level/Assign Dungeon Layers")]
        private static void Assign() => Run(apply: true);

        private static void Run(bool apply)
        {
            int world = LayerMask.NameToLayer(DungeonLayerRules.World);
            int walkable = LayerMask.NameToLayer(DungeonLayerRules.Walkable);
            if (world < 0 || walkable < 0)
            {
                Debug.LogError($"The '{DungeonLayerRules.World}' and '{DungeonLayerRules.Walkable}' layers must exist (Project Settings > Tags and Layers).");
                return;
            }

            var report = new StringBuilder();
            int prefabsChanged = 0;
            int objectsChanged = 0;
            var untouched = new List<string>();

            foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { Folder }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                string layerName = DungeonLayerRules.LayerFor(System.IO.Path.GetFileNameWithoutExtension(path));
                if (layerName == null)
                {
                    untouched.Add(System.IO.Path.GetFileNameWithoutExtension(path));
                    continue;
                }

                int layer = layerName == DungeonLayerRules.World ? world : walkable;
                GameObject contents = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    int changed = 0;
                    foreach (Collider collider in contents.GetComponentsInChildren<Collider>(true))
                    {
                        if (!collider.isTrigger && collider.gameObject.layer != layer)
                        {
                            collider.gameObject.layer = layer;
                            changed++;
                        }
                    }

                    if (changed > 0)
                    {
                        report.AppendLine($"{layerName,-9} {changed,3} object(s)  {path}");
                        prefabsChanged++;
                        objectsChanged += changed;
                        if (apply)
                        {
                            PrefabUtility.SaveAsPrefabAsset(contents, path);
                        }
                    }
                }
                finally
                {
                    PrefabUtility.UnloadPrefabContents(contents);
                }
            }

            string verb = apply ? "Moved" : "Would move";
            Debug.Log($"{verb} {objectsChanged} object(s) in {prefabsChanged} prefab(s).\n{report}\nLeft alone by name: {string.Join(", ", untouched)}");
            if (apply)
            {
                AssetDatabase.SaveAssets();
            }
        }
    }
}
