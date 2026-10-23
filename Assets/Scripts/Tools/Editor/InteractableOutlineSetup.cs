using System.Linq;
using SimpleRPG.Rendering;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace SimpleRPG.EditorTools
{
    /// <summary>
    /// Makes sure every URP renderer in the project carries the interactable outline feature, so the outline shows at every quality level.
    /// </summary>
    public static class InteractableOutlineSetup
    {
        private const string MaskShaderName = "Hidden/SimpleRPG/InteractableOutlineMask";
        private const string CompositeShaderName = "Hidden/SimpleRPG/InteractableOutlineComposite";

        [MenuItem("SimpleRPG/Rendering/Add Interactable Outline To All Renderers")]
        public static void AddToAllRenderers()
        {
            foreach (string guid in AssetDatabase.FindAssets("t:UniversalRendererData", new[] { "Assets" }))
            {
                AddTo(AssetDatabase.LoadAssetAtPath<UniversalRendererData>(AssetDatabase.GUIDToAssetPath(guid)));
            }

            AssetDatabase.SaveAssets();
        }

        /// <summary>
        /// Adds the feature to the renderer if it is missing. Returns true if it changed the asset.
        /// </summary>
        public static bool AddTo(UniversalRendererData rendererData)
        {
            if (rendererData.rendererFeatures.OfType<InteractableOutlineFeature>().Any())
            {
                return false;
            }

            var feature = ScriptableObject.CreateInstance<InteractableOutlineFeature>();
            feature.name = "InteractableOutline";

            var serialized = new SerializedObject(feature);
            serialized.FindProperty("maskShader").objectReferenceValue = Shader.Find(MaskShaderName);
            serialized.FindProperty("compositeShader").objectReferenceValue = Shader.Find(CompositeShaderName);
            serialized.ApplyModifiedPropertiesWithoutUndo();

            AssetDatabase.AddObjectToAsset(feature, rendererData);

            var rendererSerialized = new SerializedObject(rendererData);
            SerializedProperty features = rendererSerialized.FindProperty("m_RendererFeatures");
            SerializedProperty map = rendererSerialized.FindProperty("m_RendererFeatureMap");
            AssetDatabase.TryGetGUIDAndLocalFileIdentifier(feature, out _, out long localId);

            features.arraySize++;
            features.GetArrayElementAtIndex(features.arraySize - 1).objectReferenceValue = feature;
            map.arraySize++;
            map.GetArrayElementAtIndex(map.arraySize - 1).longValue = localId;
            rendererSerialized.ApplyModifiedPropertiesWithoutUndo();

            EditorUtility.SetDirty(rendererData);
            return true;
        }
    }
}
