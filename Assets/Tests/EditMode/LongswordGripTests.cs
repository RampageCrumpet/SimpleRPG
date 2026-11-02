using NUnit.Framework;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace SimpleRPG.Tests.EditMode
{
    /// <summary>
    /// Checks that the longsword is mounted on the right hand and that the blade points away from the left. Whether the hands sit
    /// right on the hilt through the swing is judged by eye, not here.
    /// </summary>
    public class LongswordGripTests
    {
        private const string PlayerPrefab = "Assets/Prefabs/Characters/Player/TestCharacter.prefab";
        private const string Attack1Fbx = "Assets/Art/Animations/Longsword/Longsword_Attack_1_1_RightDiagonal.fbx";

        /// <summary>Closed-hand palm reference on this model, matching HandGrip's default reference.</summary>
        private static readonly Vector3 PalmOffset = new Vector3(0f, 0.09f, -0.025f);

        private GameObject player;
        private Transform sword;
        private Transform rightHand;
        private Transform leftHand;
        private Animator animator;
        private AnimationClip clip;

        [SetUp]
        public void SetUp()
        {
            player = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefab));
            Transform[] all = player.GetComponentsInChildren<Transform>(true);
            sword = all.Single(t => t.name == "Longsword");
            rightHand = all.Single(t => t.name == "RightHand");
            leftHand = all.Single(t => t.name == "LeftHand");
            animator = player.GetComponentInChildren<Animator>();
            clip = AssetDatabase.LoadAllAssetRepresentationsAtPath(Attack1Fbx).OfType<AnimationClip>().Single(c => c.name == "Longsword_A1");
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(player);
        }

        [Test]
        public void Sword_IsMountedOnTheRightHand()
        {
            Assert.AreSame(rightHand, sword.parent);
        }

        [Test]
        public void BladePointsAwayFromTheLeftHand_PommelIsAtTheLowerHand()
        {
            clip.SampleAnimation(animator.gameObject, 0f);
            float right = AlongBlade(rightHand);
            float left = AlongBlade(leftHand);
            Assert.Greater(right, left, "The trailing (left) hand holds the pommel end, so the blade runs away from it.");
        }

        private float AlongBlade(Transform hand) => Vector3.Dot(hand.TransformPoint(PalmOffset) - sword.position, sword.up);
    }
}
