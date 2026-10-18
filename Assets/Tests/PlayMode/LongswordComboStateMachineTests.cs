using NUnit.Framework;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.Animations;
#endif

namespace SimpleRPG.Tests.PlayMode
{
    /// <summary>
    /// Drives the player's animator through the longsword combo and checks which state it ends up in.
    /// Time is stepped by hand (Time.timeScale is 0), so results do not depend on the frame rate.
    /// Timings from the clip lengths, measured from the first press (the crossfade blends in but does not delay the clip):
    /// attack 1 ends at 1.17 s, its combo runs to 1.45 s, attack 2 runs 1.45 s to 2.37 s, its combo to 2.42 s,
    /// attack 3 runs 2.42 s to 3.40 s. Each recovery is 1.0 s, 0.95 s or 0.98 s long.
    /// </summary>
    public class LongswordComboStateMachineTests
    {
        private const string ControllerPath = "Assets/Prefabs/Characters/Player/PlayerAnimationController.controller";
        private const float StepSize = 1f / 120f;

        private GameObject owner;
        private Animator animator;
        private float elapsed;

        [SetUp]
        public void SetUp()
        {
            Time.timeScale = 0f;

            RuntimeAnimatorController controller = LoadController();
            Assert.IsNotNull(controller, $"Could not load {ControllerPath}.");

            owner = new GameObject("LongswordComboTestAnimator");
            owner.AddComponent<TestAnimationEventReceiver>();
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

        /// <summary>
        /// Loads the player's animator controller. Assets can only be loaded from the editor, so tests are ignored in a player build.
        /// </summary>
        private static RuntimeAnimatorController LoadController()
        {
#if UNITY_EDITOR
            return AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(ControllerPath);
#else
            Assert.Ignore("The longsword controller can only be loaded in the editor.");
            return null;
#endif
        }

        /// <summary>
        /// Advances the animator to an absolute time, in seconds since the test started.
        /// </summary>
        private void AdvanceTo(float time)
        {
            while (elapsed < time - 1e-4f)
            {
                float step = Mathf.Min(StepSize, time - elapsed);
                animator.Update(step);
                elapsed += step;
            }
        }

        /// <summary>
        /// Sets a trigger and processes it immediately.
        /// </summary>
        private void Press(string trigger)
        {
            animator.SetTrigger(trigger);
            animator.Update(0f);
        }

        private bool InState(string stateName)
        {
            return animator.GetCurrentAnimatorStateInfo(0).IsName(stateName);
        }

        /// <summary>
        /// Describes the clip the animator is currently playing, for failure messages.
        /// </summary>
        private string CurrentClipName()
        {
            AnimatorClipInfo[] clips = animator.GetCurrentAnimatorClipInfo(0);
            return clips.Length > 0 ? clips[0].clip.name : "none";
        }

        private void AssertInState(string stateName, string because)
        {
            Assert.IsTrue(InState(stateName), $"{because}: expected {stateName} but was {CurrentClipName()} at {elapsed:F2}s.");
        }

        [Test]
        public void AllLongswordStates_HaveMotions()
        {
#if UNITY_EDITOR
            AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
            int found = 0;
            foreach (ChildAnimatorState child in controller.layers[0].stateMachine.states)
            {
                if (!child.state.name.StartsWith("Longsword_"))
                {
                    continue;
                }

                found++;
                Assert.IsNotNull(child.state.motion, $"{child.state.name} has no motion.");
            }

            Assert.AreEqual(8, found, "Expected eight longsword states in the controller.");
#else
            Assert.Ignore("The longsword controller can only be inspected in the editor.");
#endif
        }

        [Test]
        public void AttackPressFromIdle_StartsAttackOne()
        {
            Press("Attack_OneHanded");
            AdvanceTo(0.5f);

            AssertInState("Longsword_A1_Attack", "A press from Idle should start attack 1");
        }

        [Test]
        public void AttackOne_WithoutQueue_PlaysRecoveryThenIdle()
        {
            Press("Attack_OneHanded");

            AdvanceTo(1.6f); // attack 1 ends at 1.17 s, recovery runs to 2.17 s
            AssertInState("Longsword_A1_Recovery", "Attack 1 with no queued press should hand off to its recovery");

            AdvanceTo(2.7f); // recovery exits to Idle with a 0.1 s crossfade
            AssertInState("Idle", "After the recovery the character should return to Idle");
        }

        [Test]
        public void AttackOne_QueuedDuringSwing_PlaysComboThenAttackTwo()
        {
            Press("Attack_OneHanded");
            AdvanceTo(0.6f);
            Press("ComboQueued");

            AdvanceTo(1.3f); // combo runs from 1.17 s to 1.45 s
            AssertInState("Longsword_A1_Combo", "A queued press should continue into the attack 1 combo");

            AdvanceTo(1.6f);
            AssertInState("Longsword_A2_Attack", "The attack 1 combo should hand straight into attack 2");
        }

        [Test]
        public void AttackTwo_QueuedDuringSwing_PlaysComboThenAttackThree()
        {
            Press("Attack_OneHanded");
            AdvanceTo(0.6f);
            Press("ComboQueued"); // queues attack 2 (attack 1 combo)

            AdvanceTo(1.9f);
            AssertInState("Longsword_A2_Attack", "Attack 2 should be playing");
            Press("ComboQueued"); // queues attack 3 (attack 2 combo)

            AdvanceTo(2.65f);
            AssertInState("Longsword_A3_Attack", "The attack 2 combo should hand straight into attack 3");
        }

        [Test]
        public void AttackThree_NeverBranches_PlaysItsRecovery()
        {
            Press("Attack_OneHanded");
            AdvanceTo(0.6f);
            Press("ComboQueued");
            AdvanceTo(1.9f);
            Press("ComboQueued");
            AdvanceTo(2.8f);
            Press("ComboQueued"); // pressed during attack 3, should be discarded

            AdvanceTo(3.8f); // attack 3 ends at 3.40 s
            AssertInState("Longsword_A3_Recovery", "Attack 3 must always end in its recovery");
        }

        [Test]
        public void QueuePressedDuringAttackThree_IsDiscardedAndDoesNotChainLater()
        {
            Press("Attack_OneHanded");
            AdvanceTo(0.6f);
            Press("ComboQueued");
            AdvanceTo(1.9f);
            Press("ComboQueued");
            AdvanceTo(2.8f);
            Press("ComboQueued"); // consumed by the attack 3 transition, so it must not carry over

            AdvanceTo(5.2f); // back to Idle after attack 3's recovery (ends about 4.4 s)
            AssertInState("Idle", "The chain should have finished before the next attack");

            Press("Attack_OneHanded");
            AdvanceTo(7.0f); // attack 1 ends at 6.37 s and should hand off to its recovery, not a combo
            AssertInState("Longsword_A1_Recovery", "A stale queue must not chain into a combo");
        }

        [Test]
        public void HitboxEvents_OpenWithTheAttackNumberAndCloseEachSwing()
        {
            TestAnimationEventReceiver receiver = owner.GetComponent<TestAnimationEventReceiver>();

            Press("Attack_OneHanded");
            AdvanceTo(0.9f); // attack 1 hitbox opens at 0.93 s
            Assert.AreEqual(0, receiver.ActivationCount, "The hitbox should still be closed before the active frames.");

            AdvanceTo(1.0f);
            Assert.AreEqual(1, receiver.ActivationCount, "The hitbox should open once in attack 1.");
            Assert.AreEqual(1, receiver.LastActivatedAttack, "ActivateWeapon should receive attack number 1.");

            AdvanceTo(1.2f); // the close event sits at the last active frame, 1.15 s after the press
            Assert.AreEqual(1, receiver.DeactivationCount, "The hitbox should close at the end of the active frames.");
        }

        [Test]
        public void PressDuringRecovery_RestartsAttackOne()
        {
            Press("Attack_OneHanded");
            AdvanceTo(1.6f);
            AssertInState("Longsword_A1_Recovery", "Setup: should be in the attack 1 recovery");

            Press("Attack_OneHanded");
            AdvanceTo(2.1f);
            AssertInState("Longsword_A1_Attack", "A press during recovery should start a new attack 1");
        }
    }
}
