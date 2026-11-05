using SimpleRPG.Abilities;
using SimpleRPG.Gameplay;
using SimpleRPG.InventorySystem;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace SimpleRPG.EffectTools
{
    /// <summary>
    /// What the affix catalogs share: describing an affix as data (<see cref="Spec"/>), the three-tier ladder, and making the affix and pool assets
    /// from it. A catalog is a list of specs; it never creates an asset that exists, so the numbers belong to the designer after the first run.
    /// </summary>
    public static class AffixCatalogBuilder
    {
        /// <summary>One affix as the catalog describes it.</summary>
        public sealed class Spec
        {
            public string AssetName;
            public string DisplayName;
            public AffixKind Kind;
            public string Family;
            public int Tier;
            public int MinimumItemLevel;
            public float Weight;
            public EquipmentSlot[] Slots = new EquipmentSlot[0];
            public List<AffixEffect> Effects = new List<AffixEffect>();
        }

        // Tier 1 from item level 1, tier 2 from 8, tier 3 from 16: the same ladder for every family.
        private static readonly int[] TierLevels = { 1, 8, 16 };
        private static readonly float[] TierWeights = { 10f, 5f, 2f };

        public static StatModifierEffect Stat(string label, string key, ModifierLayer layer, float min, float max, AffixScope scope = AffixScope.Wearer) =>
            new StatModifierEffect(label, key, layer, min, max, scope);

        // Adds the three tiers of one family. ranges[tier][effect] = (min, max) for each effect, in the order of the effect makers.
        public static void Family(List<Spec> into, string family, AffixKind kind, string[] names, EquipmentSlot[] slots,
            System.Func<int, List<AffixEffect>> effectsOfTier)
        {
            for (int tier = 0; tier < 3; tier++)
            {
                into.Add(new Spec
                {
                    AssetName = (kind == AffixKind.Prefix ? "Prefix_" : "Suffix_") + names[tier].Replace("of the ", "").Replace("of ", "").Replace(" ", ""),
                    DisplayName = names[tier],
                    Kind = kind,
                    Family = family,
                    Tier = tier + 1,
                    MinimumItemLevel = TierLevels[tier],
                    Weight = TierWeights[tier],
                    Slots = slots,
                    Effects = effectsOfTier(tier),
                });
            }
        }

        public static List<AffixEffect> One(AffixEffect effect) => new List<AffixEffect> { effect };

        public static AffixDefinition BuildAffix(Spec spec)
        {
            AffixDefinition affix = ScriptableObject.CreateInstance<AffixDefinition>();
            affix.name = spec.AssetName;
            var serialized = new SerializedObject(affix);
            serialized.FindProperty("displayName").stringValue = spec.DisplayName;
            serialized.FindProperty("kind").enumValueIndex = (int)spec.Kind;
            serialized.FindProperty("family").stringValue = spec.Family;
            serialized.FindProperty("tier").intValue = spec.Tier;
            serialized.FindProperty("minimumItemLevel").intValue = spec.MinimumItemLevel;
            serialized.FindProperty("weight").floatValue = spec.Weight;

            SerializedProperty slots = serialized.FindProperty("allowedSlots");
            slots.arraySize = spec.Slots.Length;
            for (int i = 0; i < spec.Slots.Length; i++) slots.GetArrayElementAtIndex(i).enumValueIndex = (int)spec.Slots[i];

            SerializedProperty effects = serialized.FindProperty("effects");
            effects.arraySize = spec.Effects.Count;
            for (int i = 0; i < spec.Effects.Count; i++) effects.GetArrayElementAtIndex(i).managedReferenceValue = spec.Effects[i];

            serialized.ApplyModifiedPropertiesWithoutUndo();
            return affix;
        }

        /// <summary>A pool for one item, with at most one prefix and one suffix. The counts are the designer's to change per item.</summary>
        public static AffixPool BuildPool(string name, IEnumerable<AffixDefinition> affixes, int minimum = 0, int maximum = 2, RareNames rareNames = null, float rareChance = 0f)
        {
            AffixPool pool = ScriptableObject.CreateInstance<AffixPool>();
            pool.name = name;
            var serialized = new SerializedObject(pool);
            SerializedProperty list = serialized.FindProperty("affixes");
            AffixDefinition[] all = affixes.ToArray();
            list.arraySize = all.Length;
            for (int i = 0; i < all.Length; i++) list.GetArrayElementAtIndex(i).objectReferenceValue = all[i];
            serialized.FindProperty("minimumCount").intValue = minimum;
            serialized.FindProperty("maximumCount").intValue = maximum;
            serialized.FindProperty("maximumPrefixes").intValue = 1;
            serialized.FindProperty("maximumSuffixes").intValue = 1;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            if (rareNames != null && rareChance > 0f) EnableRare(pool, rareNames, rareChance);
            return pool;
        }

        /// <summary>
        /// Lets a pool's items be rare: with this chance an instance rolls three to six affixes (up to three of each kind) and is called by one of
        /// the names. The counts are the pool's own to change afterwards.
        /// </summary>
        public static void EnableRare(AffixPool pool, RareNames rareNames, float chance)
        {
            var serialized = new SerializedObject(pool);
            serialized.FindProperty("rareNames").objectReferenceValue = rareNames;
            serialized.FindProperty("rareChance").floatValue = chance;
            serialized.FindProperty("rareMinimumCount").intValue = 3;
            serialized.FindProperty("rareMaximumCount").intValue = 6;
            serialized.FindProperty("rareMaximumPrefixes").intValue = 3;
            serialized.FindProperty("rareMaximumSuffixes").intValue = 3;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        /// <summary>Makes an affix asset for each spec that has none, and returns every affix (made or already there), in spec order.</summary>
        public static List<AffixDefinition> EnsureAffixAssets(IEnumerable<Spec> specs, string folder, out int made)
        {
            Directory.CreateDirectory(folder);
            made = 0;
            var affixes = new List<AffixDefinition>();
            foreach (Spec spec in specs)
            {
                string path = $"{folder}/{spec.AssetName}.asset";
                AffixDefinition affix = AssetDatabase.LoadAssetAtPath<AffixDefinition>(path);
                if (affix == null)
                {
                    affix = BuildAffix(spec);
                    affix.EnsureId();
                    AssetDatabase.CreateAsset(affix, path);
                    made++;
                }

                affixes.Add(affix);
            }

            return affixes;
        }

        /// <summary>
        /// Gives each item of a type in a folder (that <paramref name="include"/> accepts, if given) its own pool (made only if missing) and sets it on the item only if it has none.
        /// </summary>
        public static void EnsurePools<T>(string itemFolder, string poolFolder, IReadOnlyList<AffixDefinition> affixes, int minimum, int maximum, out int pools, out int assigned, System.Func<T, bool> include = null, RareNames rareNames = null, float rareChance = 0f) where T : Item
        {
            Directory.CreateDirectory(poolFolder);
            pools = 0;
            assigned = 0;
            foreach (string guid in AssetDatabase.FindAssets("t:" + typeof(T).Name, new[] { itemFolder }))
            {
                var item = AssetDatabase.LoadAssetAtPath<T>(AssetDatabase.GUIDToAssetPath(guid));
                if (include != null && !include(item))
                {
                    continue;
                }

                string poolPath = $"{poolFolder}/{item.name}.asset";
                AffixPool pool = AssetDatabase.LoadAssetAtPath<AffixPool>(poolPath);
                if (pool == null)
                {
                    pool = BuildPool(item.name, affixes, minimum, maximum, rareNames, rareChance);
                    AssetDatabase.CreateAsset(pool, poolPath);
                    pools++;
                }

                var serialized = new SerializedObject(item);
                SerializedProperty slot = serialized.FindProperty("affixPool");
                if (slot.objectReferenceValue == null)
                {
                    slot.objectReferenceValue = pool;
                    serialized.ApplyModifiedPropertiesWithoutUndo();
                    EditorUtility.SetDirty(item);
                    assigned++;
                }
            }
        }
    }
}
