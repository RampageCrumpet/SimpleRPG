using NUnit.Framework;
using UnityEngine;

namespace SimpleRPG.Tests.EditMode
{
    public class SwingHitTrackerTests
    {
        private GameObject attacker;
        private GameObject target;
        private SwingHitTracker tracker;

        [SetUp]
        public void SetUp()
        {
            attacker = new GameObject("Attacker");
            target = new GameObject("Target");
            tracker = new SwingHitTracker();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(attacker);
            Object.DestroyImmediate(target);
        }

        [Test]
        public void FirstHitOnATarget_IsAccepted()
        {
            tracker.Begin();
            Assert.IsTrue(tracker.TryRegisterHit(target, attacker));
        }

        [Test]
        public void SecondHitOnTheSameTarget_InOneSwing_IsRejected()
        {
            tracker.Begin();
            tracker.TryRegisterHit(target, attacker);
            Assert.IsFalse(tracker.TryRegisterHit(target, attacker));
        }

        [Test]
        public void BeginDuringASwing_KeepsTheRecord()
        {
            tracker.Begin();
            tracker.TryRegisterHit(target, attacker);
            tracker.Begin();
            Assert.IsFalse(tracker.TryRegisterHit(target, attacker));
        }

        [Test]
        public void NextSwing_CanHitTheTargetAgain()
        {
            tracker.Begin();
            tracker.TryRegisterHit(target, attacker);
            tracker.End();
            tracker.Begin();
            Assert.IsTrue(tracker.TryRegisterHit(target, attacker));
        }

        [Test]
        public void TheAttacker_IsNeverHit()
        {
            tracker.Begin();
            Assert.IsFalse(tracker.TryRegisterHit(attacker, attacker));
        }

        [Test]
        public void NullTarget_IsRejected()
        {
            tracker.Begin();
            Assert.IsFalse(tracker.TryRegisterHit(null, attacker));
        }

        [Test]
        public void WithoutASwing_NothingIsHit()
        {
            Assert.IsFalse(tracker.TryRegisterHit(target, attacker));
            tracker.Begin();
            tracker.End();
            Assert.IsFalse(tracker.TryRegisterHit(target, attacker));
        }
    }
}
