using SimpleRPG.Abilities;
using SimpleRPG.Gameplay;
using SimpleRPG.InventorySystem;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using static SimpleRPG.EffectTools.AffixCatalogBuilder;

namespace SimpleRPG.EffectTools
{
    /// <summary>
    /// The first catalog of armor affixes, Diablo II style: prefixes and suffixes in families of three tiers, each tier needing a higher item
    /// level, with some affixes doing several things at once. It uses only effects the game reads today (see <see cref="AttributeConsumers"/>).
    /// <para>
    /// <b>Every number here is provisional.</b> They are a first pass to play with, not a balance. The tool makes an affix asset only if it is missing and never
    /// changes one that exists, and gives each armor item its own pool (also made only if missing, and set on an item only if it has none), so the numbers
    /// and each item's list belong to the designer from the first run.
    /// </para>
    /// </summary>
    public static class ArmorAffixCatalog
    {
        public const string AffixFolder = "Assets/Items/Affixes/Armor";
        public const string PoolFolder = "Assets/Items/AffixPools";
        public const string ArmorFolder = "Assets/Items/Armor";

        /// <summary>The whole catalog.</summary>
        public static List<Spec> Build()
        {
            const string Armor = "simplerpg.stat.armor_rating";
            var none = new EquipmentSlot[0];
            var specs = new List<Spec>();

            // ----- Prefixes -----
            Family(specs, "armor_flat", AffixKind.Prefix, new[] { "Riveted", "Reinforced", "Ironbound" }, none, t =>
                One(Stat("Armor Rating", Armor, ModifierLayer.Add, new[] { 2f, 4f, 7f }[t], new[] { 3f, 6f, 9f }[t])));

            Family(specs, "armor_enhanced", AffixKind.Prefix, new[] { "Sturdy", "Hardened", "Tempered" }, none, t =>
                One(Stat("Armor Rating", Armor, ModifierLayer.PercentAdd, new[] { 0.10f, 0.16f, 0.26f }[t], new[] { 0.15f, 0.25f, 0.40f }[t], AffixScope.ThisItem)));

            Family(specs, "movement", AffixKind.Prefix, new[] { "Fitted", "Tailored", "Masterworked" }, none, t =>
                One(Stat("Movement Speed", DerivedStats.MovementSpeed.Key, ModifierLayer.PercentAdd, new[] { 0.01f, 0.02f, 0.03f }[t], new[] { 0.02f, 0.03f, 0.04f }[t])));

            Family(specs, "ward", AffixKind.Prefix, new[] { "Bonebound", "Gravebound", "Deathsworn" }, none, t => new List<AffixEffect>
            {
                Stat("Willpower", PrimaryAttributes.WillpowerKey, ModifierLayer.PercentAdd, new[] { 0.03f, 0.05f, 0.07f }[t], new[] { 0.04f, 0.06f, 0.08f }[t]),
                Stat("Magical Damage Reduction", DerivedStats.MagicalDamageReduction.Key, ModifierLayer.Add, new[] { 0.010f, 0.020f, 0.030f }[t], new[] { 0.015f, 0.025f, 0.035f }[t]),
            });

            Family(specs, "burden", AffixKind.Prefix, new[] { "Weighted", "Burdened", "Oppressive" }, none, t => new List<AffixEffect>
            {
                Stat("Armor Rating", Armor, ModifierLayer.Add, new[] { 3f, 5f, 8f }[t], new[] { 4f, 7f, 10f }[t]),
                Stat("Constitution", PrimaryAttributes.Constitution.Key, ModifierLayer.PercentAdd, new[] { 0.03f, 0.05f, 0.07f }[t], new[] { 0.04f, 0.06f, 0.08f }[t]),
                Stat("Movement Speed", DerivedStats.MovementSpeed.Key, ModifierLayer.PercentAdd, -0.01f * (t + 1), -0.01f * (t + 1)),
            });

            Family(specs, "protection", AffixKind.Prefix, new[] { "Padded", "Plated", "Fortified" }, none, t =>
                One(Stat("Physical Damage Reduction", DerivedStats.PhysicalDamageReduction.Key, ModifierLayer.Add, new[] { 0.010f, 0.020f, 0.030f }[t], new[] { 0.020f, 0.030f, 0.040f }[t])));

            // ----- Suffixes -----
            Family(specs, "vigor", AffixKind.Suffix, new[] { "of the Veteran", "of the Survivor", "of the Undying" }, none, t =>
                One(Stat("Maximum Health", DerivedStats.MaximumHealth.Key, ModifierLayer.Add, new[] { 5f, 9f, 15f }[t], new[] { 8f, 14f, 22f }[t])));

            Family(specs, "constitution", AffixKind.Suffix, new[] { "of the Bear", "of the Boar", "of the Bull" }, none, t =>
                One(Stat("Constitution", PrimaryAttributes.Constitution.Key, ModifierLayer.Add, new[] { 1f, 2f, 3f }[t], new[] { 2f, 3f, 4f }[t])));

            Family(specs, "agility", AffixKind.Suffix, new[] { "of the Hound", "of the Wolf", "of the Predator" }, none, t =>
                One(Stat("Agility", PrimaryAttributes.Agility.Key, ModifierLayer.PercentAdd, new[] { 0.03f, 0.06f, 0.09f }[t], new[] { 0.05f, 0.08f, 0.12f }[t])));

            Family(specs, "strength", AffixKind.Suffix, new[] { "of the Ox", "of the Titan", "of the Giant" }, none, t =>
                One(Stat("Strength", PrimaryAttributes.Strength.Key, ModifierLayer.Add, new[] { 1f, 2f, 3f }[t], new[] { 2f, 3f, 4f }[t])));

            Family(specs, "weapon_flat", AffixKind.Suffix, new[] { "of Might", "of Slaying", "of Carnage" }, none, t =>
                One(Stat("Weapon Damage", DerivedStats.WeaponDamage.Key, ModifierLayer.Add, new[] { 1f, 2f, 3f }[t], new[] { 2f, 3f, 5f }[t])));

            Family(specs, "weapon_percent", AffixKind.Suffix, new[] { "of Brutality", "of Ferocity", "of Fury" }, none, t =>
                One(Stat("Weapon Damage", DerivedStats.WeaponDamageMultiplier.Key, ModifierLayer.PercentAdd, new[] { 0.02f, 0.04f, 0.06f }[t], new[] { 0.03f, 0.05f, 0.08f }[t])));

            Family(specs, "hands", AffixKind.Suffix, new[] { "of Nimble Hands", "of Deft Hands", "of Swift Hands" }, new[] { EquipmentSlot.hands }, t =>
                One(Stat("Interaction Speed", DerivedStats.InteractionSpeed.Key, ModifierLayer.PercentAdd, new[] { 0.05f, 0.10f, 0.15f }[t], new[] { 0.10f, 0.15f, 0.25f }[t])));

            return specs;
        }

        [MenuItem("SimpleRPG/Items/Build Armor Affix Catalog")]
        public static void BuildAssets()
        {
            List<AffixDefinition> affixes = EnsureAffixAssets(Build(), AffixFolder, out int made);
            EnsurePools<ArmorData>(ArmorFolder, PoolFolder, affixes, 0, 2, out int pools, out int assigned, null, RareNameSeed.EnsureAsset(), RareNameSeed.DefaultChance);

            AssetDatabase.SaveAssets();
            EditorApplication.ExecuteMenuItem("SimpleRPG/Definitions/Rebuild Definition Registry");
            Debug.Log($"[Affixes] {made} new armor affix asset(s), {pools} new pool(s), {assigned} armor item(s) given a pool. Numbers are provisional.");
        }
    }
}
