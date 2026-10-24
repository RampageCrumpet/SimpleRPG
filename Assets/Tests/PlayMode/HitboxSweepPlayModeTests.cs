using System.Collections;
using HitDetection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace SimpleRPG.Tests.PlayMode
{
    /// <summary>
    /// Checks that a blade moving further in one physics step than a target is thick cannot skip over the target, and that the hit is still
    /// reported once. The blade carries a kinematic body like the sword prefabs and is moved by its transform, as an animator moves it.
    /// </summary>
    public class HitboxSweepPlayModeTests
    {
        private GameObject blade;
        private GameObject target;
        private int hitCount;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            hitCount = 0;

            blade = new GameObject("Blade");
            blade.transform.position = new Vector3(-2f, 0f, 0f);
            BoxCollider bladeShape = blade.AddComponent<BoxCollider>();
            bladeShape.isTrigger = true;
            bladeShape.size = new Vector3(0.05f, 1f, 0.05f);
            Rigidbody body = blade.AddComponent<Rigidbody>();
            body.isKinematic = true;
            blade.AddComponent<Hitbox>().HitboxCollisionEvent.AddListener((character, location) => hitCount++);

            target = new GameObject("Target");
            target.layer = LayerMask.NameToLayer("HurtBox");
            target.AddComponent<BoxCollider>().size = new Vector3(0.1f, 1f, 0.1f);
            target.AddComponent<HurtBox>();

            // Let Start() run on the hitbox before any physics step.
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (blade != null) Object.Destroy(blade);
            if (target != null) Object.Destroy(target);
            yield return null;
        }

        [UnityTest]
        public IEnumerator Blade_MovingFarEnoughToStepOverATargetStillHitsIt()
        {
            yield return new WaitForFixedUpdate();
            blade.transform.position = new Vector3(2f, 0f, 0f);
            yield return new WaitForFixedUpdate();
            yield return new WaitForFixedUpdate();

            Assert.AreEqual(1, hitCount, "The blade crossed the target between two steps, so it should hit exactly once.");
        }

        [UnityTest]
        public IEnumerator Blade_SweptRotationAlsoHits()
        {
            // A long blade whose tip swings through the target while its pivot barely moves: a rotation sweep, as a torso turn makes.
            blade.transform.position = Vector3.zero;
            blade.GetComponent<BoxCollider>().size = new Vector3(0.05f, 0.05f, 1.2f);
            blade.GetComponent<BoxCollider>().center = new Vector3(0f, 0f, 0.6f);
            blade.transform.rotation = Quaternion.Euler(0f, -60f, 0f);
            target.transform.position = new Vector3(0f, 0f, 0.8f);
            yield return new WaitForFixedUpdate();

            blade.transform.rotation = Quaternion.Euler(0f, 60f, 0f);
            yield return new WaitForFixedUpdate();
            yield return new WaitForFixedUpdate();

            Assert.AreEqual(1, hitCount);
        }

        [UnityTest]
        public IEnumerator Blade_PassingBesideTheTargetDoesNotHit()
        {
            target.transform.position = new Vector3(0f, 0f, 3f);

            yield return new WaitForFixedUpdate();
            blade.transform.position = new Vector3(2f, 0f, 0f);
            yield return new WaitForFixedUpdate();
            yield return new WaitForFixedUpdate();

            Assert.AreEqual(0, hitCount);
        }

        [UnityTest]
        public IEnumerator Blade_WithItsWindowClosedDoesNotSweep()
        {
            blade.GetComponent<Hitbox>().enabled = false;

            yield return new WaitForFixedUpdate();
            blade.transform.position = new Vector3(2f, 0f, 0f);
            yield return new WaitForFixedUpdate();
            yield return new WaitForFixedUpdate();

            Assert.AreEqual(0, hitCount);
        }

        [UnityTest]
        public IEnumerator Blade_ReopeningItsWindowForgetsTheOldPath()
        {
            Hitbox hitbox = blade.GetComponent<Hitbox>();
            hitbox.enabled = false;
            yield return new WaitForFixedUpdate();
            blade.transform.position = new Vector3(2f, 0f, 0f);
            yield return new WaitForFixedUpdate();

            // The window opens with the blade already past the target: the move before it opened must not count.
            hitbox.enabled = true;
            yield return new WaitForFixedUpdate();
            yield return new WaitForFixedUpdate();

            Assert.AreEqual(0, hitCount);
        }
    }
}
