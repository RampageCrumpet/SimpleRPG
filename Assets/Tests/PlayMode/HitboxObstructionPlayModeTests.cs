using System.Collections;
using NUnit.Framework;
using SimpleRPG.HitDetection;
using UnityEngine;
using UnityEngine.TestTools;

namespace SimpleRPG.Tests.PlayMode
{
    /// <summary>
    /// A blade cannot reach through a wall: a target on the far side is not hit, and the blade reports where it met the wall instead.
    /// </summary>
    public class HitboxObstructionPlayModeTests
    {
        private GameObject hitboxObject;
        private GameObject hurtBoxObject;
        private GameObject wallObject;
        private Hitbox hitbox;
        private int hitCount;
        private int bounceCount;
        private Vector3 bouncePoint;
        private Vector3 bounceNormal;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            hitCount = 0;
            bounceCount = 0;

            // The wielder stands at the origin; the blade is two metres ahead of them, with a target in it.
            hitboxObject = new GameObject("TestBlade");
            hitboxObject.transform.position = new Vector3(0f, 1f, 2f);
            hitboxObject.AddComponent<BoxCollider>().isTrigger = true;
            Rigidbody body = hitboxObject.AddComponent<Rigidbody>();
            body.isKinematic = true;
            hitbox = hitboxObject.AddComponent<Hitbox>();
            hitbox.HitboxCollisionEvent.AddListener((character, location) => hitCount++);
            hitbox.Obstructed += (point, normal) =>
            {
                bounceCount++;
                bouncePoint = point;
                bounceNormal = normal;
            };
            hitbox.ObstructionOrigin = () => new Vector3(0f, 1f, 0f);

            hurtBoxObject = new GameObject("TestHurtBox");
            hurtBoxObject.layer = LayerMask.NameToLayer("HurtBox");
            hurtBoxObject.AddComponent<BoxCollider>().isTrigger = true;
            hurtBoxObject.AddComponent<Character>();
            hurtBoxObject.AddComponent<HurtBox>();
            hurtBoxObject.transform.position = hitboxObject.transform.position;

            // The blade stays off until a test has built its scene (see Settle); a physics step in between would hit the target before any wall exists.
            hitbox.enabled = false;

            yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (hitboxObject != null) Object.Destroy(hitboxObject);
            if (hurtBoxObject != null) Object.Destroy(hurtBoxObject);
            if (wallObject != null) Object.Destroy(wallObject);
            yield return null;
        }

        private void RaiseWall(string layerName)
        {
            wallObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wallObject.name = "TestWall";
            wallObject.layer = LayerMask.NameToLayer(layerName);
            wallObject.transform.position = new Vector3(0f, 1f, 1f);
            wallObject.transform.localScale = new Vector3(6f, 4f, 0.2f);
        }

        private IEnumerator Settle()
        {
            hitbox.enabled = true;
            yield return new WaitForFixedUpdate();
            yield return new WaitForFixedUpdate();
            yield return new WaitForFixedUpdate();
        }

        [UnityTest]
        public IEnumerator ATargetBehindAWall_IsNotHit_AndTheBladeReportsTheWall()
        {
            RaiseWall("World");

            yield return Settle();

            Assert.AreEqual(0, hitCount, "The wall is between the wielder and the target.");
            Assert.AreEqual(1, bounceCount, "The bounce is reported exactly once.");
            Assert.AreEqual(1f, bouncePoint.z, 0.15f, "At the face of the wall nearest the wielder.");
            Assert.Less(bounceNormal.z, -0.5f, "The wall faces back toward the wielder.");
        }

        [UnityTest]
        public IEnumerator WithNoWall_TheTargetIsHit_AndNothingBounces()
        {
            yield return Settle();

            Assert.AreEqual(1, hitCount);
            Assert.AreEqual(0, bounceCount);
        }

        [UnityTest]
        public IEnumerator AWallOnAnotherLayer_IsNotAnObstruction()
        {
            RaiseWall("Default");

            yield return Settle();

            Assert.AreEqual(1, hitCount, "Only the configured world layer stops a blade.");
            Assert.AreEqual(0, bounceCount);
        }

        [UnityTest]
        public IEnumerator WithNoWielder_TheWallCheckIsOff()
        {
            RaiseWall("World");
            hitbox.ObstructionOrigin = () => null;

            yield return Settle();

            Assert.AreEqual(1, hitCount);
            Assert.AreEqual(0, bounceCount);
        }

        [UnityTest]
        public IEnumerator ASwitchedOffAndOnWindowChecksAgain()
        {
            RaiseWall("World");
            yield return Settle();
            Assert.AreEqual(1, bounceCount);

            hitbox.enabled = false;
            hitbox.enabled = true;
            yield return Settle();

            Assert.AreEqual(2, bounceCount, "A new window reports its own bounce.");
            Assert.AreEqual(0, hitCount);
        }
    }
}
