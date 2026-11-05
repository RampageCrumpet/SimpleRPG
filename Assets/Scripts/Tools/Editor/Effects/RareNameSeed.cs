using SimpleRPG.InventorySystem;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace SimpleRPG.EffectTools
{
    /// <summary>
    /// The names rare items are called by, as the owner supplied them, and the tools that make the asset and turn rare items on in the pools.
    /// A name's id is its place in this list, 1 to 100, and is permanent: new names are added to the end, never inserted or reused.
    /// </summary>
    public static class RareNameSeed
    {
        public const string AssetPath = "Assets/Items/RareNames.asset";

        /// <summary>The chance an item from a pool is rare. A first guess.</summary>
        public const float DefaultChance = 0.15f;

        public static readonly string[] Names =
        {
            "Grave Bite",
            "Doom Fang",
            "Wraith Edge",
            "Blood Whisper",
            "Ashen Verdict",
            "Dread Talon",
            "Grim Sever",
            "Bone Cleaver",
            "Death's Grasp",
            "Widow's Cry",
            "Night Reaver",
            "Skull Rend",
            "Corpse Brand",
            "Crypt Howl",
            "Rotting Thorn",
            "Sable Fury",
            "Shadow Ruin",
            "Mourning Fang",
            "Sin Eater",
            "Bleak Harvest",
            "Grave Warden",
            "Ruin Bringer",
            "Dusk Reaver",
            "Blood Tithe",
            "Carrion Song",
            "Iron Malice",
            "Steel Vengeance",
            "Rusted Glory",
            "Gilded Sorrow",
            "Crimson Debt",
            "Bitter Edge",
            "Savage Oath",
            "Merciless Hand",
            "Cruel Fortune",
            "Tyrant's Grasp",
            "Butcher's Due",
            "Warborn Grief",
            "King's Ruin",
            "Sundered Will",
            "Executioner's Debt",
            "Saint's Regret",
            "Martyr's Wrath",
            "Pilgrim's End",
            "Heretic's Burden",
            "Oathbreaker's Hand",
            "Penitent's Fury",
            "Blighted Prayer",
            "Profane Mercy",
            "Hallowed Grief",
            "Sacred Ruin",
            "Iron Requiem",
            "Black Oath",
            "Hollow Promise",
            "Forsaken Glory",
            "Broken Covenant",
            "Silent Judgment",
            "Pale Dominion",
            "Dark Testament",
            "Fallen Honor",
            "Last Mercy",
            "Serpent's Kiss",
            "Viper's Grin",
            "Raven's Claw",
            "Wolf's Hunger",
            "Boar's Wrath",
            "Crow's Lament",
            "Basilisk Gaze",
            "Vulture's Feast",
            "Thorned Maw",
            "Dragon's Regret",
            "Cinder Wake",
            "Storm Grudge",
            "Winter's Toll",
            "Thunder Scorn",
            "Frostbound Oath",
            "Ember Reign",
            "Tempest Bite",
            "Ashen Fury",
            "Rime Whisper",
            "Scorched Promise",
            "Witch's Bane",
            "Hex Reaver",
            "Occult Fang",
            "Sorcerer's Folly",
            "Runic Malice",
            "Eldritch Thorn",
            "Phantom Toll",
            "Spectral Grudge",
            "Cursed Legacy",
            "Forbidden Rite",
            "Hollow Crown",
            "Nameless Hunger",
            "Forgotten Sin",
            "Endless Vigil",
            "Final Reckoning",
            "Bitter Triumph",
            "Dying Light",
            "Shattered Fate",
            "Crimson Requiem",
            "Eternal Silence",
        };

        /// <summary>The names as the asset holds them.</summary>
        public static RareNames Build()
        {
            RareNames asset = ScriptableObject.CreateInstance<RareNames>();
            asset.name = "RareNames";
            var serialized = new SerializedObject(asset);
            SerializedProperty list = serialized.FindProperty("names");
            list.arraySize = Names.Length;
            for (int i = 0; i < Names.Length; i++)
            {
                SerializedProperty word = list.GetArrayElementAtIndex(i);
                word.FindPropertyRelative("id").intValue = i + 1;
                word.FindPropertyRelative("text").stringValue = Names[i];
            }

            serialized.ApplyModifiedPropertiesWithoutUndo();
            return asset;
        }

        /// <summary>The names asset, made if it does not exist yet. An asset that exists is never changed.</summary>
        public static RareNames EnsureAsset()
        {
            var existing = AssetDatabase.LoadAssetAtPath<RareNames>(AssetPath);
            if (existing != null)
            {
                return existing;
            }

            Directory.CreateDirectory(Path.GetDirectoryName(AssetPath));
            RareNames made = Build();
            AssetDatabase.CreateAsset(made, AssetPath);
            return made;
        }

        [MenuItem("SimpleRPG/Items/Build Rare Names")]
        public static void BuildAsset()
        {
            EnsureAsset();
            AssetDatabase.SaveAssets();
        }

        [MenuItem("SimpleRPG/Items/Enable Rare Items On All Pools")]
        public static void EnableOnAllPools()
        {
            RareNames names = EnsureAsset();
            int changed = 0;
            foreach (string guid in AssetDatabase.FindAssets("t:AffixPool"))
            {
                var pool = AssetDatabase.LoadAssetAtPath<AffixPool>(AssetDatabase.GUIDToAssetPath(guid));
                if (!pool.CanBeRare)
                {
                    AffixCatalogBuilder.EnableRare(pool, names, DefaultChance);
                    EditorUtility.SetDirty(pool);
                    changed++;
                }
            }

            AssetDatabase.SaveAssets();
            Debug.Log($"[Affixes] Rare items turned on in {changed} pool(s).");
        }
    }
}
