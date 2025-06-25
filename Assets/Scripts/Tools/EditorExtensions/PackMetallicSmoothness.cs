#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using System.IO;

namespace UnityEditor.EditorTools
{
    public class PackMetallicSmoothness : EditorWindow
    {
        [MenuItem("Tools/Pack Metallic + Smoothness (Bulk)")]
        public static void ShowWindow()
        {
            GetWindow<PackMetallicSmoothness>("Bulk Pack Metallic + Smoothness");
        }

        private string folderPath;
        private int packedCount;

        private const string metallicSuffix = "_Metallic";
        private const string roughnessSuffix = "_Roughness";

        void OnGUI()
        {
            GUILayout.Label("Bulk Pack Metallic + Smoothness Maps", EditorStyles.boldLabel);

            if (GUILayout.Button("Select Folder"))
            {
                string selected = EditorUtility.OpenFolderPanel("Select Folder Containing Maps", "Assets", "");
                if (!string.IsNullOrEmpty(selected))
                {
                    if (selected.StartsWith(Application.dataPath))
                    {
                        folderPath = "Assets" + selected.Substring(Application.dataPath.Length);
                    }
                    else
                    {
                        Debug.LogError("Folder must be inside the Assets directory.");
                        folderPath = null;
                    }
                }
            }

            if (!string.IsNullOrEmpty(folderPath))
            {
                EditorGUILayout.LabelField("Selected Folder:", folderPath);
            }

            if (GUILayout.Button("Run Bulk Pack") && !string.IsNullOrEmpty(folderPath))
            {
                ProcessFolder(folderPath);
            }
        }

        void ProcessFolder(string path)
        {
            string[] metallicGUIDs = AssetDatabase.FindAssets("_metallic t:Texture2D", new[] { path });

            packedCount = 0;

            foreach (string guid in metallicGUIDs)
            {
                string metallicPath = AssetDatabase.GUIDToAssetPath(guid);
                string roughnessPath = metallicPath.Replace(metallicSuffix, roughnessSuffix);

                Texture2D metallicTex = AssetDatabase.LoadAssetAtPath<Texture2D>(metallicPath);
                Texture2D roughnessTex = AssetDatabase.LoadAssetAtPath<Texture2D>(roughnessPath);

                if (metallicTex == null || roughnessTex == null)
                {
                    Debug.LogWarning($"Skipping: Missing texture for pair {metallicPath}");
                    continue;
                }

                if (metallicTex.width != roughnessTex.width || metallicTex.height != roughnessTex.height)
                {
                    Debug.LogWarning($"Skipping: Size mismatch: {metallicPath} vs {roughnessPath}");
                    continue;
                }

                PackAndOverwrite(metallicTex, roughnessTex, metallicPath);
                packedCount++;
            }

            AssetDatabase.Refresh();
            Debug.Log($"Packing complete. {packedCount} textures packed.");
        }

        void PackAndOverwrite(Texture2D metallic, Texture2D roughness, string savePath)
        {
            string metallicName = Path.GetFileNameWithoutExtension(AssetDatabase.GetAssetPath(metallic));
            string roughnessName = Path.GetFileNameWithoutExtension(AssetDatabase.GetAssetPath(roughness));

            // Ensure names match
            string baseMetallic = metallicName.Replace(metallicSuffix, "").ToLower();
            string baseRoughness = roughnessName.Replace(roughnessSuffix, "").ToLower();

            if (baseMetallic != baseRoughness)
            {
                Debug.LogWarning($"Skipping: Name mismatch between '{metallicName}' and '{roughnessName}'");
                return;
            }

            if (metallicName == roughnessName)
            {
                Debug.LogWarning($"Skipping: Metallic and Roughness maps have the same name '{metallicName}'");
                return;
            }

            MakeTextureReadable(metallic);
            MakeTextureReadable(roughness);

            int width = metallic.width;
            int height = metallic.height;

            Color[] metalPixels = metallic.GetPixels();
            Color[] roughnessPixels = roughness.GetPixels();
            Color[] resultPixels = new Color[metalPixels.Length];

            for (int i = 0; i < metalPixels.Length; i++)
            {
                float invertedSmooth = 1f - roughnessPixels[i].r;
                resultPixels[i] = new Color(metalPixels[i].r, 0f, 0f, invertedSmooth);
            }

            Texture2D result = new Texture2D(width, height, TextureFormat.RGBA32, false);
            result.SetPixels(resultPixels);
            result.Apply();

            byte[] bytes = result.EncodeToPNG();
            File.WriteAllBytes(savePath, bytes);
            AssetDatabase.ImportAsset(savePath, ImportAssetOptions.ForceUpdate);

            Debug.Log("Packed and overwritten: " + savePath);
            packedCount++;

            // Only delete if names match exactly
            string roughnessPath = AssetDatabase.GetAssetPath(roughness);
            bool deleted = AssetDatabase.DeleteAsset(roughnessPath);
            if (deleted)
                Debug.Log("Deleted roughness map: " + roughnessPath);
            else
                Debug.LogWarning("Failed to delete roughness map: " + roughnessPath);
        }

        /// <summary>
        /// Make the texture editable so we can read and write data.
        /// </summary>
        /// <param name="texture">The texture we want to make editable.</param>
        void MakeTextureReadable(Texture2D texture)
        {
            string path = AssetDatabase.GetAssetPath(texture);
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;

            if (importer != null && !importer.isReadable)
            {
                importer.isReadable = true;
                importer.SaveAndReimport();
            }
        }
    }
}
#endif