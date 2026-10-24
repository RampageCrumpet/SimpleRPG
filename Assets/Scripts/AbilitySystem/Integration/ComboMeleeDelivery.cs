using SimpleRPG.Articulation;
using SimpleRPG.Gameplay;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

namespace SimpleRPG.Abilities.Integration
{
    /// <summary>
    /// Melee mechanics for a character whose weapon has a combo, such as the player. The server runs a <see cref="ComboMachine"/> on the wielded
    /// weapon's <see cref="ComboDefinition"/>: it decides whether each press starts a chain, continues it or is ignored, and the combo's data says
    /// when each swing's hit window is open, so hits do not depend on the animator reaching an event.
    /// <para>
    /// The owning player's client guesses the server's answer with a <see cref="ComboPredictor"/> and shows the swing at once, then follows the
    /// server: a confirmed guess is left alone, a wrong one is replaced, a refused one is taken back. Other clients show what the server says,
    /// joining the attack part-way through when the news is late.
    /// </para>
    /// <para>
    /// The weapon's hitbox still rides the animated blade, so the server must play each step's animation in step with the machine.
    /// </para>
    /// </summary>
    public sealed class ComboMeleeDelivery : MeleeDeliveryBase, IArticulationPhaseSource
    {
        /// <summary>
        /// The most network delay the server credits to a press, in seconds. A press stamped earlier than this is treated as made this long ago,
        /// so a lagging or dishonest client can't claim a chain window that closed long before. Keep a combo step's chain window closing at
        /// least this long before the step ends, or a press made in time can arrive after the step has run out.
        /// </summary>
        public const double MaxRewindSeconds = 0.15;

        /// <summary>
        /// How long an owning client waits for the server to answer a press it predicted before it gives the guess up. Far longer than any
        /// round trip worth playing through; it only catches answers that never come, such as a press refused before it reached the melee mechanics.
        /// </summary>
        public const double PredictionTimeoutSeconds = 1.0;

        private readonly List<ComboEvent> events = new List<ComboEvent>();

        private IGameplayClock clock;

        private ComboDefinition combo;

        private bool warnedNoCombo;

        // ----- Server -----

        private ComboMachine machine;

        // The step a press reserved for the ability's next Begin, or -1, with the press that reserved it and when the step started.
        private int pendingStep = -1;

        private int pendingPressId;

        private double pendingStartTime;

        // ----- Owning client -----

        private ComboDefinition predictedCombo;

        private ComboPredictor predictor;

        /// <summary>
        /// The clock combo time runs on. The network clock unless a test supplies its own.
        /// </summary>
        public IGameplayClock Clock
        {
            get => clock ??= AbilityServices.Current.Clock;
            set => clock = value;
        }

        /// <summary>
        /// The step running now (counting from 1), or 0 if no chain is running. Server only.
        /// </summary>
        public int CurrentAttack => machine != null && machine.IsRunning ? machine.CurrentStep + 1 : 0;

        // True on the client of the player who owns this character, which guesses the server's answers. A host already is the server.
        private bool Predicts => IsSpawned && IsOwner && !IsServer;

        /// <inheritdoc/>
        public ArticulationPhase CurrentPhase
        {
            get
            {
                ComboPhase phase = ComboPhase.None;
                if (IsServer)
                {
                    phase = machine != null ? machine.PhaseAt(Clock.ElapsedSeconds) : ComboPhase.None;
                }
                else if (Predicts && predictor != null)
                {
                    phase = predictor.Machine.PhaseAt(NetworkManager.LocalTime.Time);
                }

                switch (phase)
                {
                    case ComboPhase.Windup: return ArticulationPhase.Windup;
                    case ComboPhase.Active: return ArticulationPhase.Active;
                    case ComboPhase.Recovery: return ArticulationPhase.Recovery;
                    default: return ArticulationPhase.Free;
                }
            }
        }

        public override AttackAnimation AnimationFor(int attackNumber)
        {
            // Every machine finds the step in the wielder's combo, which is the same data the server runs.
            ComboDefinition wielded = EquippedCombo();
            int index = attackNumber - 1;
            if (wielded == null || index < 0 || index >= wielded.StepCount)
            {
                return default;
            }

            ComboStep step = wielded.Steps[index];
            return AttackAnimation.FromState(step.State, step.BlendIn, step.StartOffset);
        }

        protected override float DamageScalar(int attackNumber)
        {
            int index = attackNumber - 1;
            return combo != null && index >= 0 && index < combo.StepCount ? combo.Steps[index].DamageScalar : 1f;
        }

        private void Update()
        {
            if (IsServer)
            {
                if (machine != null && machine.IsRunning)
                {
                    machine.Advance(Clock.ElapsedSeconds, events);
                    Dispatch();
                }
            }
            else if (Predicts && predictor != null)
            {
                double now = NetworkManager.LocalTime.Time;
                predictor.Advance(now, events);
                bool gaveUp = predictor.Expire(now, events);
                events.Clear();
                if (gaveUp)
                {
                    RaiseAttackCancelled();
                }
            }
        }

        // ----- The owning client's guess -----

        /// <inheritdoc/>
        public override int BeginClientPress(bool freshAllowed)
        {
            ComboPredictor current = PredictorForEquippedWeapon();
            if (!Predicts || current == null || !Character.IsAlive)
            {
                return 0;
            }

            PredictedPress press = current.Press(NetworkManager.LocalTime.Time, freshAllowed, events);
            events.Clear();
            if (press.Predicted)
            {
                RaiseAttackStarted(press.AttackNumber, 0f);
            }

            return press.PressId;
        }

        /// <inheritdoc/>
        protected override void OnAttackStarted(int attackNumber, float secondsIntoAttack, int pressId)
        {
            float seconds = Mathf.Min(secondsIntoAttack, StepDuration(attackNumber));
            if (Predicts)
            {
                ComboPredictor current = PredictorForEquippedWeapon();
                if (current != null)
                {
                    PredictionVerdict verdict = current.Confirm(pressId, attackNumber, seconds, NetworkManager.LocalTime.Time, events);
                    events.Clear();
                    if (verdict == PredictionVerdict.Confirmed)
                    {
                        return;
                    }
                }
            }

            RaiseAttackStarted(attackNumber, seconds);
        }

        [Rpc(SendTo.Owner)]
        private void PressRejectedClientRpc(int pressId)
        {
            if (!Predicts || predictor == null)
            {
                return;
            }

            bool takeBack = predictor.Reject(pressId, NetworkManager.LocalTime.Time, events);
            events.Clear();
            if (takeBack)
            {
                RaiseAttackCancelled();
            }
        }

        // The owner's predictor for the combo of the weapon in hand, or null if it has none. A different weapon starts over.
        private ComboPredictor PredictorForEquippedWeapon()
        {
            ComboDefinition wanted = EquippedCombo();
            if (wanted == null)
            {
                return null;
            }

            if (wanted != predictedCombo)
            {
                predictedCombo = wanted;
                predictor = new ComboPredictor(wanted.CreateMachine(), PredictionTimeoutSeconds);
            }

            return predictor;
        }

        private float StepDuration(int attackNumber)
        {
            ComboDefinition wielded = EquippedCombo();
            int index = attackNumber - 1;
            return wielded != null && index >= 0 && index < wielded.StepCount ? wielded.Steps[index].Duration : float.MaxValue;
        }

        private ComboDefinition EquippedCombo()
        {
            WeaponData weapon = Character.WeaponData;
            ComboDefinition wielded = weapon != null ? weapon.Combo : null;
            return wielded != null && wielded.StepCount > 0 ? wielded : null;
        }

        // ----- The server's decision -----

        /// <inheritdoc/>
        public override MeleePressOutcome Press(long pressTick, bool freshAllowed, int pressId = 0)
        {
            if (!IsServer || !Character.IsAlive)
            {
                return MeleePressOutcome.Rejected;
            }

            ComboMachine current = MachineForEquippedWeapon();
            if (current == null)
            {
                return MeleePressOutcome.Rejected;
            }

            double at = PressTime(pressTick, Clock.ElapsedSeconds);
            ComboPressOutcome preview = current.PreviewPress(at);
            if (preview == ComboPressOutcome.Rejected || (preview == ComboPressOutcome.Started && !freshAllowed))
            {
                return MeleePressOutcome.Rejected;
            }

            ComboPressResult result = current.Press(at, events);
            Dispatch();
            pendingStep = result.Step;
            pendingPressId = pressId;
            pendingStartTime = current.CurrentStepStart;
            return result.Outcome == ComboPressOutcome.Started ? MeleePressOutcome.Fresh : MeleePressOutcome.Continued;
        }

        /// <inheritdoc/>
        public override void CancelPress()
        {
            if (pendingStep < 0)
            {
                return;
            }

            pendingStep = -1;
            machine.Interrupt(Clock.ElapsedSeconds, events);
            Dispatch();
        }

        /// <inheritdoc/>
        public override void RejectClientPress(int pressId)
        {
            if (IsServer && pressId != 0)
            {
                PressRejectedClientRpc(pressId);
            }
        }

        /// <inheritdoc/>
        public override bool Begin(in MeleeSwingLaunch launch, IMeleeEvents swingEvents)
        {
            // A swing only starts from a press the combo accepted.
            if (!IsServer || !Character.IsAlive || pendingStep < 0)
            {
                return false;
            }

            int attackNumber = pendingStep + 1;
            int pressId = pendingPressId;
            pendingStep = -1;
            StartSwing(attackNumber, launch, swingEvents, pendingStartTime, pressId);
            return true;
        }

        protected override void ReleaseAllSwings()
        {
            pendingStep = -1;

            // The owner's guess about the chain is moot too; the next life starts with no chain running.
            predictor = null;
            predictedCombo = null;
            if (machine != null)
            {
                machine.Interrupt(Clock.ElapsedSeconds, events);
                Dispatch();
            }

            base.ReleaseAllSwings();
        }

        /// <summary>
        /// The time a press is credited with: when its sender made it, but no more than <see cref="MaxRewindSeconds"/> ago and never in the future.
        /// </summary>
        private double PressTime(long pressTick, double now)
        {
            if (pressTick < 0)
            {
                return now;
            }

            double stamped = GameplayTime.TicksToSeconds(pressTick, Clock.TickRate);
            return System.Math.Min(now, System.Math.Max(stamped, now - MaxRewindSeconds));
        }

        // The machine for the combo of the weapon in hand, or null if it has none. Changing weapons ends the old chain.
        private ComboMachine MachineForEquippedWeapon()
        {
            ComboDefinition wanted = EquippedCombo();
            if (wanted == null)
            {
                if (!warnedNoCombo)
                {
                    warnedNoCombo = true;
                    Debug.LogWarning(name + " has no weapon with a combo, so it cannot swing.", this);
                }

                return null;
            }

            if (wanted != combo)
            {
                if (machine != null)
                {
                    machine.Interrupt(Clock.ElapsedSeconds, events);
                    Dispatch();
                }

                combo = wanted;
                machine = wanted.CreateMachine();
            }

            return machine;
        }

        // Applies what the machine reported to the weapon and to the swings waiting on it, then forgets it.
        private void Dispatch()
        {
            for (int i = 0; i < events.Count; i++)
            {
                ComboEvent e = events[i];
                Swings.TryGetValue(e.Step + 1, out Swing swing);
                switch (e.Kind)
                {
                    case ComboEventKind.HitOpened:
                        if (swing != null)
                        {
                            ActiveSwing = swing;
                            WeaponComponent?.ActivateHitbox();
                        }

                        break;

                    case ComboEventKind.HitClosed:
                        WeaponComponent?.DeactivateHitbox();
                        if (swing != null)
                        {
                            Finish(swing);
                        }

                        break;

                    case ComboEventKind.StepEnded:
                        // A step that ended before its hit window closed (it was cut short) still ends its swing.
                        if (swing != null)
                        {
                            Finish(swing);
                        }

                        break;
                }
            }

            events.Clear();
        }
    }
}
