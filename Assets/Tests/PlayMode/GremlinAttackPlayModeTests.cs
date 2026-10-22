using NUnit.Framework;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace SimpleRPG.Tests.PlayMode
{
    /// <summary>
    /// Plays the enemy's one-handed attack through its own controller and checks the hitbox events it fires.
    /// Time is stepped by hand (Time.timeScale is 0), so results do not depend on the frame rate.
    /// The original clip opens the hitbox at 0.17 s and closes it at 0.50 s.
    /// </summary>
    public class GremlinAttackPlayModeTests
    {
        private const string ControllerPath = "Assets/Prefabs/Characters/Enemies/GremlinAnimationController.controller";
        private const string AttackStateName = "hum_male_meleeattack_1H_Weapon_01";
        private const float StepSize = 1f / 120f;

        private GameObject owner;
        private Animator animator;
        private TestAnimationEventReceiver receiver;
        private float elapsed;

        [SetUp]
        public void SetUp()
        {
            Time.timeScale = 0f;

            RuntimeAnimatorController controller = LoadController();
            Assert.IsNotNull(controller, $"Could not load {ControllerPath}.");

            owner = new GameObject("GremlinAttackTestAnimator");
            receiver = owner.AddComponent<TestAnimationEventReceiver>();
            animator = owner.AddComponent<Animator>();
            animator.runtimeAnimatorController = controller;
            animator.Update(0f);
            elapsed = 0f;
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(owner);
            Time.timeScale = 1f;
        }

        private static RuntimeAnimatorController LoadController()
        {
#if UNITY_EDITOR
            return AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(ControllerPath);
#else
            Assert.Ignore("The Gremlin controller can only be loaded in the editor.");
            return null;
#endif
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

        [Test]
        public void AttackPress_PlaysTheOneHandedAttack()
        {
            Press("Attack_OneHanded");
            AdvanceTo(0.3f);

            Assert.IsTrue(animator.GetCurrentAnimatorStateInfo(0).IsName(AttackStateName),
                "A press should play the enemy's one-handed attack.");
        }

        [Test]
        public void AttackPress_OpensAndClosesTheHitbox_WithNoAttackNumber()
        {
            Press("Attack_OneHanded");

            AdvanceTo(0.1f);
            Assert.AreEqual(0, receiver.ActivationCount, "The hitbox should still be closed before the first active frame.");

            AdvanceTo(0.3f); // the old clip opens the hitbox at 0.17 s
            Assert.AreEqual(1, receiver.ActivationCount, "The ActivateWeapon event should open the hitbox once.");
            // The enemy clip has no attack number, so its event passes 0. MeleeDelivery treats 0 as full damage.
            Assert.AreEqual(0, receiver.LastActivatedAttack, "The enemy clip's ActivateWeapon event should pass attack number 0.");

            AdvanceTo(0.6f); // and closes it at 0.50 s
            Assert.AreEqual(1, receiver.DeactivationCount, "The DeactivateWeaponH event should close the hitbox once.");
        }
    }
}
