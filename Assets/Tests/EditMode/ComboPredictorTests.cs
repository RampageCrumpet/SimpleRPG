using NUnit.Framework;
using SimpleRPG.Abilities;
using System.Collections.Generic;

namespace SimpleRPG.Tests.EditMode
{
    /// <summary>
    /// Checks the owning player's guesses about the server: what is predicted, how the server's answer is matched to the guess, and when a guess
    /// is taken back. Three steps with exact binary-fraction times, as in <see cref="ComboMachineTests"/>: step 0 lasts 1 s with its chain window
    /// at 0.625-0.875 s, step 1 lasts 0.75 s with its chain window at 0.5-0.625 s, step 2 lasts 1 s with none. Presses begin at 10 s.
    /// </summary>
    public class ComboPredictorTests
    {
        private const double Start = 10.0;
        private const double Timeout = 1.0;

        private ComboPredictor predictor;
        private List<ComboEvent> events;

        [SetUp]
        public void SetUp()
        {
            ComboMachine machine = new ComboMachine(new[]
            {
                new ComboStepTiming(1f, 0.375f, 0.5f, 0.625f, 0.875f),
                new ComboStepTiming(0.75f, 0.25f, 0.375f, 0.5f, 0.625f),
                new ComboStepTiming(1f, 0.375f, 0.5f, 0f, 0f),
            });
            predictor = new ComboPredictor(machine, Timeout);
            events = new List<ComboEvent>();
        }

        private PredictedPress Press(double time, bool freshAllowed = true)
        {
            return predictor.Press(time, freshAllowed, events);
        }

        [Test]
        public void AFirstPress_PredictsAttackOne()
        {
            PredictedPress press = Press(Start);

            Assert.IsTrue(press.Predicted);
            Assert.AreEqual(1, press.AttackNumber);
            Assert.AreEqual(1, press.PressId);
            Assert.IsTrue(predictor.HasPendingPrediction);
            Assert.AreEqual(0, predictor.Machine.CurrentStep);
        }

        [Test]
        public void EveryPress_GetsItsOwnNonZeroId_PredictedOrNot()
        {
            PredictedPress first = Press(Start);
            PredictedPress tooEarly = Press(Start + 0.25);
            PredictedPress chained = Press(Start + 0.75);

            Assert.AreEqual(1, first.PressId);
            Assert.AreEqual(2, tooEarly.PressId);
            Assert.AreEqual(3, chained.PressId);
        }

        [Test]
        public void APressTooEarly_PredictsNothingAndChangesNothing()
        {
            Press(Start);

            PredictedPress early = Press(Start + 0.25);

            Assert.IsFalse(early.Predicted);
            Assert.AreEqual(0, early.AttackNumber);
            Assert.AreEqual(0, predictor.Machine.CurrentStep);
        }

        [Test]
        public void AFreshPress_IsNotPredictedWhileTheCooldownShows()
        {
            PredictedPress press = Press(Start, freshAllowed: false);

            Assert.IsFalse(press.Predicted);
            Assert.IsFalse(predictor.HasPendingPrediction);
            Assert.IsFalse(predictor.Machine.IsRunning);
        }

        [Test]
        public void AChainPress_IsPredictedEvenWhileTheCooldownShows()
        {
            Press(Start);

            PredictedPress chained = Press(Start + 0.75, freshAllowed: false);

            Assert.AreEqual(2, chained.AttackNumber);
        }

        [Test]
        public void TheServerStartingTheGuessedAttack_ConfirmsIt()
        {
            PredictedPress press = Press(Start);

            PredictionVerdict verdict = predictor.Confirm(press.PressId, 1, 0.0625, Start + 0.125, events);

            Assert.AreEqual(PredictionVerdict.Confirmed, verdict);
            Assert.IsFalse(predictor.HasPendingPrediction);
            Assert.AreEqual(Start, predictor.Machine.CurrentStepStart, "A confirmed guess leaves the machine as it was.");
        }

        [Test]
        public void AnAnswerToALaterPress_ConfirmsTheEarlierGuessesToo()
        {
            PredictedPress first = Press(Start);
            PredictedPress second = Press(Start + 0.75);

            predictor.Confirm(second.PressId, 2, 0, Start + 0.875, events);

            Assert.IsFalse(predictor.HasPendingPrediction);
            Assert.AreEqual(first.PressId + 1, second.PressId);
        }

        [Test]
        public void TheServerStartingADifferentAttack_ReplaysAndFollowsTheServer()
        {
            PredictedPress press = Press(Start);

            PredictionVerdict verdict = predictor.Confirm(press.PressId, 2, 0.25, Start + 0.5, events);

            Assert.AreEqual(PredictionVerdict.Replay, verdict);
            Assert.IsFalse(predictor.HasPendingPrediction);
            Assert.AreEqual(1, predictor.Machine.CurrentStep);
            Assert.AreEqual(Start + 0.25, predictor.Machine.CurrentStepStart, "The step started 0.25 s ago by the server's account.");
        }

        [Test]
        public void AnAttackTheClientDidNotPredict_IsReplayedAndFollowed()
        {
            // The client judged the press just outside a window and predicted nothing, but the server accepted it.
            Press(Start);
            PredictedPress early = Press(Start + 0.5);
            Assert.IsFalse(early.Predicted);

            PredictionVerdict verdict = predictor.Confirm(early.PressId, 2, 0, Start + 0.5625, events);

            Assert.AreEqual(PredictionVerdict.Replay, verdict);
            Assert.AreEqual(1, predictor.Machine.CurrentStep);
        }

        [Test]
        public void AnAttackTheServerStartedOnItsOwn_IsFollowed()
        {
            PredictionVerdict verdict = predictor.Confirm(0, 1, 0, Start, events);

            Assert.AreEqual(PredictionVerdict.Replay, verdict);
            Assert.AreEqual(0, predictor.Machine.CurrentStep);
        }

        [Test]
        public void ARejectedPress_TakesBackTheGuess()
        {
            PredictedPress press = Press(Start);

            PredictionRollback takeBack = predictor.Reject(press.PressId, Start + 0.125, events);

            Assert.IsTrue(takeBack.Undone);
            Assert.AreEqual(0, takeBack.ResumeAttack, "Nothing was running before it, so the client falls back to rest.");
            Assert.IsFalse(predictor.HasPendingPrediction);
            Assert.IsFalse(predictor.Machine.IsRunning);
        }

        [Test]
        public void ARejectedChainPress_ResumesTheSwingItTriedToCutShort()
        {
            PredictedPress first = Press(Start);
            predictor.Confirm(first.PressId, 1, 0, Start + 0.0625, events);
            PredictedPress chained = Press(Start + 0.75);

            PredictionRollback takeBack = predictor.Reject(chained.PressId, Start + 0.8125, events);

            Assert.IsTrue(takeBack.Undone);
            Assert.AreEqual(1, takeBack.ResumeAttack);
            Assert.AreEqual(0.8125f, takeBack.SecondsIntoResume, 1e-4f);
            Assert.AreEqual(0, predictor.Machine.CurrentStep, "The server never stopped step one, so the client's machine goes back to it.");
            Assert.AreEqual(Start, predictor.Machine.CurrentStepStart);
        }

        [Test]
        public void ARejectedChainPress_AfterTheEarlierSwingRanOut_ResumesNothing()
        {
            PredictedPress first = Press(Start);
            predictor.Confirm(first.PressId, 1, 0, Start + 0.0625, events);
            PredictedPress chained = Press(Start + 0.75);

            PredictionRollback takeBack = predictor.Reject(chained.PressId, Start + 1.25, events);

            Assert.IsTrue(takeBack.Undone);
            Assert.AreEqual(0, takeBack.ResumeAttack);
            Assert.IsFalse(predictor.Machine.IsRunning);
        }

        [Test]
        public void ARejectedPress_KeepsEarlierPendingGuesses()
        {
            Press(Start);
            PredictedPress chained = Press(Start + 0.75);

            predictor.Reject(chained.PressId, Start + 0.8125, events);

            Assert.IsTrue(predictor.HasPendingPrediction, "The first guess is still waiting on the server.");
            Assert.AreEqual(0, predictor.Machine.CurrentStep);
        }

        [Test]
        public void ARejectionOfAPressThatPredictedNothing_ChangesNothing()
        {
            Press(Start);
            PredictedPress early = Press(Start + 0.25);

            PredictionRollback takeBack = predictor.Reject(early.PressId, Start + 0.3125, events);

            Assert.IsFalse(takeBack.Undone);
            Assert.IsTrue(predictor.Machine.IsRunning, "The attack that was predicted and not rejected keeps going.");
        }

        [Test]
        public void AfterARollback_TheNextPressPredictsAFreshChain()
        {
            PredictedPress press = Press(Start);
            predictor.Reject(press.PressId, Start + 0.125, events);

            PredictedPress again = Press(Start + 0.25);

            Assert.AreEqual(1, again.AttackNumber);
        }

        [Test]
        public void AGuessTheServerNeverAnswers_IsGivenUpAfterTheTimeout()
        {
            Press(Start);

            Assert.IsFalse(predictor.Expire(Start + Timeout, events).Undone, "Still within the timeout.");
            Assert.IsTrue(predictor.Expire(Start + Timeout + 0.0625, events).Undone);
            Assert.IsFalse(predictor.HasPendingPrediction);
            Assert.IsFalse(predictor.Machine.IsRunning);
        }

        [Test]
        public void Expire_WithNothingPending_DoesNothing()
        {
            Assert.IsFalse(predictor.Expire(Start + 100, events).Undone);
        }

        [Test]
        public void AnAnsweredGuess_NeverExpires()
        {
            PredictedPress press = Press(Start);
            predictor.Confirm(press.PressId, 1, 0, Start + 0.125, events);

            Assert.IsFalse(predictor.Expire(Start + 100, events).Undone);
        }

        [Test]
        public void Reset_ForgetsTheComboAndItsGuesses()
        {
            Press(Start);

            predictor.Reset(Start + 0.25, events);

            Assert.IsFalse(predictor.HasPendingPrediction);
            Assert.IsFalse(predictor.Machine.IsRunning);
        }

        [Test]
        public void ThePredictorsMachine_EndsStepsAsTimePasses()
        {
            Press(Start);

            predictor.Advance(Start + 2, events);

            Assert.IsFalse(predictor.Machine.IsRunning);
            Assert.AreEqual(1, Press(Start + 2).AttackNumber, "After the chain ran out a press starts a fresh one.");
        }
    }
}
