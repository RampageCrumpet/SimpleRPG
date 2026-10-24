using System.Collections;
using HitDetection;
using NUnit.Framework;
using SimpleRPG;
using UnityEngine;
using UnityEngine.TestTools;

namespace SimpleRPG.Tests.PlayMode
{
    /// <summary>
    /// Checks the physics path that the damage tests skip: a real hitbox overlapping a real HurtBox
    /// should fire <see cref="Hitbox.HitboxCollisionEvent"/> once, and one on a non-HurtBox layer should not.
    /// </summary>
    public class HitboxTriggerPlayModeTests
    {
        private GameObject hitboxObject;
        private GameObject hurtBoxObject;
        private int hitCount;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            hitCount = 0;

            hitboxObject = new GameObject("TestHitbox");
            hitboxObject.AddComponent<BoxCollider>().isTrigger = true;
            Hitbox hitbox = hitboxObject.AddComponent<Hitbox>();
            hitbox.HitboxCollisionEvent.AddListener((character, location) => hitCount++);

            hurtBoxObject = new GameObject("TestHurtBox");
            hurtBoxObject.AddComponent<BoxCollider>().isTrigger = true;
            hurtBoxObject.AddComponent<Character>();
            hurtBoxObject.AddComponent<HurtBox>();
            hurtBoxObject.transform.position = hitboxObject.transform.position;

            // Let Start() run on the hitbox before any physics step.
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (hitboxObject != null) Object.Destroy(hitboxObject);
            if (hurtBoxObject != null) Object.Destroy(hurtBoxObject);
            yield return null;
        }

        /// <summary>
        /// Unity only sends trigger messages when one side has a Rigidbody, so the hitbox carries a kinematic one, as the sword prefabs do.
        /// </summary>
        [UnityTest]
        public IEnumerator Hitbox_OverlappingAHurtBox_RaisesTheHitEvent()
        {
            Rigidbody body = hitboxObject.AddComponent<Rigidbody>();
            body.isKinematic = true;
            hurtBoxObject.layer = LayerMask.NameToLayer("HurtBox");

            // Trigger messages are sent during the physics step, so wait for a couple of fixed updates.
            yield return new WaitForFixedUpdate();
            yield return new WaitForFixedUpdate();

            Assert.AreEqual(1, hitCount, "A melee hitbox overlapping a HurtBox should register exactly one hit.");
        }

        [UnityTest]
        public IEnumerator Hitbox_OverlappingANonHurtBoxLayer_DoesNotRaiseTheHitEvent()
        {
            hurtBoxObject.layer = LayerMask.NameToLayer("Default");

            yield return new WaitForFixedUpdate();
            yield return new WaitForFixedUpdate();

            Assert.AreEqual(0, hitCount, "Only HurtBox layers should register hits.");
        }
    }
}
