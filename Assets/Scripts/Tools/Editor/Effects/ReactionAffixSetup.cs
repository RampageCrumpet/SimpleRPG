using SimpleRPG.Abilities;
using SimpleRPG.Gameplay;
using SimpleRPG.InventorySystem;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace SimpleRPG.EffectTools
{
    /// <summary>
    /// Three more triggered affixes, all made of the same ordinary pieces as the bleed (an ability, a status, a <see cref="TriggeredEffect"/>, an affix):
    /// <b>Thorns</b> hurts whoever strikes the wearer (OnTakeDamage), <b>Rampage</b> stacks a damage bonus on the wearer for each kill (OnKill), and
    /// <b>Frostbite</b> slows what the wearer hits (OnHit). Numbers are first guesses. Assets are made only if missing and never changed after.
    /// The affixes are not put in any pool; add them to the pools you want.
    /// </summary>
    public static class ReactionAffixSetup
    {
        /// <summary>An ability a triggered effect runs: deals fixed damage to the effect's target.</summary>
        public static AbilityDefinition BuildDamageAbility(string name, float amount)
        {
            AbilityDefinition ability = ScriptableObject.CreateInstance<AbilityDefinition>();
            ability.name = name;
            ability.SetDetails(name, 0f);
            ability.RemoveNode("activate");
            string entry = ability.AddNode(NodeTypes.TriggerEntry, null, "entry");
            string target = ability.AddNode(NodeTypes.TriggerTarget, null, "target");
            string value = ability.AddNode(NodeTypes.Constant, new ConstantConfig { Value = amount }, "amount");
            string damage = ability.AddNode(NodeTypes.Damage, new DamageConfig { Provenance = DamageProvenance.Proc, Type = DamageType.Physical }, "damage");
            ability.Connect(entry, BuiltInNodes.Next, damage, BuiltInNodes.In);
            ability.Connect(target, BuiltInNodes.Value, damage, BuiltInNodes.Target);
            ability.Connect(value, BuiltInNodes.Value, damage, BuiltInNodes.Amount);
            return ability;
        }

        /// <summary>An ability that puts a status on the effect's target.</summary>
        public static AbilityDefinition BuildApplyAbility(string name, StatusDefinition status)
        {
            AbilityDefinition ability = ScriptableObject.CreateInstance<AbilityDefinition>();
            ability.name = name;
            ability.SetDetails(name, 0f);
            ability.RemoveNode("activate");
            string entry = ability.AddNode(NodeTypes.TriggerEntry, null, "entry");
            string target = ability.AddNode(NodeTypes.TriggerTarget, null, "target");
            string strength = ability.AddNode(NodeTypes.Constant, new ConstantConfig { Value = 1f }, "strength");
            string apply = ability.AddNode(NodeTypes.ApplyStatus, new ApplyStatusConfig { Status = status }, "apply");
            ability.Connect(entry, BuiltInNodes.Next, apply, BuiltInNodes.In);
            ability.Connect(target, BuiltInNodes.Value, apply, BuiltInNodes.Target);
            ability.Connect(strength, BuiltInNodes.Value, apply, BuiltInNodes.PotencyPort);
            return ability;
        }

        /// <summary>A status that changes a stat while it lasts and stacks.</summary>
        public static StatusDefinition BuildStatModifierStatus(string name, string statKey, ModifierLayer layer, double value, float seconds, int maxStacks)
        {
            StatusDefinition status = ScriptableObject.CreateInstance<StatusDefinition>();
            status.name = name;
            status.Configure(name, seconds, 0f, null, maxStacks: maxStacks,
                statModifiers: new[] { new StatusStatModifier { attributeKey = statKey, layer = layer, value = value } });
            return status;
        }

        public static TriggeredEffect BuildEffect(string name, AbilityDefinition ability, EffectTrigger trigger, EffectTarget target, float chance, string description)
        {
            TriggeredEffect effect = ScriptableObject.CreateInstance<TriggeredEffect>();
            effect.name = name;
            effect.Configure(ability, chance, trigger, EffectScope.Owner, target);
            var serialized = new SerializedObject(effect);
            serialized.FindProperty("description").stringValue = description;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return effect;
        }

        /// <summary>The three affixes as data: asset name, name shown, family, and the effect they bring.</summary>
        public static List<(string asset, string display, string family, TriggeredEffect effect)> BuildAll(List<Object> made)
        {
            T Track<T>(T o) where T : Object { made.Add(o); return o; }

            AbilityDefinition thornsAbility = Track(BuildDamageAbility("ThornsStrike", 4f));
            TriggeredEffect thorns = Track(BuildEffect("ThornsOnHurt", thornsAbility, EffectTrigger.OnTakeDamage, EffectTarget.Other, 0.25f, "25% chance to hurt whoever strikes you"));

            StatusDefinition rage = Track(BuildStatModifierStatus("Rampage", DerivedStats.WeaponDamageMultiplier.Key, ModifierLayer.PercentAdd, 0.05, 8f, 5));
            AbilityDefinition rampageAbility = Track(BuildApplyAbility("ApplyRampage", rage));
            TriggeredEffect rampage = Track(BuildEffect("RampageOnKill", rampageAbility, EffectTrigger.OnKill, EffectTarget.Owner, 1f, "Each kill adds 5% weapon damage for 8 seconds (up to 5 times)"));

            StatusDefinition chill = Track(BuildStatModifierStatus("Frostbite", DerivedStats.MovementSpeed.Key, ModifierLayer.PercentAdd, -0.15, 4f, 3));
            AbilityDefinition chillAbility = Track(BuildApplyAbility("ApplyFrostbite", chill));
            TriggeredEffect frostbite = Track(BuildEffect("FrostbiteOnHit", chillAbility, EffectTrigger.OnHit, EffectTarget.Other, 0.2f, "20% chance to slow what you hit by 15%, stacking"));

            return new List<(string, string, string, TriggeredEffect)>
            {
                ("OfThorns", "of Thorns", "thorns", thorns),
                ("OfRampage", "of Rampage", "rampage", rampage),
                ("OfFrost", "of Frost", "frost", frostbite),
            };
        }

        [MenuItem("SimpleRPG/Effects/Build Reaction Affixes")]
        public static void Build()
        {
            const string folder = "Assets/Abilities/Reactions";
            Directory.CreateDirectory(folder);
            Directory.CreateDirectory(ArmorAffixCatalog.AffixFolder);
            var made = new List<Object>();
            var items = BuildAll(made);
            int created = 0;

            // Anything the pieces refer to is saved first, so the affix assets point at saved assets.
            foreach (Object piece in made)
            {
                string path = $"{folder}/{piece.name}.asset";
                if (AssetDatabase.LoadAssetAtPath<Object>(path) != null)
                {
                    continue;
                }

                if (piece is GameplayDefinition definition) definition.EnsureId();
                AssetDatabase.CreateAsset(piece, path);
            }

            foreach (var item in items)
            {
                string path = $"{ArmorAffixCatalog.AffixFolder}/Suffix_{item.asset}.asset";
                if (AssetDatabase.LoadAssetAtPath<AffixDefinition>(path) != null)
                {
                    continue;
                }

                var spec = new AffixCatalogBuilder.Spec { AssetName = item.asset, DisplayName = item.display, Kind = AffixKind.Suffix, Family = item.family, Tier = 1, MinimumItemLevel = 4, Weight = 3f };
                AffixDefinition affix = AffixCatalogBuilder.BuildAffix(spec);
                var serialized = new SerializedObject(affix);
                SerializedProperty effects = serialized.FindProperty("effects");
                effects.arraySize = 1;
                effects.GetArrayElementAtIndex(0).managedReferenceValue = new TriggeredAffixEffect(item.effect);
                serialized.ApplyModifiedPropertiesWithoutUndo();
                affix.EnsureId();
                AssetDatabase.CreateAsset(affix, path);
                created++;
            }

            AssetDatabase.SaveAssets();
            EditorApplication.ExecuteMenuItem("SimpleRPG/Definitions/Rebuild Definition Registry");
            Debug.Log($"[Reactions] {created} new affix(es). They are in no pool yet.");
        }
    }
}
