using NUnit.Framework;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace SimpleRPG.Tests.PlayMode
{
    /// <summary>
    /// Drives the skeleton test animator through Die, Rise and back to Idle.
    /// Die is 1.6 s and Rise is 2.4 s; Rise hands over to Idle with a 0.5 s crossfade.
    /// The weapon and off-hand layers are hidden while the skeleton is in pieces and come back during the Rise to Idle blend.
    /// Time is stepped by hand (Time.timeScale is 0), so results do not depend on the frame rate.
    /// </summary>
    public class SkeletonDieRiseStateMachineTests
    {
        private const string PrefabPath = "Assets/Art/3d Models/Skeleton/Skeleton_Test.prefab";
        private const float StepSize = 1f / 120f;
        private static readonly string[] LayerNames = { "Weapon_Arm", "Weapon_Attack", "OffHand_Attack" };

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
            animator = owner.GetComponentInChildren<Animator>();
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
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

        private bool InState(string name)
        {
            return animator.GetCurrentAnimatorStateInfo(0).IsName(name);
        }

        private void AssertLayerWeights(float expected, string because)
        {
            foreach (string layerName in LayerNames)
            {
                Assert.AreEqual(expected, animator.GetLayerWeight(animator.GetLayerIndex(layerName)), 0.01f, $"{because}: layer {layerName}.");
            }
        }

        [Test]
        public void DiePress_PlaysDieAndHidesTheWeaponLayers()
        {
            Press("Die");
            AdvanceTo(0.5f);

            Assert.IsTrue(InState("Die"), "A Die press should play Die.");
            AssertLayerWeights(0f, "The weapon layers must be hidden while the skeleton falls apart");
        }

        [Test]
        public void Die_HoldsTheFinalPileUntilRiseIsPressed()
        {
            Press("Die");
            AdvanceTo(3.0f);

            Assert.IsTrue(InState("Die"), "Die should stay in its last pose (the pile), not loop or exit.");
        }

        [Test]
        public void RisePress_AfterDie_PlaysRiseWithTheLayersStillHidden()
        {
            Press("Die");
            AdvanceTo(2.0f);
            Press("Rise");
            AdvanceTo(2.2f);

            Assert.IsTrue(InState("Rise"), "A Rise press after Die should play Rise.");
            AssertLayerWeights(0f, "The weapon layers stay hidden while the bones reassemble");
        }

        [Test]
        public void Rise_FadesIntoIdleAndBringsTheLayersBack()
        {
            Press("Die");
            AdvanceTo(2.0f);
            Press("Rise");

            AdvanceTo(2.0f + 2.4f + 0.7f);

            Assert.IsTrue(InState("Idle"), "Rise should hand over to Idle.");
            AssertLayerWeights(1f, "The weapon layers should be fully back once the crossfade ends");
        }
    }
}
