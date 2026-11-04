using SimpleRPG.Abilities;
using SimpleRPG.Gameplay;
using SimpleRPG.InventorySystem;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace SimpleRPG.EffectTools
{
    /// <summary>
    /// The first real triggered effect, and the smallest one that exercises the whole chain: a chance on a weapon hit to put a stacking
    /// damage-over-time status on whoever was struck. It is made of ordinary assets: an ability that applies the status, the status with its tick
    /// ability, a <see cref="TriggeredEffect"/> that decides when, and an affix that gives it to an item. Nothing here is code that knows about bleeding.
    /// <para>
    /// Safe to run again: an asset that exists is <b>never</b> changed, so the numbers (the chance, the damage, the duration) belong to the designer
    /// from the first run. The values below are a starting point. The affix is not added to any item's pool; that is a content decision.
    /// </para>
    /// </summary>
    public static class BleedingSetup
    {
        public const string AbilitiesFolder = "Assets/Abilities/Abilities";
        public const string StatusesFolder = "Assets/Abilities/Statuses";
        public const string EffectsFolder = "Assets/Abilities/Effects";
        public const string AffixesFolder = "Assets/Items/Affixes";

        public const float DamagePerStackPerTick = 2f;
        public const float DurationSeconds = 6f;
        public const float TickSeconds = 1f;
        public const int MaxStacks = 5;
        public const float ChanceOnHit = 0.2f;

        /// <summary>The tick ability: each tick damages the holder by the status's strength, as a bleed that ignores armor.</summary>
        public static AbilityDefinition BuildTickAbility()
        {
            AbilityDefinition ability = ScriptableObject.CreateInstance<AbilityDefinition>();
            ability.name = "BleedTick";
            ability.SetDetails("Bleed Tick", 0f);
            ability.RemoveNode("activate");
            string tick = ability.AddNode(NodeTypes.StatusTick, null, "tick");
            string holder = ability.AddNode(NodeTypes.StatusHolder, null, "holder");
            string potency = ability.AddNode(NodeTypes.StatusPotency, null, "potency");
            string damage = ability.AddNode(NodeTypes.Damage,
                new DamageConfig { Provenance = DamageProvenance.Periodic, Type = DamageType.Physical, BypassesMitigation = true }, "damage");
            ability.Connect(tick, BuiltInNodes.Next, damage, BuiltInNodes.In);
            ability.Connect(holder, BuiltInNodes.Value, damage, BuiltInNodes.Target);
            ability.Connect(potency, BuiltInNodes.Value, damage, BuiltInNodes.Amount);
            return ability;
        }

        /// <summary>The status: stacks, shares one timer, and ticks the tick ability. Each hit that bleeds adds a stack.</summary>
        public static StatusDefinition BuildStatus(AbilityDefinition tickAbility)
        {
            StatusDefinition status = ScriptableObject.CreateInstance<StatusDefinition>();
            status.name = "Bleeding";
            status.Configure("Bleeding", DurationSeconds, TickSeconds, tickAbility, maxStacks: MaxStacks);
            return status;
        }

        /// <summary>The ability a triggered effect runs: put the status on whoever the effect is aimed at.</summary>
        public static AbilityDefinition BuildApplyAbility(StatusDefinition status)
        {
            AbilityDefinition ability = ScriptableObject.CreateInstance<AbilityDefinition>();
            ability.name = "ApplyBleeding";
            ability.SetDetails("Apply Bleeding", 0f);
            ability.RemoveNode("activate");
            string entry = ability.AddNode(NodeTypes.TriggerEntry, null, "entry");
            string target = ability.AddNode(NodeTypes.TriggerTarget, null, "target");
            string strength = ability.AddNode(NodeTypes.Constant, new ConstantConfig { Value = DamagePerStackPerTick }, "strength");
            string apply = ability.AddNode(NodeTypes.ApplyStatus, new ApplyStatusConfig { Status = status }, "apply");
            ability.Connect(entry, BuiltInNodes.Next, apply, BuiltInNodes.In);
            ability.Connect(target, BuiltInNodes.Value, apply, BuiltInNodes.Target);
            ability.Connect(strength, BuiltInNodes.Value, apply, BuiltInNodes.PotencyPort);
            return ability;
        }

        [MenuItem("SimpleRPG/Effects/Build Bleeding Effect")]
        public static void Build()
        {
            Directory.CreateDirectory(AbilitiesFolder);
            Directory.CreateDirectory(StatusesFolder);
            Directory.CreateDirectory(EffectsFolder);
            Directory.CreateDirectory(AffixesFolder);

            AbilityDefinition tick = LoadOrCreate(AbilitiesFolder + "/BleedTick.asset", BuildTickAbility, out bool madeTick);
            StatusDefinition status = LoadOrCreate(StatusesFolder + "/Bleeding.asset", () => BuildStatus(tick), out bool madeStatus);
            AbilityDefinition apply = LoadOrCreate(AbilitiesFolder + "/ApplyBleeding.asset", () => BuildApplyAbility(status), out bool madeApply);
            TriggeredEffect effect = LoadOrCreate(EffectsFolder + "/BleedOnHit.asset", () => BuildEffect(apply), out bool madeEffect);
            AffixDefinition affix = LoadOrCreate(AffixesFolder + "/OfBleeding.asset", () => BuildAffix(effect), out bool madeAffix);

            AssetDatabase.SaveAssets();
            EditorApplication.ExecuteMenuItem("SimpleRPG/Definitions/Rebuild Definition Registry");
            Debug.Log($"[Bleeding] tick {(madeTick ? "made" : "kept")}, status {(madeStatus ? "made" : "kept")}, apply {(madeApply ? "made" : "kept")}, " +
                      $"effect {(madeEffect ? "made" : "kept")}, affix {(madeAffix ? "made" : "kept")}. The affix is in no item's pool.");
        }

        public static TriggeredEffect BuildEffect(AbilityDefinition apply)
        {
            TriggeredEffect effect = ScriptableObject.CreateInstance<TriggeredEffect>();
            effect.name = "BleedOnHit";
            effect.Configure(apply, ChanceOnHit, EffectTrigger.OnHit, EffectScope.Owner, EffectTarget.Other);
            var serialized = new SerializedObject(effect);
            serialized.FindProperty("description").stringValue = $"{ChanceOnHit * 100f:0}% chance to cause bleeding on hit";
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return effect;
        }

        public static AffixDefinition BuildAffix(TriggeredEffect effect)
        {
            AffixDefinition affix = ScriptableObject.CreateInstance<AffixDefinition>();
            affix.name = "OfBleeding";
            var serialized = new SerializedObject(affix);
            serialized.FindProperty("displayName").stringValue = "of Bleeding";
            serialized.FindProperty("kind").enumValueIndex = (int)AffixKind.Suffix;
            serialized.FindProperty("family").stringValue = "bleeding";
            SerializedProperty effects = serialized.FindProperty("effects");
            effects.arraySize = 1;
            effects.GetArrayElementAtIndex(0).managedReferenceValue = new TriggeredAffixEffect(effect);
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return affix;
        }

        // Loads the asset at the path, or builds and saves it. An asset that exists is returned untouched.
        private static T LoadOrCreate<T>(string path, System.Func<T> build, out bool created) where T : Object
        {
            T existing = AssetDatabase.LoadAssetAtPath<T>(path);
            created = existing == null;
            if (!created)
            {
                return existing;
            }

            T made = build();
            if (made is GameplayDefinition definition) definition.EnsureId();
            AssetDatabase.CreateAsset(made, path);
            return made;
        }
    }
}
