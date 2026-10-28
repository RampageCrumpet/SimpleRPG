using NUnit.Framework;
using SimpleRPG.Abilities;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace SimpleRPG.Tests.EditMode
{
    /// <summary>
    /// Checks the shipped longsword data: the player's weapon and blade, one clip per attack, the combo asset that times them, and the animator
    /// states the combo plays. These tests read the imported assets, so they run without entering Play Mode.
    /// </summary>
    public class LongswordDataTests
    {
        private const string Attack1Fbx = "Assets/Art/Animations/Longsword/Longsword_Attack_1_1_RightDiagonal.fbx";
        private const string Attack2Fbx = "Assets/Art/Animations/Longsword/Longsword_Attack_1_2_LeftDiagonal.fbx";
        private const string Attack3Fbx = "Assets/Art/Animations/Longsword/Longsword_Attack_1_3_CenterlineThrust.fbx";
        private const string PlayerPrefab = "Assets/Prefabs/Characters/Player/TestCharacter.prefab";
        private const string PlayerController = "Assets/Prefabs/Characters/Player/PlayerAnimationController.controller";
        private const string PlayerWeapon = "Assets/Items/Weapon Data/TestWeaponData.asset";
        private const string AttackLayerName = "Attack";

        private const float FrameTolerance = 1f / 60f;

        [Test]
        public void Player_EquipsProvidedLongswordWithBladeCollider()
        {
            var player = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefab);
            var weapon = player.GetComponentInChildren<Weapon>(true);
            Assert.AreEqual("Longsword", weapon.name);
            var model = weapon.GetComponentInChildren<MeshFilter>(true);
            Assert.AreEqual("Assets/Art/3d Models/Items/Longsword/Longsword.fbx", AssetDatabase.GetAssetPath(model.sharedMesh));
            var collider = weapon.GetComponent<BoxCollider>();
            Assert.IsTrue(collider.isTrigger);
            Assert.Greater(collider.size.y, 1f);
            Assert.Less(collider.size.x, .1f);
        }

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

        private static ComboDefinition LoadCombo()
        {
            WeaponData weapon = AssetDatabase.LoadAssetAtPath<WeaponData>(PlayerWeapon);
            Assert.IsNotNull(weapon, $"Could not load {PlayerWeapon}.");
            Assert.IsNotNull(weapon.Combo, "The player's weapon has no combo.");
            return weapon.Combo;
        }

        [Test]
        public void AttackClips_HaveDesignedLengths()
        {
            // Each attack is one strip: windup, hit, then recovery to the ready pose. Lengths in frames at 60 fps, from Longsword_Combo_ClipRanges.md.
            Assert.AreEqual(130f / 60f, GetClip(Attack1Fbx, "Longsword_A1").length, FrameTolerance);
            Assert.AreEqual(112f / 60f, GetClip(Attack2Fbx, "Longsword_A2").length, FrameTolerance);
            Assert.AreEqual(118f / 60f, GetClip(Attack3Fbx, "Longsword_A3").length, FrameTolerance);
        }

        [Test]
        public void AttackClips_CarryNoAnimationEvents()
        {
            // The combo's data opens and closes the hit window, so a clip event would only duplicate it.
            foreach ((string fbx, string clip) in new[] { (Attack1Fbx, "Longsword_A1"), (Attack2Fbx, "Longsword_A2"), (Attack3Fbx, "Longsword_A3") })
            {
                Assert.IsEmpty(AnimationUtility.GetAnimationEvents(GetClip(fbx, clip)), clip);
            }
        }

        [Test]
        public void TheShippedCombo_IsValid()
        {
            Assert.IsEmpty(LoadCombo().Validate());
        }

        [Test]
        public void TheShippedCombo_HasOneStepPerClip_AndTheDesignedHitWindows()
        {
            ComboDefinition combo = LoadCombo();

            Assert.AreEqual(3, combo.StepCount);
            // Active hit frames at 60 fps, from Longsword_Combo_ClipRanges.md: attack 1 is 56-70, attack 2 is 38-55, attack 3 is 45-59.
            Assert.AreEqual(56f / 60f, combo.Steps[0].HitStart, FrameTolerance);
            Assert.AreEqual(70f / 60f, combo.Steps[0].HitEnd, FrameTolerance);
            Assert.AreEqual(38f / 60f, combo.Steps[1].HitStart, FrameTolerance);
            Assert.AreEqual(55f / 60f, combo.Steps[1].HitEnd, FrameTolerance);
            Assert.AreEqual(45f / 60f, combo.Steps[2].HitStart, FrameTolerance);
            Assert.AreEqual(59f / 60f, combo.Steps[2].HitEnd, FrameTolerance);
        }

        [Test]
        public void TheShippedCombo_ChainsOnlyInsideTheRecovery_LeavingRoomForLatency()
        {
            ComboDefinition combo = LoadCombo();

            for (int i = 0; i < combo.StepCount - 1; i++)
            {
                ComboStep step = combo.Steps[i];
                Assert.GreaterOrEqual(step.ChainOpen, step.HitEnd, $"Step {i + 1} must not chain before its hit ends.");
                Assert.GreaterOrEqual(step.Duration - step.ChainClose, (float)SimpleRPG.Abilities.Integration.ComboMeleeDelivery.MaxRewindSeconds,
                    $"Step {i + 1} closes its chain window too close to its end for a delayed press to arrive in time.");
            }
        }

        [Test]
        public void EveryComboStep_PlaysAnAttackLayerStateWhoseMotionIsItsClip()
        {
            AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(PlayerController);
            AnimatorControllerLayer layer = controller.layers.Single(l => l.name == AttackLayerName);
            Assert.IsNotNull(layer.avatarMask, "The attack layer must be masked so the legs keep moving.");

            foreach (ComboStep step in LoadCombo().Steps)
            {
                AnimatorState state = layer.stateMachine.states.Select(s => s.state).SingleOrDefault(s => s.name == step.State);
                Assert.IsNotNull(state, $"The attack layer has no state '{step.State}'.");
                Assert.AreEqual(step.Clip, state.motion, $"State '{step.State}' must play the step's clip.");
            }
        }

        [Test]
        public void TheAnimator_HasNoLegacyComboStatesOrTriggers()
        {
            AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(PlayerController);

            Assert.IsFalse(controller.parameters.Any(p => p.name == "ComboQueued" || p.name == "Attack_OneHanded"));
            foreach (AnimatorControllerLayer layer in controller.layers)
            {
                Assert.IsFalse(layer.stateMachine.states.Any(s => s.state.name.EndsWith("_Combo") || s.state.name.EndsWith("_Recovery")), layer.name);
            }
        }

        [Test]
        public void AttackLayer_RestsInAHeldReadyPoseTakenFromTheFirstAttackClip()
        {
            AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(PlayerController);
            AnimatorStateMachine machine = controller.layers.Single(l => l.name == AttackLayerName).stateMachine;
            AnimatorState ready = machine.states.Select(s => s.state).SingleOrDefault(s => s.name == "Longsword_Ready");
            Assert.IsNotNull(ready, "The attack layer needs a state that holds the ready stance.");

            AnimatorState first = machine.states.Select(s => s.state).Single(s => s.name == "Longsword_A1");
            Assert.AreSame(first.motion, ready.motion, "The ready pose reuses the A1 clip instead of a copy.");
            Assert.AreEqual(0f, ready.speed, "The ready pose is held, not played.");
            Assert.AreEqual(0f, ready.cycleOffset, "The ready pose is the clip's first frame, which A1 also ends on.");
            Assert.AreEqual(0, ready.transitions.Length, "The ready state is left by the attack code, not by transitions.");

            foreach (string attack in new[] { "Longsword_A1", "Longsword_A2", "Longsword_A3" })
            {
                AnimatorState state = machine.states.Select(s => s.state).Single(s => s.name == attack);
                Assert.IsTrue(state.transitions.Length > 0 && state.transitions.All(t => t.destinationState == ready), $"{attack} must recover into the ready stance, not drop to the base layer.");
            }
        }

        [Test]
        public void FirstAttackClip_EndsOnItsStartingPose()
        {
            AnimationClip clip = GetClip(Attack1Fbx, "Longsword_A1");
            foreach (EditorCurveBinding binding in AnimationUtility.GetCurveBindings(clip).Where(b => b.propertyName.StartsWith("Right ")))
            {
                AnimationCurve curve = AnimationUtility.GetEditorCurve(clip, binding);
                Assert.AreEqual(curve.Evaluate(0f), curve.Evaluate(clip.length), 0.02f, binding.propertyName);
            }
        }
    }
}
