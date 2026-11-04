using NUnit.Framework;
using SimpleRPG.Animations;
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

        [Test]
        public void BothHandsHaveAGripThatClosesAllFifteenFingerBones()
        {
            HandGrip[] grips = player.GetComponentsInChildren<HandGrip>(true);

            Assert.AreEqual(2, grips.Length, "one grip per hand");
            Assert.AreEqual(1, grips.Count(g => g.Hand == GripHand.Left));
            Assert.AreEqual(1, grips.Count(g => g.Hand == GripHand.Right));
            foreach (HandGrip grip in grips) Assert.AreEqual(15, grip.FingerPose.Count, grip.name);
        }

        [Test]
        public void ClosedFingers_WrapTheHandleOnBothGrips()
        {
            foreach (HandGrip grip in player.GetComponentsInChildren<HandGrip>(true))
            {
                Transform hand = animator.GetBoneTransform(grip.Hand == GripHand.Left ? HumanBodyBones.LeftHand : HumanBodyBones.RightHand);
                grip.GetHandPose(out Vector3 position, out Quaternion rotation);
                hand.SetPositionAndRotation(position, rotation);
                foreach (FingerBone finger in grip.FingerPose) animator.GetBoneTransform(finger.bone).localRotation = finger.localRotation;

                foreach (string name in new[] { "Index", "Middle", "Ring", "Little" })
                {
                    Transform distal = animator.GetBoneTransform((HumanBodyBones)System.Enum.Parse(typeof(HumanBodyBones), grip.Hand + name + "Distal"));
                    Vector3 local = sword.InverseTransformPoint(distal.position);
                    float fromAxis = Mathf.Sqrt(local.x * local.x + local.z * local.z);
                    Assert.That(fromAxis, Is.InRange(0.005f, 0.04f), grip.Hand + " " + name + " fingertip joint is " + (fromAxis * 100f).ToString("0.0") + " cm from the hilt axis; it should rest on the handle");
                }
            }
        }

        private float AlongBlade(Transform hand) => Vector3.Dot(hand.TransformPoint(PalmOffset) - sword.position, sword.up);
    }
}
