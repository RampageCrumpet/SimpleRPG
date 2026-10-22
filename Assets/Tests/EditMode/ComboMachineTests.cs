using NUnit.Framework;
using SimpleRPG.Abilities;
using System;
using System.Collections.Generic;
using System.Linq;

namespace SimpleRPG.Tests.EditMode
{
    /// <summary>
    /// Checks the combo logic: which presses start or chain a step, which are rejected, and when hit windows and steps report.
    /// Three steps, with times chosen as exact binary fractions so boundary presses are exact. The chain starts at 10 s.
    /// Step 0 lasts 1 s, hit 0.375-0.5, chain window 0.625-0.875. Step 1 lasts 0.75 s, hit 0.25-0.375, chain window 0.5-0.625.
    /// Step 2 lasts 1 s, hit 0.375-0.5, no chain window.
    /// </summary>
    public class ComboMachineTests
    {
        private const double Start = 10.0;

        private ComboMachine machine;
        private List<ComboEvent> events;

        [SetUp]
        public void SetUp()
        {
            machine = new ComboMachine(new[]
            {
                new ComboStepTiming(1f, 0.375f, 0.5f, 0.625f, 0.875f),
                new ComboStepTiming(0.75f, 0.25f, 0.375f, 0.5f, 0.625f),
                new ComboStepTiming(1f, 0.375f, 0.5f, 0f, 0f),
            });
            events = new List<ComboEvent>();
        }

        private static string Describe(ComboEvent e)
        {
            return $"{e.Kind}:{e.Step}@{e.Time:0.####}{(e.Chained ? "*" : string.Empty)}";
        }

        private string[] Events()
        {
            return events.Select(Describe).ToArray();
        }

        private ComboPressResult Press(double time)
        {
            return machine.Press(time, events);
        }

        private void StartChain()
        {
            Press(Start);
            events.Clear();
        }

        [Test]
        public void NewMachine_IsNotRunning()
        {
            Assert.IsFalse(machine.IsRunning);
            Assert.AreEqual(-1, machine.CurrentStep);
            Assert.AreEqual(3, machine.StepCount);
        }

        [Test]
        public void Constructor_RejectsAnEmptyChain()
        {
            Assert.Throws<ArgumentException>(() => new ComboMachine(new ComboStepTiming[0]));
            Assert.Throws<ArgumentException>(() => new ComboMachine(null));
        }

        [Test]
        public void PressWhenIdle_StartsTheFirstStep()
        {
            ComboPressResult result = Press(Start);

            Assert.AreEqual(ComboPressOutcome.Started, result.Outcome);
            Assert.AreEqual(0, result.Step);
            Assert.IsTrue(result.Accepted);
            Assert.IsTrue(machine.IsRunning);
            Assert.AreEqual(Start, machine.CurrentStepStart);
            CollectionAssert.AreEqual(new[] { "StepStarted:0@10" }, Events());
        }

        [Test]
        public void PressBeforeTheChainWindow_IsDiscardedAndNotRemembered()
        {
            StartChain();

            ComboPressResult early = Press(Start + 0.5);
            machine.Advance(Start + 0.75, events);

            Assert.AreEqual(ComboPressOutcome.Rejected, early.Outcome);
            Assert.AreEqual(-1, early.Step);
            Assert.IsFalse(early.Accepted);
            Assert.AreEqual(0, machine.CurrentStep, "an early press must not chain once the window opens");
            CollectionAssert.AreEqual(new[] { "HitOpened:0@10.375", "HitClosed:0@10.5" }, Events());
        }

        [Test]
        public void PressExactlyWhenTheWindowOpens_Chains()
        {
            StartChain();

            ComboPressResult result = Press(Start + 0.625);

            Assert.AreEqual(ComboPressOutcome.Chained, result.Outcome);
            Assert.AreEqual(1, result.Step);
        }

        [Test]
        public void PressJustBeforeTheWindowOpens_IsRejected()
        {
            StartChain();

            Assert.AreEqual(ComboPressOutcome.Rejected, Press(Start + 0.625 - 1e-6).Outcome);
        }

        [Test]
        public void PressJustBeforeTheWindowCloses_Chains()
        {
            StartChain();

            Assert.AreEqual(ComboPressOutcome.Chained, Press(Start + 0.875 - 1e-6).Outcome);
        }

        [Test]
        public void PressExactlyWhenTheWindowCloses_IsRejected()
        {
            StartChain();

            Assert.AreEqual(ComboPressOutcome.Rejected, Press(Start + 0.875).Outcome);
            Assert.AreEqual(0, machine.CurrentStep);
        }

        [Test]
        public void Chain_CutsTheStepShortAndStartsTheNextAtThePressTime()
        {
            StartChain();

            Press(Start + 0.75);

            Assert.AreEqual(1, machine.CurrentStep);
            Assert.AreEqual(Start + 0.75, machine.CurrentStepStart);
            CollectionAssert.AreEqual(
                new[] { "HitOpened:0@10.375", "HitClosed:0@10.5", "StepEnded:0@10.75*", "StepStarted:1@10.75" },
                Events());
        }

        [Test]
        public void SecondPressAtTheSameInstant_IsRejected()
        {
            StartChain();
            Press(Start + 0.75);
            events.Clear();

            ComboPressResult again = Press(Start + 0.75);

            Assert.AreEqual(ComboPressOutcome.Rejected, again.Outcome);
            Assert.AreEqual(1, machine.CurrentStep, "spamming must not skip a step");
            Assert.IsEmpty(events);
        }

        [Test]
        public void HeldButtonSpam_AdvancesOneStepPerWindow()
        {
            StartChain();

            // Press every 1/16 s until just before the last step would run out.
            for (double t = Start; t < Start + 2; t += 0.0625)
            {
                Press(t);
            }

            string[] started = events.Where(e => e.Kind == ComboEventKind.StepStarted).Select(Describe).ToArray();
            CollectionAssert.AreEqual(new[] { "StepStarted:1@10.625", "StepStarted:2@11.125" }, started, "each step starts exactly once, as soon as its window opens");
        }

        [Test]
        public void ChainsThroughEveryStep()
        {
            StartChain();

            Assert.AreEqual(1, Press(Start + 0.75).Step);
            Assert.AreEqual(2, Press(Start + 0.75 + 0.5).Step);
            Assert.AreEqual(2, machine.CurrentStep);
        }

        [Test]
        public void LastStep_RejectsEveryPress()
        {
            StartChain();
            Press(Start + 0.75);
            Press(Start + 1.25);
            events.Clear();

            for (double elapsed = 0; elapsed < 1; elapsed += 0.0625)
            {
                Assert.AreEqual(ComboPressOutcome.Rejected, Press(Start + 1.25 + elapsed).Outcome, "elapsed " + elapsed);
            }

            Assert.AreEqual(2, machine.CurrentStep);
        }

        [Test]
        public void PressAfterTheWindowCloses_IsRejectedAndTheStepRunsOut()
        {
            StartChain();

            Press(Start + 0.9375);
            machine.Advance(Start + 1.25, events);

            Assert.IsFalse(machine.IsRunning);
            CollectionAssert.AreEqual(
                new[] { "HitOpened:0@10.375", "HitClosed:0@10.5", "StepEnded:0@11" },
                Events());
        }

        [Test]
        public void PressAfterTheStepRanOut_StartsAFreshChain()
        {
            StartChain();

            ComboPressResult result = Press(Start + 2);

            Assert.AreEqual(ComboPressOutcome.Started, result.Outcome);
            Assert.AreEqual(0, result.Step);
            Assert.AreEqual(Start + 2, machine.CurrentStepStart);
            CollectionAssert.AreEqual(
                new[] { "HitOpened:0@10.375", "HitClosed:0@10.5", "StepEnded:0@11", "StepStarted:0@12" },
                Events());
        }

        [Test]
        public void PressAfterTheLastStepEnds_StartsAFreshChain()
        {
            StartChain();
            Press(Start + 0.75);
            Press(Start + 1.25);

            ComboPressResult result = Press(Start + 1.25 + 1.5);

            Assert.AreEqual(ComboPressOutcome.Started, result.Outcome);
            Assert.AreEqual(0, result.Step);
        }

        [Test]
        public void Advance_ReportsHitWindowAtItsOwnTimes()
        {
            StartChain();

            machine.Advance(Start + 0.25, events);
            Assert.IsEmpty(events);

            machine.Advance(Start + 0.4375, events);
            CollectionAssert.AreEqual(new[] { "HitOpened:0@10.375" }, Events());

            machine.Advance(Start + 0.5625, events);
            CollectionAssert.AreEqual(new[] { "HitOpened:0@10.375", "HitClosed:0@10.5" }, Events());
        }

        [Test]
        public void Advance_ReportsEachEventOnce()
        {
            StartChain();

            machine.Advance(Start + 0.75, events);
            int count = events.Count;
            machine.Advance(Start + 0.75, events);
            machine.Advance(Start + 0.8125, events);

            Assert.AreEqual(count, events.Count);
        }

        [Test]
        public void Advance_OverAWholeStepInOneCall_ReportsEverythingInOrder()
        {
            StartChain();

            machine.Advance(Start + 5, events);

            CollectionAssert.AreEqual(new[] { "HitOpened:0@10.375", "HitClosed:0@10.5", "StepEnded:0@11" }, Events());
            Assert.IsFalse(machine.IsRunning);
        }

        [Test]
        public void Advance_ReportsTheSameEventsWhateverTheFrameRate()
        {
            ComboMachine coarse = new ComboMachine(new[]
            {
                new ComboStepTiming(1f, 0.375f, 0.5f, 0.625f, 0.875f),
                new ComboStepTiming(0.75f, 0.25f, 0.375f, 0.5f, 0.625f),
                new ComboStepTiming(1f, 0.375f, 0.5f, 0f, 0f),
            });
            List<ComboEvent> coarseEvents = new List<ComboEvent>();
            coarse.Press(Start, coarseEvents);
            coarse.Press(Start + 0.75, coarseEvents);
            coarse.Advance(Start + 4, coarseEvents);

            StartChain();
            events.Clear();
            machine.Advance(Start, events);
            for (double t = Start + 0.0625; t < Start + 0.75; t += 0.0625)
            {
                machine.Advance(t, events);
            }

            Press(Start + 0.75);
            for (double t = Start + 0.75 + 0.0625; t < Start + 4; t += 0.0625)
            {
                machine.Advance(t, events);
            }

            machine.Advance(Start + 4, events);

            // The stepped run did not record the first StepStarted (cleared in StartChain), so compare from there.
            CollectionAssert.AreEqual(coarseEvents.Skip(1).Select(Describe).ToArray(), Events());
        }

        [Test]
        public void Interrupt_WhileTheHitIsOpen_ClosesItAndEndsTheStep()
        {
            StartChain();
            machine.Advance(Start + 0.4375, events);
            events.Clear();

            machine.Interrupt(Start + 0.4375, events);

            CollectionAssert.AreEqual(new[] { "HitClosed:0@10.4375", "StepEnded:0@10.4375" }, Events());
            Assert.IsFalse(machine.IsRunning);
        }

        [Test]
        public void Interrupt_BeforeTheHit_EndsTheStepOnly()
        {
            StartChain();

            machine.Interrupt(Start + 0.125, events);

            CollectionAssert.AreEqual(new[] { "StepEnded:0@10.125" }, Events());
        }

        [Test]
        public void Interrupt_WhenIdle_DoesNothing()
        {
            machine.Interrupt(Start, events);

            Assert.IsEmpty(events);
        }

        [Test]
        public void AfterInterrupt_NextPressStartsFresh()
        {
            StartChain();
            Press(Start + 0.75);
            machine.Interrupt(Start + 0.8125, events);
            events.Clear();

            ComboPressResult result = Press(Start + 0.875);

            Assert.AreEqual(ComboPressOutcome.Started, result.Outcome);
            Assert.AreEqual(0, result.Step);
        }

        [Test]
        public void PressBeforeTheStepStarted_IsRejected()
        {
            StartChain();

            Assert.AreEqual(ComboPressOutcome.Rejected, Press(Start - 0.25).Outcome);
            Assert.AreEqual(0, machine.CurrentStep);
        }

        [Test]
        public void AdvanceWithAnEarlierTime_IsIgnored()
        {
            StartChain();
            machine.Advance(Start + 0.75, events);
            events.Clear();

            machine.Advance(Start + 0.25, events);

            Assert.IsEmpty(events);
            Assert.AreEqual(0, machine.CurrentStep);
        }

        [Test]
        public void PressCarryingAnEarlierTime_StillChainsIfTheStepIsRunning()
        {
            StartChain();
            machine.Advance(Start + 0.9375, events);
            events.Clear();

            // The press was made at 0.75 s but only arrived after the machine had reached 0.9375 s.
            ComboPressResult result = Press(Start + 0.75);

            Assert.AreEqual(ComboPressOutcome.Chained, result.Outcome);
            Assert.AreEqual(Start + 0.75, machine.CurrentStepStart);
            CollectionAssert.AreEqual(new[] { "StepEnded:0@10.75*", "StepStarted:1@10.75" }, Events());
        }

        [Test]
        public void PressCarryingAnEarlierTime_IsRejectedIfTheWindowHadNotOpenedThen()
        {
            StartChain();
            machine.Advance(Start + 0.9375, events);

            Assert.AreEqual(ComboPressOutcome.Rejected, Press(Start + 0.5).Outcome);
        }

        [Test]
        public void PreviewPress_NeverChangesTheMachine()
        {
            StartChain();

            machine.PreviewPress(Start + 0.75);
            machine.PreviewPress(Start + 5);

            Assert.AreEqual(0, machine.CurrentStep);
            Assert.AreEqual(Start, machine.CurrentStepStart);
            Assert.IsEmpty(events);
        }

        [Test]
        public void PreviewPress_AgreesWithPress_AtEveryMoment()
        {
            // Compare a fresh machine's preview with what a second, identical machine's press does, across the whole chain and a bit beyond.
            for (double elapsed = -0.25; elapsed < 5; elapsed += 0.03125)
            {
                foreach (bool running in new[] { false, true })
                {
                    ComboMachine previewed = Build();
                    ComboMachine pressed = Build();
                    List<ComboEvent> sink = new List<ComboEvent>();
                    if (running)
                    {
                        previewed.Press(Start, sink);
                        pressed.Press(Start, sink);
                    }

                    // Catch both up to the press time first when it is later, as the server's per-frame update does.
                    double at = Start + elapsed;
                    previewed.Advance(at, sink);
                    pressed.Advance(at, sink);

                    ComboPressOutcome expected = previewed.PreviewPress(at);
                    ComboPressOutcome actual = pressed.Press(at, sink).Outcome;

                    Assert.AreEqual(actual, expected, $"running {running}, elapsed {elapsed}");
                }
            }
        }

        [Test]
        public void PreviewPress_WhenIdle_SaysStarted()
        {
            Assert.AreEqual(ComboPressOutcome.Started, machine.PreviewPress(Start));
        }

        [Test]
        public void PreviewPress_AfterTheStepRanOutUnadvanced_SaysStarted()
        {
            StartChain();

            Assert.AreEqual(ComboPressOutcome.Started, machine.PreviewPress(Start + 2));
        }

        [Test]
        public void PreviewPress_ForARewoundTime_MatchesPress()
        {
            StartChain();
            machine.Advance(Start + 0.9375, events);

            Assert.AreEqual(ComboPressOutcome.Chained, machine.PreviewPress(Start + 0.75));
            Assert.AreEqual(ComboPressOutcome.Rejected, machine.PreviewPress(Start + 0.5));
        }

        private static ComboMachine Build()
        {
            return new ComboMachine(new[]
            {
                new ComboStepTiming(1f, 0.375f, 0.5f, 0.625f, 0.875f),
                new ComboStepTiming(0.75f, 0.25f, 0.375f, 0.5f, 0.625f),
                new ComboStepTiming(1f, 0.375f, 0.5f, 0f, 0f),
            });
        }

        [Test]
        public void ComboDefinition_BuildsAMachineFromItsSteps()
        {
            ComboDefinition combo = ComboDefinition.Create(new[]
            {
                new ComboStep("A1", null, 1f, 0.375f, 0.5f, 0.625f, 0.875f),
                new ComboStep("A2", null, 0.75f, 0.25f, 0.375f),
            });

            try
            {
                ComboMachine built = combo.CreateMachine();
                List<ComboEvent> builtEvents = new List<ComboEvent>();

                Assert.AreEqual(2, built.StepCount);
                built.Press(Start, builtEvents);
                Assert.AreEqual(ComboPressOutcome.Rejected, built.Press(Start + 0.5, builtEvents).Outcome);
                Assert.AreEqual(ComboPressOutcome.Chained, built.Press(Start + 0.75, builtEvents).Outcome);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(combo);
            }
        }
    }
}
