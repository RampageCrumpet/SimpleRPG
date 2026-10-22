using NUnit.Framework;
using SimpleRPG.Abilities;

namespace SimpleRPG.Tests.EditMode
{
    /// <summary>
    /// Checks the server's longsword combo timing: which presses queue the next attack, which are rejected, and when a fresh chain may start.
    /// Uses the same clip and combo lengths as the player's melee behaviour. The chain starts at time 10 s.
    /// </summary>
    public class MeleeComboTrackerTests
    {
        private const float Start = 10f;
        private const float Attack1Length = 70f / 60f;
        private const float Attack2Length = 55f / 60f;
        private const float Attack3Length = 59f / 60f;
        private const float Combo1Length = 17f / 60f;
        private const float Combo2Length = 3f / 60f;
        private const float Delta = 1e-3f;

        private MeleeComboTracker NewTracker()
        {
            return new MeleeComboTracker(
                new[] { Attack1Length, Attack2Length, Attack3Length },
                new[] { Combo1Length, Combo2Length, 0f });
        }

        [Test]
        public void StartAttack_SetsAttackOne()
        {
            MeleeComboTracker tracker = NewTracker();
            Assert.IsTrue(tracker.CanStartAttack(Start));

            tracker.StartAttack(Start);

            Assert.AreEqual(1, tracker.CurrentAttack);
        }

        [Test]
        public void QueueBeforeAnyChain_IsRejected()
        {
            MeleeComboTracker tracker = NewTracker();

            Assert.IsFalse(tracker.TryQueueNext(Start, out int next));
            Assert.AreEqual(0, next);
        }

        [Test]
        public void QueueDuringAttackOne_StartsAttackTwo()
        {
            MeleeComboTracker tracker = NewTracker();
            tracker.StartAttack(Start);

            Assert.IsTrue(tracker.TryQueueNext(Start + 0.6f, out int next));

            Assert.AreEqual(2, next);
            Assert.AreEqual(2, tracker.CurrentAttack);
        }

        [Test]
        public void QueueInComboGap_IsRejected()
        {
            // Attack 1 ends at 1.17 s and attack 2 starts at 1.45 s, so 1.3 s is in the combo continuation.
            MeleeComboTracker tracker = NewTracker();
            tracker.StartAttack(Start);
            Assert.IsTrue(tracker.TryQueueNext(Start + 0.6f, out _));

            Assert.IsFalse(tracker.TryQueueNext(Start + 1.3f, out int next));
            Assert.AreEqual(0, next);
        }

        [Test]
        public void HeldPressesInTheSameWindow_OnlyTheFirstIsAccepted()
        {
            MeleeComboTracker tracker = NewTracker();
            tracker.StartAttack(Start);

            Assert.IsTrue(tracker.TryQueueNext(Start + 0.6f, out _));
            Assert.IsFalse(tracker.TryQueueNext(Start + 0.7f, out _), "A second press in attack 1's window must not queue again.");
            Assert.IsFalse(tracker.TryQueueNext(Start + 0.8f, out _), "Presses while attack 2 is pending must not skip ahead.");
        }

        [Test]
        public void QueueDuringAttackTwo_StartsAttackThree()
        {
            MeleeComboTracker tracker = NewTracker();
            tracker.StartAttack(Start);
            Assert.IsTrue(tracker.TryQueueNext(Start + 0.6f, out _));

            // Attack 2 starts at 1.45 s and runs to 2.37 s.
            Assert.IsTrue(tracker.TryQueueNext(Start + 1.9f, out int next));

            Assert.AreEqual(3, next);
        }

        [Test]
        public void QueueDuringAttackThree_IsRejected()
        {
            MeleeComboTracker tracker = NewTracker();
            tracker.StartAttack(Start);
            Assert.IsTrue(tracker.TryQueueNext(Start + 0.6f, out _));
            Assert.IsTrue(tracker.TryQueueNext(Start + 1.9f, out _));

            Assert.IsFalse(tracker.TryQueueNext(Start + 2.8f, out int next), "Attack 3 is the end of the chain.");
            Assert.AreEqual(0, next);
        }

        [Test]
        public void QueueJustAfterAttackEnd_IsAcceptedWithinTolerance()
        {
            MeleeComboTracker tracker = NewTracker();
            tracker.StartAttack(Start);

            // Attack 1 ends at 1.1667 s. A press 0.05 s after that is still within the network allowance.
            Assert.IsTrue(tracker.TryQueueNext(Start + Attack1Length + 0.05f, out int next));
            Assert.AreEqual(2, next);
        }

        [Test]
        public void PressAfterAttackEnd_ContinuesWithinTheRecoveryWindow()
        {
            var tracker = NewTracker();
            tracker.StartAttack(Start);
            float press = Start + Attack1Length + .2f;
            Assert.IsTrue(tracker.TryQueueNext(press, out int next));
            Assert.AreEqual(2, next);

            // Attack two starts after the late press and its continuation, not at the old buffered time.
            float secondEnd = press + Combo1Length + Attack2Length;
            Assert.IsTrue(tracker.TryQueueNext(secondEnd + .2f, out next));
            Assert.AreEqual(3, next);
        }

        [Test]
        public void PressAfterRecoveryWindow_ExpiresInsteadOfContinuing()
        {
            var tracker = NewTracker();
            tracker.StartAttack(Start);
            Assert.IsFalse(tracker.TryQueueNext(Start + Attack1Length
                + MeleeComboTracker.DefaultContinuationWindow + MeleeComboTracker.Tolerance + Delta, out _));
        }

        [Test]
        public void LatePress_CannotSkipThePendingContinuation()
        {
            var tracker = NewTracker();
            tracker.StartAttack(Start);
            float press = Start + Attack1Length + .2f;
            Assert.IsTrue(tracker.TryQueueNext(press, out _));
            Assert.IsFalse(tracker.TryQueueNext(press + .05f, out _));
        }

        [Test]
        public void FreshChain_IsBlockedWhileAQueuedChainRuns()
        {
            MeleeComboTracker tracker = NewTracker();
            tracker.StartAttack(Start);
            Assert.IsTrue(tracker.TryQueueNext(Start + 0.6f, out _));
            Assert.IsTrue(tracker.TryQueueNext(Start + 1.9f, out _));

            // Attack 3 runs from 2.42 s to 3.40 s after the chain start, so a fresh chain must wait for it to finish.
            Assert.IsFalse(tracker.CanStartAttack(Start + 2.8f));
            Assert.IsFalse(tracker.CanStartAttack(Start + 3.2f));
            Assert.IsTrue(tracker.CanStartAttack(Start + 3.5f));
        }

        [Test]
        public void UnqueuedAttack_DoesNotBlockAFreshChain()
        {
            // Only the cooldown limits attacks that were never queued.
            MeleeComboTracker tracker = NewTracker();
            tracker.StartAttack(Start);

            Assert.IsTrue(tracker.CanStartAttack(Start + 0.5f));
        }

        [Test]
        public void FreshChain_ResetsAttackNumberAfterAQueuedChain()
        {
            MeleeComboTracker tracker = NewTracker();
            tracker.StartAttack(Start);
            Assert.IsTrue(tracker.TryQueueNext(Start + 0.6f, out _));
            Assert.IsTrue(tracker.TryQueueNext(Start + 1.9f, out _));

            float later = Start + 4f;
            Assert.IsTrue(tracker.CanStartAttack(later));
            tracker.StartAttack(later);

            Assert.AreEqual(1, tracker.CurrentAttack);
            Assert.IsTrue(tracker.TryQueueNext(later + 0.6f, out int next), "The new chain's attack 1 should accept a queue.");
            Assert.AreEqual(2, next);
        }
    }
}
