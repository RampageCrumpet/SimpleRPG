using System.Collections.Generic;
using System.Linq;
using Unity.Netcode;
using UnityEditor;
using UnityEngine;

namespace SimpleRPG.NetworkTools
{
    /// <summary>
    /// Netcode adds every prefab with a NetworkObject to the default network prefab list whenever it is imported, which puts
    /// test-only prefabs (Assets/Tests) into the shipped list and can add the same prefab twice. This cleans the list just
    /// before it is written to disk. Test prefabs are registered by the tests that use them.
    /// </summary>
    public class NetworkPrefabListGuard : AssetModificationProcessor
    {
        private const string TestsFolder = "Assets/Tests/";

        private static string[] OnWillSaveAssets(string[] paths)
        {
            foreach (string path in paths)
            {
                if (path.EndsWith(".asset") && AssetDatabase.GetMainAssetTypeAtPath(path) == typeof(NetworkPrefabsList))
                {
                    Clean(AssetDatabase.LoadAssetAtPath<NetworkPrefabsList>(path));
                }
            }

            return paths;
        }

        /// <summary>
        /// Removes entries for test prefabs and repeated entries for the same prefab.
        /// </summary>
        /// <returns> True if the list changed.</returns>
        public static bool Clean(NetworkPrefabsList list)
        {
            if (list == null)
            {
                return false;
            }

            var seen = new HashSet<GameObject>();
            var unwanted = new List<NetworkPrefab>();
            foreach (NetworkPrefab entry in list.PrefabList)
            {
                GameObject prefab = entry.Prefab;
                bool isTest = prefab != null && AssetDatabase.GetAssetPath(prefab).StartsWith(TestsFolder);
                if (isTest || (prefab != null && !seen.Add(prefab)))
                {
                    unwanted.Add(entry);
                }
            }

            foreach (NetworkPrefab entry in unwanted)
            {
                list.Remove(entry);
            }

            if (unwanted.Count > 0)
            {
                EditorUtility.SetDirty(list);
            }

            return unwanted.Count > 0;
        }
    }
}
