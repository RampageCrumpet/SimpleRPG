#if UNITY_EDITOR
using SimpleRPG.InventorySystem;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace SimpleRPG.EditorTools
{
    /// <summary>
    /// Creates and updates the <see cref="ItemRegistry"/> asset in Resources.
    /// Items are only ever appended: an item's network ID is its position in the registry, so existing entries must not move.
    /// </summary>
    public static class ItemRegistryBuilder
    {
        private const string RegistryAssetPath = "Assets/Resources/" + ItemRegistry.ResourcePath + ".asset";

        /// <summary>
        /// Appends every Item asset in the project that isn't in the registry yet, creating the registry if needed.
        /// </summary>
        [MenuItem("Items/Update Item Registry")]
        public static void UpdateRegistry()
        {
            ItemRegistry registry = AssetDatabase.LoadAssetAtPath<ItemRegistry>(RegistryAssetPath);
            if (registry == null)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(RegistryAssetPath));
                registry = ScriptableObject.CreateInstance<ItemRegistry>();
                AssetDatabase.CreateAsset(registry, RegistryAssetPath);
            }

            var serializedRegistry = new SerializedObject(registry);
            SerializedProperty items = serializedRegistry.FindProperty("items");

            var registered = new System.Collections.Generic.HashSet<Object>();
            for (int i = 0; i < items.arraySize; i++)
            {
                registered.Add(items.GetArrayElementAtIndex(i).objectReferenceValue);
            }

            int added = 0;
            foreach (string guid in AssetDatabase.FindAssets("t:Item"))
            {
                Item item = AssetDatabase.LoadAssetAtPath<Item>(AssetDatabase.GUIDToAssetPath(guid));
                if (item != null && registered.Add(item))
                {
                    items.InsertArrayElementAtIndex(items.arraySize);
                    items.GetArrayElementAtIndex(items.arraySize - 1).objectReferenceValue = item;
                    added++;
                }
            }

            serializedRegistry.ApplyModifiedProperties();
            EditorUtility.SetDirty(registry);
            AssetDatabase.SaveAssets();
            Debug.Log($"Item registry updated: {added} item(s) added, {items.arraySize} total.");
        }
    }
}
#endif
