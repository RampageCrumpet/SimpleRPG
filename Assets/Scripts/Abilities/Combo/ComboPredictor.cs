using System.Collections.Generic;

namespace SimpleRPG.Abilities
{
    /// <summary>
    /// What a client press predicted.
    /// </summary>
    public readonly struct PredictedPress
    {
        public PredictedPress(int pressId, int attackNumber)
        {
            PressId = pressId;
            AttackNumber = attackNumber;
        }

        /// <summary>Names the press to the server, which echoes it so the client can match the answer to the guess. Never zero.</summary>
        public int PressId { get; }

        /// <summary>The attack (counting from 1) the client expects the server to start and has begun showing, or 0 if it predicted nothing.</summary>
        public int AttackNumber { get; }

        public bool Predicted => AttackNumber > 0;
    }

    /// <summary>
    /// What the server's answer means for a client that predicted.
    /// </summary>
    public enum PredictionVerdict
    {
        /// <summary>The server started the attack the client already shows. Nothing to do.</summary>
        Confirmed,

        /// <summary>The server started an attack the client did not show, or a different one. The client should show the server's and follow it.</summary>
        Replay,
    }

    /// <summary>
    /// The owning player's guess about what the server will do with a press, so the swing can start on the click instead of a round trip later.
    /// It runs the same <see cref="ComboMachine"/> the server runs, on the client's own clock, remembers which guesses the server has not
    /// answered, and says when a guess turned out wrong. It has no engine or network types.
    /// <para>
    /// The server always has the last word. A press the server rejects, or never answers within the timeout, rolls the guess back.
    /// </para>
    /// </summary>
    public sealed class ComboPredictor
    {
        private readonly struct Pending
        {
            public Pending(int pressId, int attackNumber, double madeAt)
            {
                PressId = pressId;
                AttackNumber = attackNumber;
                MadeAt = madeAt;
            }

            public int PressId { get; }

            public int AttackNumber { get; }

            public double MadeAt { get; }
        }

        private readonly ComboMachine machine;
        private readonly double timeoutSeconds;
        private readonly List<Pending> pending = new List<Pending>();
        private int lastPressId;

        /// <param name="machine">The client's own machine for the combo. The predictor owns it from now on.</param>
        /// <param name="timeoutSeconds">How long a guess waits for the server before it is given up.</param>
        public ComboPredictor(ComboMachine machine, double timeoutSeconds)
        {
            this.machine = machine;
            this.timeoutSeconds = timeoutSeconds;
        }

        public ComboMachine Machine => machine;

        /// <summary>
        /// True while the client has shown an attack the server has not yet confirmed.
        /// </summary>
        public bool HasPendingPrediction => pending.Count > 0;

        /// <summary>
        /// Catches the machine up to the client's time, so steps that ran out end and the next press is judged against the right step.
        /// </summary>
        public void Advance(double now, List<ComboEvent> events)
        {
            machine.Advance(now, events);
        }

        /// <summary>
        /// A press on this client. Guesses whether the server will accept it, and if so starts the attack in the client's machine. The press
        /// should be sent to the server either way: the server may accept a press the client judged just outside a window.
        /// </summary>
        /// <param name="now">The client's time.</param>
        /// <param name="freshAllowed">False if starting a fresh chain is not allowed, such as while the ability's cooldown is showing.</param>
        public PredictedPress Press(double now, bool freshAllowed, List<ComboEvent> events)
        {
            int id = ++lastPressId;
            machine.Advance(now, events);
            ComboPressOutcome preview = machine.PreviewPress(now);
            if (preview == ComboPressOutcome.Rejected || (preview == ComboPressOutcome.Started && !freshAllowed))
            {
                return new PredictedPress(id, 0);
            }

            ComboPressResult result = machine.Press(now, events);
            int attack = result.Step + 1;
            pending.Add(new Pending(id, attack, now));
            return new PredictedPress(id, attack);
        }

        /// <summary>
        /// The server started an attack. Matches it against what the client guessed.
        /// </summary>
        /// <param name="pressId">The press the server is answering, or zero if the server started it for another reason.</param>
        /// <param name="attackNumber">The attack (counting from 1) the server started.</param>
        /// <param name="secondsIntoAttack">How far into that attack the server says it is.</param>
        /// <param name="now">The client's time.</param>
        public PredictionVerdict Confirm(int pressId, int attackNumber, double secondsIntoAttack, double now, List<ComboEvent> events)
        {
            int index = pending.FindIndex(p => p.PressId == pressId);
            if (index >= 0 && pending[index].AttackNumber == attackNumber)
            {
                // Older guesses were answered by this one arriving, since the server answers in order.
                pending.RemoveRange(0, index + 1);
                return PredictionVerdict.Confirmed;
            }

            // Whatever the client guessed is wrong or missing: follow the server.
            pending.Clear();
            machine.Resync(attackNumber - 1, now - secondsIntoAttack, events);
            return PredictionVerdict.Replay;
        }

        /// <summary>
        /// The server refused a press.
        /// </summary>
        /// <returns>True if the client had shown an attack for it, which must now be taken back.</returns>
        public bool Reject(int pressId, double now, List<ComboEvent> events)
        {
            if (!pending.Exists(p => p.PressId == pressId))
            {
                return false;
            }

            RollBack(now, events);
            return true;
        }

        /// <summary>
        /// Gives up guesses the server never answered.
        /// </summary>
        /// <returns>True if an attack the client showed must now be taken back.</returns>
        public bool Expire(double now, List<ComboEvent> events)
        {
            if (pending.Count == 0 || now - pending[0].MadeAt <= timeoutSeconds)
            {
                return false;
            }

            RollBack(now, events);
            return true;
        }

        /// <summary>
        /// Forgets the combo, such as when the character dies or changes weapon.
        /// </summary>
        public void Reset(double now, List<ComboEvent> events)
        {
            pending.Clear();
            machine.Interrupt(now, events);
        }

        private void RollBack(double now, List<ComboEvent> events)
        {
            pending.Clear();
            machine.Interrupt(now, events);
        }
    }
}
