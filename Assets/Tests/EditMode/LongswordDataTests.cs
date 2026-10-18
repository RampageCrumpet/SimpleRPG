using NUnit.Framework;
using SimpleRPG.Animations;
using UnityEditor;
using UnityEngine;

namespace SimpleRPG.Tests.EditMode
{
    /// <summary>
    /// Checks the longsword combo data: clip lengths, hitbox event timing, damage scalars and the trigger helpers.
    /// These tests read the imported assets, so they run without entering Play Mode.
    /// </summary>
    public class LongswordDataTests
    {
        private const string Attack1Fbx = "Assets/Art/Animations/Longsword/Longsword_Attack_1_1_RightDiagonal.fbx";
        private const string Attack2Fbx = "Assets/Art/Animations/Longsword/Longsword_Attack_1_2_LeftDiagonal.fbx";
        private const string Attack3Fbx = "Assets/Art/Animations/Longsword/Longsword_Attack_1_3_CenterlineThrust.fbx";
        private const string PlayerPrefab = "Assets/Prefabs/Characters/Player/TestCharacter.prefab";

        private const float FrameTolerance = 1f / 60f;

        /// <summary>
        /// Finds an imported animation clip by name inside one of the longsword FBX files.
        /// </summary>
        private static AnimationClip GetClip(string fbxPath, string clipName)
        {
            foreach (Object asset in AssetDatabase.LoadAllAssetRepresentationsAtPath(fbxPath))
            {
                if (asset is AnimationClip clip && clip.name == clipName)
                {
                    return clip;
                }
            }

            Assert.Fail($"Clip {clipName} not found in {fbxPath}.");
            return null;
        }

        /// <summary>
        /// Reads the damage scalars from the player's melee behaviour.
        /// </summary>
        private static float[] GetComboDamageScalars()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefab);
            Assert.IsNotNull(prefab, $"Could not load {PlayerPrefab}.");

            MeleeAttackAbilityBehaviour behaviour = prefab.GetComponentInChildren<MeleeAttackAbilityBehaviour>(true);
            Assert.IsNotNull(behaviour, "The player prefab has no MeleeAttackAbilityBehaviour.");

            SerializedObject serialized = new SerializedObject(behaviour);
            SerializedProperty scalars = serialized.FindProperty("comboDamageScalars");
            float[] values = new float[scalars.arraySize];
            for (int i = 0; i < values.Length; i++)
            {
                values[i] = scalars.GetArrayElementAtIndex(i).floatValue;
            }

            return values;
        }

        [Test]
        public void AttackClips_HaveDesignedLengths()
        {
            // Lengths in frames at 60 fps, from Longsword_Combo_ClipRanges.md.
            Assert.AreEqual(70f / 60f, GetClip(Attack1Fbx, "Longsword_A1_Attack").length, FrameTolerance);
            Assert.AreEqual(55f / 60f, GetClip(Attack2Fbx, "Longsword_A2_Attack").length, FrameTolerance);
            Assert.AreEqual(59f / 60f, GetClip(Attack3Fbx, "Longsword_A3_Attack").length, FrameTolerance);
        }

        [Test]
        public void ComboAndRecoveryClips_HaveDesignedLengths()
        {
            Assert.AreEqual(17f / 60f, GetClip(Attack1Fbx, "Longsword_A1_Combo").length, FrameTolerance);
            Assert.AreEqual(60f / 60f, GetClip(Attack1Fbx, "Longsword_A1_Recovery").length, FrameTolerance);
            Assert.AreEqual(3f / 60f, GetClip(Attack2Fbx, "Longsword_A2_Combo").length, FrameTolerance);
            Assert.AreEqual(57f / 60f, GetClip(Attack2Fbx, "Longsword_A2_Recovery").length, FrameTolerance);
            Assert.AreEqual(59f / 60f, GetClip(Attack3Fbx, "Longsword_A3_Recovery").length, FrameTolerance);
        }

        [Test]
        public void HitboxEvents_OpenAndCloseInsideEachAttack()
        {
            // Attack number, activate frame (design hit window start) and the clip they belong to.
            CheckHitboxEvents(Attack1Fbx, "Longsword_A1_Attack", attackNumber: 1, activateFrame: 56, lastFrame: 70);
            CheckHitboxEvents(Attack2Fbx, "Longsword_A2_Attack", attackNumber: 2, activateFrame: 38, lastFrame: 55);
            CheckHitboxEvents(Attack3Fbx, "Longsword_A3_Attack", attackNumber: 3, activateFrame: 45, lastFrame: 59);
        }

        private static void CheckHitboxEvents(string fbxPath, string clipName, int attackNumber, int activateFrame, int lastFrame)
        {
            AnimationClip clip = GetClip(fbxPath, clipName);
            AnimationEvent[] events = AnimationUtility.GetAnimationEvents(clip);

            AnimationEvent activate = null;
            AnimationEvent deactivate = null;
            foreach (AnimationEvent evt in events)
            {
                if (evt.functionName == "ActivateWeapon") activate = evt;
                if (evt.functionName == "DeactivateWeaponH") deactivate = evt;
            }

            Assert.IsNotNull(activate, $"{clipName} has no ActivateWeapon event.");
            Assert.IsNotNull(deactivate, $"{clipName} has no DeactivateWeaponH event.");
            Assert.AreEqual(attackNumber, activate.intParameter, $"{clipName} ActivateWeapon must pass attack number {attackNumber}.");
            Assert.AreEqual(activateFrame / 60f, activate.time, FrameTolerance, $"{clipName} hitbox opens at the wrong time.");
            Assert.Less(activate.time, deactivate.time, $"{clipName} hitbox must open before it closes.");
            Assert.Less(deactivate.time, clip.length, $"{clipName} hitbox must close inside the clip.");
            Assert.GreaterOrEqual(deactivate.time, (lastFrame - 2) / 60f, $"{clipName} hitbox closes too early.");
        }

        [Test]
        public void DamageScalars_MatchClipLengthRatios()
        {
            // The rule from the design: each attack's scalar is its clip length relative to Attack 1, to two decimals.
            float attack1 = GetClip(Attack1Fbx, "Longsword_A1_Attack").length;
            float[] expected =
            {
                1f,
                Mathf.Round(GetClip(Attack2Fbx, "Longsword_A2_Attack").length / attack1 * 100f) / 100f,
                Mathf.Round(GetClip(Attack3Fbx, "Longsword_A3_Attack").length / attack1 * 100f) / 100f,
            };

            float[] actual = GetComboDamageScalars();

            Assert.AreEqual(expected.Length, actual.Length, "Expected one damage scalar per attack.");
            for (int i = 0; i < expected.Length; i++)
            {
                Assert.AreEqual(expected[i], actual[i], 0.005f, $"Damage scalar for attack {i + 1} does not match its clip-length ratio.");
            }
        }

        [Test]
        public void ScaleDamage_RoundsToNearestWholePoint()
        {
            Assert.AreEqual(10, Weapon.ScaleDamage(10, 1f));
            Assert.AreEqual(8, Weapon.ScaleDamage(10, 0.79f));
            Assert.AreEqual(8, Weapon.ScaleDamage(10, 0.84f));
            Assert.AreEqual(1, Weapon.ScaleDamage(1, 0.79f));
            Assert.AreEqual(0, Weapon.ScaleDamage(0, 1.15f));
        }

        [Test]
        public void GetAttackTrigger_ChoosesTriggerForEachState()
        {
            Assert.AreEqual("ComboQueued", AnimatableCharacter.GetAttackTrigger(inAttack: true, inCombo: false));
            Assert.IsNull(AnimatableCharacter.GetAttackTrigger(inAttack: false, inCombo: true));
            Assert.AreEqual("Attack_OneHanded", AnimatableCharacter.GetAttackTrigger(inAttack: false, inCombo: false));
        }
    }
}
