using System.IO;
using UnityEditor;
using UnityEngine;

namespace SimpleRPG.EditorTools
{
    /// <summary>
    /// Real gemstone optics for the SimpleRPG/Gemstone shader. Indices are nD (589nm); dispersion is
    /// nG - nB (431nm minus 687nm), both from standard gemmological tables. Transmittance is the
    /// linear colour left after light crosses one stone width, tuned to each stone's absorption bands.
    /// </summary>
    public static class GemMaterialPresets
    {
        const string ShaderName = "SimpleRPG/Gemstone";
        const string OutputFolder = "Assets/Art/3d Models/Items/Treasure Items/Gems/Materials";

        public readonly struct Preset
        {
            public readonly string Name;
            public readonly float Ior;
            public readonly float Dispersion;
            public readonly Color Transmittance;
            public readonly float Density;

            public Preset(string name, float ior, float dispersion, Color transmittance, float density)
            {
                Name = name;
                Ior = ior;
                Dispersion = dispersion;
                Transmittance = transmittance;
                Density = density;
            }
        }

        public static readonly Preset[] All =
        {
            new("Diamond",        2.417f, 0.044f, new Color(0.97f, 0.98f, 1.00f), 0.5f),
            new("Moissanite",     2.650f, 0.104f, new Color(0.96f, 0.98f, 0.98f), 0.5f),
            new("CubicZirconia",  2.150f, 0.060f, new Color(0.97f, 0.98f, 1.00f), 0.5f),
            new("Zircon",         1.925f, 0.039f, new Color(0.70f, 0.82f, 0.95f), 1.0f),
            new("Garnet",         1.790f, 0.024f, new Color(0.55f, 0.03f, 0.08f), 1.5f),
            new("Ruby",           1.762f, 0.018f, new Color(0.80f, 0.02f, 0.06f), 1.5f),
            new("Sapphire",       1.762f, 0.018f, new Color(0.05f, 0.12f, 0.70f), 1.5f),
            new("Spinel",         1.718f, 0.020f, new Color(0.85f, 0.10f, 0.20f), 1.2f),
            new("Tanzanite",      1.691f, 0.030f, new Color(0.30f, 0.20f, 0.85f), 1.5f),
            new("Peridot",        1.654f, 0.020f, new Color(0.55f, 0.78f, 0.10f), 1.2f),
            new("Topaz",          1.629f, 0.014f, new Color(0.90f, 0.50f, 0.15f), 1.0f),
            new("Emerald",        1.577f, 0.014f, new Color(0.08f, 0.70f, 0.25f), 1.5f),
            new("Aquamarine",     1.577f, 0.014f, new Color(0.45f, 0.82f, 0.90f), 1.0f),
            new("Amethyst",       1.544f, 0.013f, new Color(0.50f, 0.15f, 0.75f), 1.2f),
            new("Citrine",        1.544f, 0.013f, new Color(0.92f, 0.62f, 0.10f), 1.0f),
        };

        public static void Apply(Material material, Preset preset)
        {
            material.SetFloat("_IOR", preset.Ior);
            material.SetFloat("_Dispersion", preset.Dispersion);
            material.SetColor("_Transmittance", preset.Transmittance);
            material.SetFloat("_Density", preset.Density);
        }

        [MenuItem("SimpleRPG/Gems/Create Gem Materials")]
        static void CreateAll()
        {
            Shader shader = Shader.Find(ShaderName);
            if (shader == null)
            {
                Debug.LogError($"Shader '{ShaderName}' not found.");
                return;
            }

            Directory.CreateDirectory(OutputFolder);
            foreach (Preset preset in All)
            {
                string path = $"{OutputFolder}/Gem_{preset.Name}.mat";
                Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (material == null)
                {
                    material = new Material(shader);
                    AssetDatabase.CreateAsset(material, path);
                }
                material.shader = shader;
                Apply(material, preset);
                EditorUtility.SetDirty(material);
            }
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }
    }
}
