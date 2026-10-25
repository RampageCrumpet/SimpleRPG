using System.Collections.Generic;
using SimpleRPG.Abilities;
using SimpleRPG.Audio;
using UnityEditor;
using UnityEngine;

namespace SimpleRPG.AudioTools
{
    /// <summary>
    /// Builds the small example catalog under Assets/Audio/Catalog: seven definitions that show every part of the system working together.
    /// Where the project has a recording the event uses it; where it does not the event is a valid but silent placeholder (it shows as a
    /// warning in validation) waiting for a clip. Existing assets are kept, never overwritten, so editing them is safe.
    /// </summary>
    public static class AudioCatalog
    {
        public const string Root = "Assets/Audio/Catalog";

        private const string Combat = "Assets/Art/Audio/SFX/Combat/";
        private const string Skeleton = "Assets/Art/Audio/SFX/Skeleton/";

        [MenuItem("SimpleRPG/Audio/Create Example Catalog")]
        public static void CreateMenu()
        {
            Build();
            Debug.Log("[Audio] Example catalog is in " + Root);
        }

        private static T Make<T>(string folder, string name, System.Func<T> create) where T : Object
        {
            AudioSetup.EnsureFolder(Root + "/" + folder);
            string path = Root + "/" + folder + "/" + name + ".asset";
            T existing = AssetDatabase.LoadAssetAtPath<T>(path);
            if (existing != null)
            {
                return existing;
            }

            T made = create();
            AssetDatabase.CreateAsset(made, path);
            return made;
        }

        private static AudioClip Clip(string path) => AssetDatabase.LoadAssetAtPath<AudioClip>(path);

        private static AudioLayer ClipLayer(float pitchVariation, float volumeVariation, params AudioClip[] clips)
        {
            List<WeightedClip> list = new List<WeightedClip>();
            foreach (AudioClip clip in clips)
            {
                if (clip != null) list.Add(new WeightedClip(clip));
            }

            return new AudioLayer { clips = list.ToArray(), pitchVariationSemitones = pitchVariation, volumeVariation = volumeVariation };
        }

        /// <summary>
        /// A single-clip one-shot event in the catalog, kept if it already exists.
        /// </summary>
        public static AudioEventDefinition Event(string name, AudioCategory category, float pitchVariation, float volumeVariation, string clipPath, int importance = 60)
        {
            return Make("Events", name, () => AudioEventDefinition.Create(name, category, AudioPlaybackMode.OneShot,
                new[] { ClipLayer(pitchVariation, volumeVariation, Clip(clipPath)) }, maxConcurrent: 3, importance: importance));
        }

        public static AudioRouting Build()
        {
            AudioRouting routing = AudioSetup.CreateMixerAndRouting();

            // ----- Tags and surfaces -----
            AudioTag steel = Make("Tags", "Material_Steel", () => AudioTag.Create("Material_Steel"));
            AudioTag bone = Make("Tags", "Material_Bone", () => AudioTag.Create("Material_Bone"));
            Make("Tags", "Material_Flesh", () => AudioTag.Create("Material_Flesh"));
            AudioTag barefoot = Make("Tags", "Footwear_Barefoot", () => AudioTag.Create("Footwear_Barefoot"));
            AudioTag boots = Make("Tags", "Footwear_Boots", () => AudioTag.Create("Footwear_Boots"));
            AudioTag plate = Make("Tags", "Armor_Plate", () => AudioTag.Create("Armor_Plate"));

            AcousticSurface ground = Make("Surfaces", "Surface_Ground", () => AudioTag.Create<AcousticSurface>("Surface_Ground"));
            AcousticSurface stone = Make("Surfaces", "Surface_Stone", () => AudioTag.Create<AcousticSurface>("Surface_Stone", ground));
            AcousticSurface dirt = Make("Surfaces", "Surface_Dirt", () => AudioTag.Create<AcousticSurface>("Surface_Dirt", ground));
            Make("Surfaces", "Surface_Gravel", () => AudioTag.Create<AcousticSurface>("Surface_Gravel", dirt));
            Make("Surfaces", "Surface_Wood", () => AudioTag.Create<AcousticSurface>("Surface_Wood", ground));
            Make("Surfaces", "Surface_Metal", () => AudioTag.Create<AcousticSurface>("Surface_Metal", ground));
            Make("Surfaces", "Surface_Water", () => AudioTag.Create<AcousticSurface>("Surface_Water"));

            // ----- 1. Weapon swings, one per attack -----
            AudioEventDefinition swingA1 = Make("Events", "Swing_Longsword_A1", () => AudioEventDefinition.Create("Swing_Longsword_A1", AudioCategory.Combat, AudioPlaybackMode.OneShot,
                new[] { ClipLayer(0.3f, 0.05f, Clip(Combat + "SFX_Longsword_A1_RightDiagonal.wav")) }, maxConcurrent: 3, importance: 60));
            AudioEventDefinition swingA2 = Make("Events", "Swing_Longsword_A2", () => AudioEventDefinition.Create("Swing_Longsword_A2", AudioCategory.Combat, AudioPlaybackMode.OneShot,
                new[] { ClipLayer(0.3f, 0.05f, Clip(Combat + "SFX_Longsword_A2_LeftDiagonal.wav")) }, maxConcurrent: 3, importance: 60));
            AudioEventDefinition swingA3 = Make("Events", "Swing_Longsword_A3", () => AudioEventDefinition.Create("Swing_Longsword_A3", AudioCategory.Combat, AudioPlaybackMode.OneShot,
                new[] { ClipLayer(0.3f, 0.05f, Clip(Combat + "SFX_Longsword_A3_Thrust.wav")) }, maxConcurrent: 3, importance: 60));

            // ----- 2. Bone impacts: one shared family for every steel weapon on bone -----
            AudioEventDefinition strike = Make("Events", "Impact_SteelOnBone", () => AudioEventDefinition.Create("Impact_SteelOnBone", AudioCategory.Combat, AudioPlaybackMode.OneShot,
                new[] { ClipLayer(0.5f, 0.05f, Clip(Combat + "SFX_Skeleton_Hit_Strike_01.wav")) }, maxConcurrent: 4, importance: 75));
            AudioEventDefinition headStrike = Make("Events", "Impact_SteelOnBone_Head", () => AudioEventDefinition.Create("Impact_SteelOnBone_Head", AudioCategory.Combat, AudioPlaybackMode.OneShot,
                new[] { ClipLayer(0.5f, 0.05f, Clip(Combat + "SFX_Skeleton_Hit_Head_01.wav")) }, maxConcurrent: 4, importance: 80));
            AudioEventTable impacts = Make("Tables", "Impacts_Default", () =>
            {
                AudioTableEntry head = AudioEventTable.Row(headStrike,
                    new AudioCondition { facet = AudioFacet.Source, tag = steel }, new AudioCondition { facet = AudioFacet.Target, tag = bone });
                head.regions = AudioRegion.Head;
                return AudioEventTable.Create("Impacts_Default", null,
                    AudioEventTable.Row(strike, new AudioCondition { facet = AudioFacet.Source, tag = steel }, new AudioCondition { facet = AudioFacet.Target, tag = bone }),
                    head);
            });

            // ----- 3. Stone footsteps that depend on footwear -----
            AudioEventDefinition stepBare = Make("Events", "Footstep_Stone_Bare", () => AudioEventDefinition.Create("Footstep_Stone_Bare", AudioCategory.Characters, AudioPlaybackMode.OneShot,
                new[] { ClipLayer(0.5f, 0.1f, Clip(Skeleton + "SFX_Skeleton_Footstep_L_01.wav"), Clip(Skeleton + "SFX_Skeleton_Footstep_R_01.wav")) }, maxConcurrent: 4, importance: 35));
            AudioEventDefinition stepBoots = Make("Events", "Footstep_Stone_Boots", () => AudioEventDefinition.Create("Footstep_Stone_Boots", AudioCategory.Characters, AudioPlaybackMode.OneShot,
                new[] { new AudioLayer { pitchVariationSemitones = 1f, volumeVariation = 0.1f } }, maxConcurrent: 4, importance: 35));
            AudioEventTable footsteps = Make("Tables", "Footsteps_Default", () => AudioEventTable.Create("Footsteps_Default", stepBare,
                AudioEventTable.Row(stepBare, new AudioCondition { facet = AudioFacet.Surface, tag = stone }, new AudioCondition { facet = AudioFacet.Footwear, tag = barefoot }),
                AudioEventTable.Row(stepBoots, new AudioCondition { facet = AudioFacet.Surface, tag = stone }, new AudioCondition { facet = AudioFacet.Footwear, tag = boots })));

            // ----- 4. Metal equipment rattle, layered onto steps -----
            AudioEventDefinition rattle = Make("Events", "Movement_PlateRattle", () => AudioEventDefinition.Create("Movement_PlateRattle", AudioCategory.Characters, AudioPlaybackMode.OneShot,
                new[] { new AudioLayer { pitchVariationSemitones = 1.5f, volumeVariation = 0.15f, volume = 0.6f } }, maxConcurrent: 2, importance: 25, minInterval: 0.15f));
            Make("Profiles", "Movement_PlateArmor", () => MovementAudioProfile.Create("Movement_PlateArmor", armor: plate, stepSound: rattle, chance: 0.5f, cooldown: 0.3f, group: "metal", minIntensity: 0.3f));
            Make("Profiles", "Movement_Boots", () => MovementAudioProfile.Create("Movement_Boots", footwear: boots));

            // ----- 5. Inventory: a family for gems -----
            AudioEventDefinition gemPlace = Make("Events", "Inventory_Gem_Place", () => AudioEventDefinition.Create("Inventory_Gem_Place", AudioCategory.UI, AudioPlaybackMode.OneShot,
                new[] { new AudioLayer { pitchVariationSemitones = 2f, volumeVariation = 0.1f } }, AudioSpatialMode.NonSpatial, maxConcurrent: 2, importance: 40));
            AudioEventDefinition gemPickup = Make("Events", "Inventory_Gem_Pickup", () => AudioEventDefinition.Create("Inventory_Gem_Pickup", AudioCategory.World, AudioPlaybackMode.OneShot,
                new[] { new AudioLayer { pitchVariationSemitones = 2f } }, maxConcurrent: 2, importance: 40));
            Make("Profiles", "ItemAudio_Gems", () => ItemAudioProfile.Create("ItemAudio_Gems", null, gemPickup, gemPlace));

            // ----- 6. A spatial environmental loop -----
            Make("Events", "Loop_Torch", () => AudioEventDefinition.Create("Loop_Torch", AudioCategory.Ambience, AudioPlaybackMode.Sustained,
                new[] { new AudioLayer { loop = true, volume = 0.5f } }, maxConcurrent: 0, importance: 20, fadeOutSeconds: 0.5f));

            // ----- 7. Intermittent dungeon ambience, mostly quiet -----
            AudioEventDefinition drip = Make("Events", "Ambience_Drip", () => AudioEventDefinition.Create("Ambience_Drip", AudioCategory.Ambience, AudioPlaybackMode.OneShot,
                new[] { new AudioLayer { pitchVariationSemitones = 3f, volumeVariation = 0.2f, volume = 0.5f } }, maxConcurrent: 2, importance: 15));
            Make("Profiles", "Ambience_Crypt", () => AmbienceProfile.Create("Ambience_Crypt", null,
                new[] { new IntermittentSound { sound = drip, minInterval = 25f, maxInterval = 70f, minDistance = 6f, maxDistance = 18f, chance = 0.8f } },
                quietChance: 0.4f, quietMin: 30f, quietMax: 90f));

            AssetDatabase.SaveAssets();
            WireRouting(routing, footsteps, impacts, stone);
            WireLongsword(swingA1, swingA2, swingA3, steel);
            AssetDatabase.SaveAssets();
            return routing;
        }

        private static void WireRouting(AudioRouting routing, AudioEventBase footsteps, AudioEventBase impacts, AcousticSurface stone)
        {
            SerializedObject so = new SerializedObject(routing);
            if (so.FindProperty("defaultFootstepTable").objectReferenceValue == null) so.FindProperty("defaultFootstepTable").objectReferenceValue = footsteps;
            if (so.FindProperty("defaultImpactTable").objectReferenceValue == null) so.FindProperty("defaultImpactTable").objectReferenceValue = impacts;
            if (so.FindProperty("defaultSurface").objectReferenceValue == null) so.FindProperty("defaultSurface").objectReferenceValue = stone;

            PhysicsMaterial stoneMaterial = AssetDatabase.LoadAssetAtPath<PhysicsMaterial>("Assets/PhysicsMaterials/Stone.physicMaterial");
            SerializedProperty map = so.FindProperty("physicsMaterialSurfaces");
            if (stoneMaterial != null && map.arraySize == 0)
            {
                map.arraySize = 1;
                map.GetArrayElementAtIndex(0).FindPropertyRelative("material").objectReferenceValue = stoneMaterial;
                map.GetArrayElementAtIndex(0).FindPropertyRelative("surface").objectReferenceValue = stone;
            }

            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(routing);
        }

        // The longsword's three attacks get their own swing sounds, at the moments the audio workflow doc measured for each swing.
        private static void WireLongsword(AudioEventBase a1, AudioEventBase a2, AudioEventBase a3, AudioTag steel)
        {
            ComboDefinition combo = AssetDatabase.LoadAssetAtPath<ComboDefinition>("Assets/Items/Weapon Data/Longsword_Combo.asset");
            if (combo == null)
            {
                return;
            }

            SerializedObject so = new SerializedObject(combo);
            SerializedProperty steps = so.FindProperty("steps");
            AudioEventBase[] sounds = { a1, a2, a3 };
            float[] times = { 0.93f, 0.70f, 0.81f };
            for (int i = 0; i < steps.arraySize && i < sounds.Length; i++)
            {
                SerializedProperty swing = steps.GetArrayElementAtIndex(i).FindPropertyRelative("swingSound");
                if (swing.objectReferenceValue == null)
                {
                    swing.objectReferenceValue = sounds[i];
                    steps.GetArrayElementAtIndex(i).FindPropertyRelative("swingSoundTime").floatValue = times[i];
                }
            }

            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(combo);

            foreach (string guid in AssetDatabase.FindAssets("t:WeaponData"))
            {
                WeaponData weapon = AssetDatabase.LoadAssetAtPath<WeaponData>(AssetDatabase.GUIDToAssetPath(guid));
                if (weapon != null && weapon.Combo == combo && weapon.AudioMaterial == null)
                {
                    SerializedObject weaponObject = new SerializedObject(weapon);
                    weaponObject.FindProperty("audioMaterial").objectReferenceValue = steel;
                    weaponObject.ApplyModifiedPropertiesWithoutUndo();
                    EditorUtility.SetDirty(weapon);
                }
            }
        }
    }
}
