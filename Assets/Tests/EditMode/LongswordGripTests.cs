using NUnit.Framework;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace SimpleRPG.Tests.EditMode
{
    /// <summary>
    /// Checks that the longsword sits in the character's hands. The attack clips were authored with both hands on the hilt, so with the
    /// clip posed the sword's grip must run through both palms. These tests pose the real prefab, so they catch a mount whose rotation or
    /// offset does not match the rig's hand axes.
    /// </summary>
    public class LongswordGripTests
    {
        private const string PlayerPrefab = "Assets/Prefabs/Characters/Player/TestCharacter.prefab";
        private const string Attack1Fbx = "Assets/Art/Animations/Longsword/Longsword_Attack_1_1_RightDiagonal.fbx";

        /// <summary>Closed-hand palm reference on this model, matching HandGrip's default reference.</summary>
        private static readonly Vector3 PalmOffset = new Vector3(0f, 0.09f, -0.025f);

        /// <summary>How far a palm may sit from the sword's centre line.</summary>
        private const float MaxPalmOffAxis = 0.03f;

        /// <summary>The hilt on the mesh, in metres along the blade axis from the sword's origin, with slack for hand width.</summary>
        private const float GripMin = -0.17f;
        private const float GripMax = 0.17f;

        /// <summary>Times in the first attack, before the swing starts, when both hands are still on the hilt.</summary>
        private static readonly float[] GripTimes = { 0f, 0.4f, 0.8f };

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
        public void BothPalms_LieOnTheSwordGripWhileTheSwingWindsUp()
        {
            foreach (float time in GripTimes)
            {
                clip.SampleAnimation(animator.gameObject, time);
                AssertPalmOnGrip(rightHand, "right", time);
                AssertPalmOnGrip(leftHand, "left", time);
            }
        }

        [Test]
        public void AllAttackFrames_KeepTheLeftWristAtItsAuthoredGrip()
        {
            var grip = sword.GetComponentsInChildren<SimpleRPG.Animations.HandGrip>()
                .Single(g => g.Hand == SimpleRPG.Animations.GripHand.Left);
            foreach (string path in new[] { Attack1Fbx,
                "Assets/Art/Animations/Longsword/Longsword_Attack_1_2_LeftDiagonal.fbx",
                "Assets/Art/Animations/Longsword/Longsword_Attack_1_3_CenterlineThrust.fbx" })
            {
                var attack = AssetDatabase.LoadAllAssetRepresentationsAtPath(path)
                    .OfType<AnimationClip>().Single(c => !c.name.StartsWith("__"));
                for (int frame = 0; frame <= Mathf.RoundToInt(attack.length * 60f); frame++)
                {
                    attack.SampleAnimation(animator.gameObject, frame / 60f);
                    grip.GetHandPose(out Vector3 position, out Quaternion rotation);
                    // Imported Humanoid curves are compressed. Runtime IK removes this small residual.
                    Assert.Less(Vector3.Distance(leftHand.position, position), 0.005f, $"{attack.name}, frame {frame}");
                    Assert.Less(Quaternion.Angle(leftHand.rotation, rotation), 1.1f, $"{attack.name}, frame {frame}");
                }
            }
        }

        [Test]
        public void BladePointsAwayFromTheLeftHand_PommelIsAtTheLowerHand()
        {
            clip.SampleAnimation(animator.gameObject, 0f);
            float right = AlongBlade(rightHand);
            float left = AlongBlade(leftHand);
            Assert.Greater(right, left, "The trailing (left) hand holds the pommel end, so the blade runs away from it.");
        }

        private void AssertPalmOnGrip(Transform hand, string side, float time)
        {
            Vector3 palm = hand.TransformPoint(PalmOffset) - sword.position;
            float along = Vector3.Dot(palm, sword.up);
            float off = (palm - sword.up * along).magnitude;
            Assert.LessOrEqual(off, MaxPalmOffAxis, $"The {side} palm is {off * 100f:F1} cm off the sword axis at t={time}.");
            Assert.That(along, Is.InRange(GripMin, GripMax), $"The {side} palm is {along * 100f:F1} cm along the blade, outside the hilt, at t={time}.");
        }

        private float AlongBlade(Transform hand) => Vector3.Dot(hand.TransformPoint(PalmOffset) - sword.position, sword.up);
    }
}
