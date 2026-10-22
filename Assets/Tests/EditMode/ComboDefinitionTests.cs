using NUnit.Framework;
using SimpleRPG.Abilities;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace SimpleRPG.Tests.EditMode
{
    /// <summary>
    /// Checks combo data validation and the chain window query. Built from code, so no assets are needed.
    /// A valid two-step combo: step 1 lasts 1 s with its hit at 0.4-0.6 s and its chain window at 0.7-0.9 s; step 2 lasts 0.8 s with its hit at 0.3-0.5 s.
    /// </summary>
    public class ComboDefinitionTests
    {
        private readonly List<Object> created = new List<Object>();

        [TearDown]
        public void TearDown()
        {
            foreach (Object item in created)
            {
                Object.DestroyImmediate(item);
            }

            created.Clear();
        }

        /// <summary>
        /// Makes a clip whose length is the given number of seconds.
        /// </summary>
        private AnimationClip Clip(float length)
        {
            AnimationClip clip = new AnimationClip { name = "TestClip" };
            clip.SetCurve(string.Empty, typeof(Transform), "localPosition.x", AnimationCurve.Linear(0f, 0f, length, 1f));
            created.Add(clip);
            return clip;
        }

        private ComboStep FirstStep(float duration = 1f, float hitStart = 0.4f, float hitEnd = 0.6f, float chainOpen = 0.7f, float chainClose = 0.9f)
        {
            return new ComboStep("A1", Clip(duration), duration, hitStart, hitEnd, chainOpen, chainClose);
        }

        private ComboStep LastStep(float duration = 0.8f, float hitStart = 0.3f, float hitEnd = 0.5f)
        {
            return new ComboStep("A2", Clip(duration), duration, hitStart, hitEnd);
        }

        private ComboDefinition Combo(params ComboStep[] steps)
        {
            ComboDefinition combo = ComboDefinition.Create(steps);
            created.Add(combo);
            return combo;
        }

        private static void AssertProblem(ComboDefinition combo, string fragment)
        {
            List<string> problems = combo.Validate();
            Assert.IsTrue(problems.Any(p => p.Contains(fragment)), $"Expected a problem containing '{fragment}' but got: {string.Join(" | ", problems)}");
        }

        [Test]
        public void ValidCombo_HasNoProblems()
        {
            Assert.IsEmpty(Combo(FirstStep(), LastStep()).Validate());
        }

        [Test]
        public void SingleStepCombo_IsValid()
        {
            Assert.IsEmpty(Combo(LastStep()).Validate());
        }

        [Test]
        public void ComboWithNoSteps_IsReported()
        {
            AssertProblem(Combo(), "no steps");
        }

        [Test]
        public void NullStep_IsReported()
        {
            AssertProblem(Combo(FirstStep(), null), "Step 2: is empty");
        }

        [Test]
        public void Steps_ArePreservedInOrder()
        {
            ComboStep first = FirstStep();
            ComboStep last = LastStep();
            ComboDefinition combo = Combo(first, last);

            Assert.AreEqual(2, combo.StepCount);
            Assert.AreSame(first, combo.Steps[0]);
            Assert.AreSame(last, combo.Steps[1]);
        }

        [Test]
        public void MissingState_IsReported()
        {
            ComboStep step = new ComboStep(" ", Clip(0.8f), 0.8f, 0.3f, 0.5f);
            AssertProblem(Combo(step), "no animator state");
        }

        [Test]
        public void MissingClip_IsReported()
        {
            ComboStep step = new ComboStep("A2", null, 0.8f, 0.3f, 0.5f);
            AssertProblem(Combo(step), "no clip");
        }

        [Test]
        public void DurationThatDiffersFromClip_IsReported()
        {
            ComboStep step = new ComboStep("A2", Clip(0.5f), 0.8f, 0.3f, 0.5f);
            AssertProblem(Combo(step), "does not match clip");
        }

        [Test]
        public void DurationWithinOneFrameOfClip_IsAccepted()
        {
            ComboStep step = new ComboStep("A2", Clip(0.8f - ComboDefinition.ClipLengthTolerance * 0.5f), 0.8f, 0.3f, 0.5f);
            Assert.IsEmpty(Combo(step).Validate());
        }

        [Test]
        public void ZeroDuration_IsReported()
        {
            ComboStep step = new ComboStep("A2", Clip(0.001f), 0f, 0f, 0f);
            AssertProblem(Combo(step), "duration must be greater than zero");
        }

        [Test]
        public void HitWindowEndingBeforeItStarts_IsReported()
        {
            AssertProblem(Combo(LastStep(hitStart: 0.5f, hitEnd: 0.3f)), "hit window must end after it starts");
        }

        [Test]
        public void EmptyHitWindow_IsReported()
        {
            AssertProblem(Combo(LastStep(hitStart: 0.4f, hitEnd: 0.4f)), "hit window must end after it starts");
        }

        [Test]
        public void HitWindowPastTheEndOfTheStep_IsReported()
        {
            AssertProblem(Combo(LastStep(duration: 0.8f, hitStart: 0.3f, hitEnd: 0.9f)), "hit window ends after the step");
        }

        [Test]
        public void HitWindowEndingExactlyWhenTheStepEnds_IsAccepted()
        {
            Assert.IsEmpty(Combo(LastStep(duration: 0.8f, hitStart: 0.3f, hitEnd: 0.8f)).Validate());
        }

        [Test]
        public void NonFinalStepWithoutChainWindow_IsReported()
        {
            AssertProblem(Combo(FirstStep(chainOpen: 0f, chainClose: 0f), LastStep()), "needs a chain window");
        }

        [Test]
        public void ChainWindowOpeningBeforeTheHitEnds_IsReported()
        {
            AssertProblem(Combo(FirstStep(hitEnd: 0.6f, chainOpen: 0.5f, chainClose: 0.9f), LastStep()), "opens before the hit window ends");
        }

        [Test]
        public void ChainWindowOpeningWhenTheHitEnds_IsAccepted()
        {
            Assert.IsEmpty(Combo(FirstStep(hitEnd: 0.6f, chainOpen: 0.6f, chainClose: 0.9f), LastStep()).Validate());
        }

        [Test]
        public void ChainWindowClosingAfterTheStepEnds_IsReported()
        {
            AssertProblem(Combo(FirstStep(duration: 1f, chainOpen: 0.7f, chainClose: 1.2f), LastStep()), "closes after the step ends");
        }

        [Test]
        public void ChainWindowOnTheLastStep_IsReported()
        {
            ComboStep last = new ComboStep("A2", Clip(0.8f), 0.8f, 0.3f, 0.5f, chainOpen: 0.6f, chainClose: 0.7f);
            AssertProblem(Combo(last), "last step");
        }

        [Test]
        public void StartOffsetSkippingTheWholeStep_IsReported()
        {
            ComboStep step = new ComboStep("A2", Clip(0.8f), 0.8f, 0.3f, 0.5f, startOffset: 0.8f);
            AssertProblem(Combo(step), "start offset");
        }

        [Test]
        public void NegativeDamageScalar_IsReported()
        {
            ComboStep step = new ComboStep("A2", Clip(0.8f), 0.8f, 0.3f, 0.5f, damageScalar: -1f);
            AssertProblem(Combo(step), "damage scalar");
        }

        [Test]
        public void Problems_NameTheStepTheyBelongTo()
        {
            ComboStep badSecond = new ComboStep("A2", null, 0.8f, 0.3f, 0.5f);
            List<string> problems = Combo(FirstStep(), badSecond).Validate();

            Assert.IsTrue(problems.All(p => p.StartsWith("Step 2:")), string.Join(" | ", problems));
        }

        [Test]
        public void ChainWindow_IncludesItsOpeningAndExcludesItsClosing()
        {
            ComboStep step = FirstStep(chainOpen: 0.7f, chainClose: 0.9f);

            Assert.IsFalse(step.IsInChainWindow(0.69f), "before the window");
            Assert.IsTrue(step.IsInChainWindow(0.7f), "exactly at opening");
            Assert.IsTrue(step.IsInChainWindow(0.85f), "inside the window");
            Assert.IsFalse(step.IsInChainWindow(0.9f), "exactly at closing");
            Assert.IsFalse(step.IsInChainWindow(1.5f), "after the window");
        }

        [Test]
        public void StepWithoutChainWindow_NeverChains()
        {
            ComboStep step = LastStep();

            Assert.IsFalse(step.HasChainWindow);
            Assert.IsFalse(step.IsInChainWindow(0f));
            Assert.IsFalse(step.IsInChainWindow(0.4f));
        }
    }
}
