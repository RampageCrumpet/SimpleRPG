using System;
using System.Collections.Generic;

namespace SimpleRPG.Abilities
{
    /// <summary>
    /// What a press did to the combo.
    /// </summary>
    public enum ComboPressOutcome
    {
        /// <summary>The press changed nothing: it was before the chain window, after it, or on the last step. It is not remembered.</summary>
        Rejected,

        /// <summary>No chain was running, so the press started the first step.</summary>
        Started,

        /// <summary>The press landed in a chain window and cut the running step short to start the next one.</summary>
        Chained,
    }

    /// <summary>
    /// The result of <see cref="ComboMachine.Press"/>.
    /// </summary>
    public readonly struct ComboPressResult
    {
        public ComboPressResult(ComboPressOutcome outcome, int step)
        {
            Outcome = outcome;
            Step = step;
        }

        public ComboPressOutcome Outcome { get; }

        /// <summary>The step (counting from 0) the press started, or -1 if it was rejected.</summary>
        public int Step { get; }

        public bool Accepted => Outcome != ComboPressOutcome.Rejected;
    }

    public enum ComboEventKind
    {
        StepStarted,
        HitOpened,
        HitClosed,
        StepEnded,
    }

    /// <summary>
    /// Something that happened in the combo. The time is when it happened in combo time, not when it was noticed, so a late
    /// <see cref="ComboMachine.Advance"/> reports the same times as a frame-by-frame one.
    /// </summary>
    public readonly struct ComboEvent
    {
        public ComboEvent(ComboEventKind kind, int step, double time, bool chained = false)
        {
            Kind = kind;
            Step = step;
            Time = time;
            Chained = chained;
        }

        public ComboEventKind Kind { get; }

        /// <summary>The step (counting from 0) the event is about.</summary>
        public int Step { get; }

        public double Time { get; }

        /// <summary>For <see cref="ComboEventKind.StepEnded"/>: true if the step was cut short by a chain press, false if it ran out or was interrupted.</summary>
        public bool Chained { get; }
    }

    /// <summary>
    /// The logic of a melee combo chain. It knows which step is running and when, accepts or rejects presses, and reports when each step's hit
    /// window opens and closes. It has no clock, no engine types and no network: callers pass the time in, so the server and the owning
    /// player's client run exactly the same code, and tests can drive it by hand.
    /// <para>
    /// A press inside the running step's chain window cuts that step short and starts the next. A press anywhere else is rejected and forgotten;
    /// nothing is buffered. Times are seconds on any clock the caller keeps consistent. <see cref="Advance"/> must not be given an earlier time than before
    /// (an earlier time is ignored). A press may carry an earlier time than the last advance, to credit network latency, as long as its step is still running.
    /// </para>
    /// </summary>
    public sealed class ComboMachine
    {
        private readonly ComboStepTiming[] steps;

        // The step running now (counting from 0), or -1 when no chain is running.
        private int current = -1;

        private double stepStart;

        // The latest time the machine has caught up to.
        private double processedUntil = double.NegativeInfinity;

        private bool hitOpened;

        private bool hitClosed;

        /// <summary>
        /// Creates a machine for a chain of steps.
        /// </summary>
        /// <param name="steps">The timing of each step, in chain order. Must not be empty.</param>
        public ComboMachine(IReadOnlyList<ComboStepTiming> steps)
        {
            if (steps == null || steps.Count == 0)
            {
                throw new ArgumentException("A combo needs at least one step.", nameof(steps));
            }

            this.steps = new ComboStepTiming[steps.Count];
            for (int i = 0; i < steps.Count; i++)
            {
                this.steps[i] = steps[i];
            }
        }

        public int StepCount => steps.Length;

        /// <summary>
        /// True while a step is running.
        /// </summary>
        public bool IsRunning => current >= 0;

        /// <summary>
        /// The step running now (counting from 0), or -1 if no chain is running.
        /// </summary>
        public int CurrentStep => current;

        /// <summary>
        /// When the running step started. Only meaningful while <see cref="IsRunning"/>.
        /// </summary>
        public double CurrentStepStart => stepStart;

        /// <summary>
        /// Catches the machine up to a time: reports hit windows that opened or closed, and the step ending if it ran out.
        /// </summary>
        /// <param name="now">The time to catch up to.</param>
        /// <param name="events">Receives what happened, in order.</param>
        public void Advance(double now, List<ComboEvent> events)
        {
            if (now < processedUntil)
            {
                return;
            }

            processedUntil = now;
            if (current < 0)
            {
                return;
            }

            ComboStepTiming step = steps[current];
            double elapsed = now - stepStart;
            if (!hitOpened && elapsed >= step.HitStart)
            {
                hitOpened = true;
                events.Add(new ComboEvent(ComboEventKind.HitOpened, current, stepStart + step.HitStart));
            }

            if (hitOpened && !hitClosed && elapsed >= step.HitEnd)
            {
                hitClosed = true;
                events.Add(new ComboEvent(ComboEventKind.HitClosed, current, stepStart + step.HitEnd));
            }

            if (elapsed >= step.Duration)
            {
                EndStep(stepStart + step.Duration, chained: false, events);
            }
        }

        /// <summary>
        /// Handles a press. Starts the chain if none is running, chains into the next step if the press is inside the running step's
        /// chain window, and otherwise does nothing.
        /// </summary>
        /// <param name="now">When the press happened.</param>
        /// <param name="events">Receives what happened, in order, including anything that finished before the press.</param>
        public ComboPressResult Press(double now, List<ComboEvent> events)
        {
            Advance(now, events);

            if (current < 0)
            {
                Begin(0, now, events);
                return new ComboPressResult(ComboPressOutcome.Started, 0);
            }

            double elapsed = now - stepStart;
            int next = current + 1;
            if (elapsed < 0 || next >= steps.Length || !steps[current].IsInChainWindow(elapsed))
            {
                return new ComboPressResult(ComboPressOutcome.Rejected, -1);
            }

            EndStep(now, chained: true, events);
            Begin(next, now, events);
            return new ComboPressResult(ComboPressOutcome.Chained, next);
        }

        /// <summary>
        /// Says what <see cref="Press"/> would do at this time, without changing anything. Lets a caller refuse a press for a reason of its own,
        /// such as a cooldown that only applies to starting a fresh chain, before the machine commits to it.
        /// </summary>
        /// <param name="now">When the press happened.</param>
        public ComboPressOutcome PreviewPress(double now)
        {
            // A step that has run out by the time of the press is gone, exactly as Press would find it after catching up.
            bool ranOut = current >= 0 && now >= processedUntil && now - stepStart >= steps[current].Duration;
            if (current < 0 || ranOut)
            {
                return ComboPressOutcome.Started;
            }

            double elapsed = now - stepStart;
            bool chains = elapsed >= 0 && current + 1 < steps.Length && steps[current].IsInChainWindow(elapsed);
            return chains ? ComboPressOutcome.Chained : ComboPressOutcome.Rejected;
        }

        /// <summary>
        /// Stops the running step at once, for example because the character died or was stunned. Reports the hit window closing if it was open.
        /// Does nothing if no chain is running.
        /// </summary>
        public void Interrupt(double now, List<ComboEvent> events)
        {
            Advance(now, events);
            if (current >= 0)
            {
                EndStep(now, chained: false, events);
            }
        }

        private void Begin(int step, double time, List<ComboEvent> events)
        {
            current = step;
            stepStart = time;
            hitOpened = false;
            hitClosed = false;
            events.Add(new ComboEvent(ComboEventKind.StepStarted, step, time));
        }

        private void EndStep(double time, bool chained, List<ComboEvent> events)
        {
            if (hitOpened && !hitClosed)
            {
                hitClosed = true;
                events.Add(new ComboEvent(ComboEventKind.HitClosed, current, time));
            }

            events.Add(new ComboEvent(ComboEventKind.StepEnded, current, time, chained));
            current = -1;
        }
    }
}
