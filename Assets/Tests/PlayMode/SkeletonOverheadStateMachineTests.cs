using NUnit.Framework;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace SimpleRPG.Tests.PlayMode
{
    /// <summary>
    /// Drives the skeleton test animator through the layered overhead strike.
    /// The Weapon_Attack layer (torso, head, sword arm and legs) plays the Attack-tagged state; the OffHand_Attack layer carries the left arm.
    /// Time is stepped by hand (Time.timeScale is 0), so results do not depend on the frame rate.
    /// The clip is 3.0 s long: the hitbox opens at 1.6 s and closes at 1.8 s.
    /// </summary>
    public class SkeletonOverheadStateMachineTests
    {
        private const string PrefabPath = "Assets/Art/3d Models/Skeleton/Skeleton_Test.prefab";
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
            owner = Object.Instantiate(prefab);
#else
            Assert.Ignore("The skeleton prefab can only be loaded in the editor.");
#endif
            owner.AddComponent<TestAnimationEventReceiver>();
            animator = owner.GetComponentInChildren<Animator>();
            animator.Update(0f);
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

        [Test]
        public void OverheadPress_PlaysTheAttackTaggedStateOnTheWeaponLayer()
        {
            int layer = Layer(WeaponAttackLayer);
            Assert.IsFalse(animator.GetCurrentAnimatorStateInfo(layer).IsTag("Attack"), "The weapon layer should be idle before the press.");

            Press("Overhead");
            AdvanceTo(0.5f);

            Assert.IsTrue(animator.GetCurrentAnimatorStateInfo(layer).IsName("Overhead"), "The weapon layer should be playing Overhead.");
            Assert.IsTrue(animator.GetCurrentAnimatorStateInfo(layer).IsTag("Attack"), "The Overhead state should carry the Attack tag.");
        }

        [Test]
        public void OverheadPress_AlsoPlaysTheLeftArmLayer()
        {
            Press("Overhead");
            AdvanceTo(0.5f);

            Assert.IsTrue(animator.GetCurrentAnimatorStateInfo(Layer(OffHandLayer)).IsName("Overhead"), "The off-hand layer should play the same clip.");
        }

        [Test]
        public void OffHandLayerAtWeightZero_LeavesTheWeaponLayerSwinging()
        {
            animator.SetLayerWeight(Layer(OffHandLayer), 0f);

            Press("Overhead");
            AdvanceTo(0.5f);

            Assert.IsTrue(animator.GetCurrentAnimatorStateInfo(Layer(WeaponAttackLayer)).IsTag("Attack"), "A shield variant (off-hand weight 0) must still swing.");
        }

        [Test]
        public void HitboxEvents_OpenAndCloseOncePerSwing()
        {
            TestAnimationEventReceiver receiver = owner.GetComponent<TestAnimationEventReceiver>();

            Press("Overhead");
            AdvanceTo(1.5f);
            Assert.AreEqual(0, receiver.ActivationCount, "The hitbox should still be closed before 1.6 s.");

            AdvanceTo(1.7f);
            Assert.AreEqual(1, receiver.ActivationCount, "The hitbox should open at 1.6 s.");
            Assert.AreEqual(1, receiver.LastActivatedAttack, "ActivateWeapon should receive attack number 1.");

            AdvanceTo(1.9f);
            Assert.AreEqual(1, receiver.DeactivationCount, "The hitbox should close at 1.8 s.");
        }

        [Test]
        public void Overhead_ReturnsToTheEmptyStateAfterTheClip()
        {
            int layer = Layer(WeaponAttackLayer);

            Press("Overhead");
            AdvanceTo(3.6f);

            Assert.IsTrue(animator.GetCurrentAnimatorStateInfo(layer).IsName("Empty"), "The weapon layer should hand back to Empty after the clip.");
            Assert.IsFalse(animator.GetCurrentAnimatorStateInfo(layer).IsTag("Attack"));
        }
    }
}
