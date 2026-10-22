using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace SimpleRPG.Tests.EditMode
{
    public class SkeletonGameplayAnimationTests
    {
        private GameObject skeleton;
        private Animator animator;

        [SetUp]
        public void SetUp()
        {
            skeleton = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Characters/Enemies/SkeletonFootman.prefab"));
            animator = skeleton.GetComponent<Animator>();
            animator.fireEvents = false; // Pose checks run without a network session or combat initialization.
            animator.Rebind();
            animator.Update(0);
            Step(3.5f);
        }

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(skeleton);

        private void Step(float seconds)
        {
            for (int i = 0; i < Mathf.CeilToInt(seconds * 120); i++) animator.Update(1f / 120);
        }

        [Test]
        public void Walk_WithWeaponLayers_StepsBothFeetAndStaysInPlace()
        {
            animator.SetBool("Walk", true);
            Step(.6f);
            foreach (string name in new[] { "LeftFoot", "RightFoot" })
            {
                Transform foot = skeleton.GetComponentsInChildren<Transform>().First(t => t.name == name);
                float min = float.PositiveInfinity, max = float.NegativeInfinity;
                for (int i = 0; i < 216; i++)
                {
                    animator.Update(1f / 120);
                    min = Mathf.Min(min, foot.position.z);
                    max = Mathf.Max(max, foot.position.z);
                    Assert.That(foot.position.y, Is.InRange(.04f, .15f), name + " must stay near the ground.");
                }
                Assert.Greater(max - min, .3f, name + " must step even with the weapon layers active.");
            }
            Assert.Less(skeleton.transform.position.magnitude, .001f);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void Death_AfterLocomotionOrAttack_LeavesEntireMeshOnGround(bool attack)
        {
            animator.SetBool("Walk", true);
            Step(2);
            if (attack) { animator.SetTrigger("Overhead"); Step(1.7f); }
            animator.SetTrigger("Die");
            Step(3);
            var renderer = skeleton.GetComponentInChildren<SkinnedMeshRenderer>();
            var mesh = new Mesh();
            try
            {
                renderer.BakeMesh(mesh);
                var vertices = mesh.vertices.Select(renderer.transform.TransformPoint).ToArray();
                Assert.GreaterOrEqual(vertices.Min(v => v.y), -.015f, "No bones should sink below the floor.");
                Assert.Less(vertices.Max(v => v.y), .25f, "Hands, head and all bone pieces must reach the pile.");
            }
            finally { Object.DestroyImmediate(mesh); }
            Assert.IsTrue(animator.GetCurrentAnimatorStateInfo(0).IsName("Die"));
        }
    }
}
