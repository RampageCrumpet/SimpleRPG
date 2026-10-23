using NUnit.Framework;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace SimpleRPG.Tests.PlayMode
{
    /// <summary>
    /// Drives the skeleton test animator through the layered overhead and horizontal strikes.
    /// The Weapon_Attack layer (torso, head, sword arm and legs) plays the Attack-tagged state; the OffHand_Attack layer carries the left arm.
    /// Time is stepped by hand (Time.timeScale is 0), so results do not depend on the frame rate.
    /// Both clips are 1.3 s long, with independently retimed damage windows.
    /// </summary>
    public class SkeletonOverheadStateMachineTests
    {
        private const string PrefabPath = "Assets/Prefabs/Characters/Enemies/SkeletonFootman.prefab";
        private const string WeaponAttackLayer = "Weapon_Attack";
        private const string OffHandLayer = "OffHand_Attack";
        private const float StepSize = 1f / 120f;

        private GameObject owner;
        private Animator animator;
        private float elapsed;

        [SetUp]
        public void SetUp()
        {
            Time.timeScale = 0f;

#if UNITY_EDITOR
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            Assert.IsNotNull(prefab, $"Could not load {PrefabPath}.");
            // Copy the gameplay hierarchy without starting network or combat behaviours.
            owner = new GameObject("SkeletonOverheadTest");
            foreach (Transform child in prefab.transform) Object.Instantiate(child.gameObject, owner.transform, false);
#else
            Assert.Ignore("The skeleton prefab can only be loaded in the editor.");
#endif
            owner.AddComponent<TestAnimationEventReceiver>();
            animator = owner.AddComponent<Animator>();
#if UNITY_EDITOR
            animator.runtimeAnimatorController = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>("Assets/Prefabs/Characters/Enemies/SkeletonFootman.controller");
#endif
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            animator.Rebind();
            animator.Update(0f);
            for (int i = 0; i < 420; i++) animator.Update(StepSize); // Finish the spawn/rise state.
            elapsed = 0f;
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(owner);
            Time.timeScale = 1f;
        }

        private void AdvanceTo(float time)
        {
            while (elapsed < time - 1e-4f)
            {
                float step = Mathf.Min(StepSize, time - elapsed);
                animator.Update(step);
                elapsed += step;
            }
        }

        private void Press(string trigger)
        {
            animator.SetTrigger(trigger);
            animator.Update(0f);
        }

        private int Layer(string name)
        {
            int index = animator.GetLayerIndex(name);
            Assert.GreaterOrEqual(index, 0, $"The skeleton controller has no layer named {name}.");
            return index;
        }

        [TestCase("Overhead")]
        [TestCase("Slash")]
        public void AttackPress_PlaysTheAttackTaggedStateOnTheWeaponLayer(string attack)
        {
            int layer = Layer(WeaponAttackLayer);
            Assert.IsFalse(animator.GetCurrentAnimatorStateInfo(layer).IsTag("Attack"), "The weapon layer should be idle before the press.");

            Press(attack);
            AdvanceTo(0.5f);

            Assert.IsTrue(animator.GetCurrentAnimatorStateInfo(layer).IsName(attack), "The weapon layer should be playing the requested attack.");
            Assert.IsTrue(animator.GetCurrentAnimatorStateInfo(layer).IsTag("Attack"), "The requested state should carry the Attack tag.");
        }

        [TestCase("Overhead")]
        [TestCase("Slash")]
        public void AttackPress_AlsoPlaysTheLeftArmLayer(string attack)
        {
            Press(attack);
            AdvanceTo(0.5f);

            Assert.IsTrue(animator.GetCurrentAnimatorStateInfo(Layer(OffHandLayer)).IsName(attack), "The off-hand layer should play the same clip.");
        }

        [TestCase("Overhead")]
        [TestCase("Slash")]
        public void OffHandLayerAtWeightZero_LeavesTheWeaponLayerSwinging(string attack)
        {
            animator.SetLayerWeight(Layer(OffHandLayer), 0f);

            Press(attack);
            AdvanceTo(0.5f);

            Assert.IsTrue(animator.GetCurrentAnimatorStateInfo(Layer(WeaponAttackLayer)).IsTag("Attack"), "A shield variant (off-hand weight 0) must still swing.");
        }

        [TestCase("Overhead", .55f, .75f)]
        [TestCase("Slash", .524389f, .772265f)]
        public void HitboxEvents_OpenAndCloseOncePerSwing(string attack, float opens, float closes)
        {
            TestAnimationEventReceiver receiver = owner.GetComponent<TestAnimationEventReceiver>();

            Press(attack);
            AdvanceTo(opens - .02f);
            Assert.AreEqual(0, receiver.ActivationCount, "The hitbox should be closed before the damage window.");

            AdvanceTo(opens + .02f);
            Assert.AreEqual(1, receiver.ActivationCount, "The hitbox should open at the retimed event.");
            Assert.AreEqual(1, receiver.LastActivatedAttack, "ActivateWeapon should receive attack number 1.");

            AdvanceTo(closes + .02f);
            Assert.AreEqual(1, receiver.DeactivationCount, "The hitbox should close at the retimed event.");
        }

        [TestCase("Overhead")]
        [TestCase("Slash")]
        public void Attack_ReturnsToTheEmptyStateAfterTheClip(string attack)
        {
            int layer = Layer(WeaponAttackLayer);

            Press(attack);
            AdvanceTo(1.5f);

            Assert.IsTrue(animator.GetCurrentAnimatorStateInfo(layer).IsName("Empty"), "The weapon layer should hand back to Empty after the clip.");
            Assert.IsFalse(animator.GetCurrentAnimatorStateInfo(layer).IsTag("Attack"));
        }
    }
}
