using SimpleRPG.Abilities;
using SimpleRPG.Gameplay;
using SimpleRPG.InventorySystem;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using static SimpleRPG.EffectTools.AffixCatalogBuilder;

namespace SimpleRPG.EffectTools
{
    /// <summary>
    /// The first weapon affixes. Two families of its own, both prefixes that change the weapon's damage: Enhanced Damage, a percentage of that one
    /// weapon's base damage, and a flat addition to it. A weapon's pool also takes the damage and Strength suffixes of the armor catalog, and the
    /// on-hit bleed (<see cref="BleedingSetup"/>), so a weapon exercises the whole chain: a stat, a damage bonus and a triggered effect.
    /// <b>All numbers are provisional.</b> Assets and pools are made only when missing, and never changed after.
    /// </summary>
    public static class WeaponAffixCatalog
    {
        public const string AffixFolder = "Assets/Items/Affixes/Weapon";
        public const string WeaponFolder = "Assets/Items/Weapon Data";

        /// <summary>The weapons a player can find and that roll affixes. Test and monster weapons are left alone.</summary>
        public static readonly string[] Weapons = { "Longsword_Combo" };

        /// <summary>The weapon's own affixes.</summary>
        public static List<Spec> Build()
        {
            var none = new EquipmentSlot[0];
            var specs = new List<Spec>();

            Family(specs, "enhanced_damage", AffixKind.Prefix, new[] { "Jagged", "Vicious", "Merciless" }, none, t =>
                One(Stat("Weapon Damage", DerivedStats.WeaponDamage.Key, ModifierLayer.PercentAdd, new[] { 0.15f, 0.26f, 0.41f }[t], new[] { 0.25f, 0.40f, 0.60f }[t], AffixScope.ThisItem)));

            Family(specs, "sharpness", AffixKind.Prefix, new[] { "Keen", "Honed", "Razored" }, none, t =>
                One(Stat("Weapon Damage", DerivedStats.WeaponDamage.Key, ModifierLayer.Add, new[] { 1f, 2f, 4f }[t], new[] { 2f, 4f, 6f }[t])));

            return specs;
        }

        [MenuItem("SimpleRPG/Items/Build Weapon Affix Catalog")]
        public static void BuildAssets()
        {
            List<AffixDefinition> own = EnsureAffixAssets(Build(), AffixFolder, out int made);

            // The shared suffixes and the bleed come from the other catalogs; run those first.
            var pool = new List<AffixDefinition>(own);
            foreach (string family in new[] { "strength", "weapon_percent" })
            {
                foreach (Spec spec in ArmorAffixCatalog.Build().Where(s => s.Family == family))
                {
                    var affix = AssetDatabase.LoadAssetAtPath<AffixDefinition>($"{ArmorAffixCatalog.AffixFolder}/{spec.AssetName}.asset");
                    if (affix == null) Debug.LogWarning($"[Affixes] {spec.AssetName} is missing: run Build Armor Affix Catalog first, then this again.");
                    else pool.Add(affix);
                }
            }

            var bleeding = AssetDatabase.LoadAssetAtPath<AffixDefinition>(BleedingSetup.AffixesFolder + "/OfBleeding.asset");
            if (bleeding == null) Debug.LogWarning("[Affixes] OfBleeding is missing: run Build Bleeding Effect first, then this again.");
            else pool.Add(bleeding);

            EnsurePools<WeaponData>(WeaponFolder, ArmorAffixCatalog.PoolFolder, pool, 0, 2, out int pools, out int assigned, weapon => Weapons.Contains(weapon.name));

            AssetDatabase.SaveAssets();
            EditorApplication.ExecuteMenuItem("SimpleRPG/Definitions/Rebuild Definition Registry");
            Debug.Log($"[Affixes] {made} new weapon affix asset(s), {pools} new pool(s), {assigned} weapon(s) given a pool. Numbers are provisional.");
        }
    }
}
