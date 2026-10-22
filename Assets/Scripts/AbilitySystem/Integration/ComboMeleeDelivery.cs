using SimpleRPG.Gameplay;
using System.Collections.Generic;
using UnityEngine;

namespace SimpleRPG.Abilities.Integration
{
    /// <summary>
    /// Melee mechanics for a character whose weapon has a combo, such as the player. The server runs a <see cref="ComboMachine"/> on the wielded
    /// weapon's <see cref="ComboDefinition"/>: it decides whether each press starts a chain, continues it or is ignored, and the combo's data says
    /// when each swing's hit window is open, so hits do not depend on the animator reaching an event.
    /// <para>
    /// The weapon's hitbox still rides on the animated blade, so the server must play each step's animation in step with the machine.
    /// </para>
    /// </summary>
    public sealed class ComboMeleeDelivery : MeleeDeliveryBase
    {
        /// <summary>
        /// The most network delay the server credits to a press, in seconds. A press stamped earlier than this is treated as made this long ago,
        /// so a lagging or dishonest client can't claim a chain window that closed long before. Keep a combo step's chain window closing at
        /// least this long before the step ends, or a press made in time can arrive after the step has run out.
        /// </summary>
        public const double MaxRewindSeconds = 0.15;

        private readonly List<ComboEvent> events = new List<ComboEvent>();

        private IGameplayClock clock;

        private ComboDefinition combo;

        private ComboMachine machine;

        // The step a press reserved for the ability's next Begin, or -1.
        private int pendingStep = -1;

        private bool warnedNoCombo;

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

        public override AttackAnimation AnimationFor(int attackNumber)
        {
            // Every machine finds the step in the wielder's combo, which is the same data the server runs.
            WeaponData weapon = Character.WeaponData;
            ComboDefinition wielded = weapon != null ? weapon.Combo : null;
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
            if (!IsServer || machine == null || !machine.IsRunning)
            {
                return;
            }

            machine.Advance(Clock.ElapsedSeconds, events);
            Dispatch();
        }

        /// <inheritdoc/>
        public override MeleePressOutcome Press(long pressTick, bool freshAllowed)
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
        public override bool Begin(in MeleeSwingLaunch launch, IMeleeEvents swingEvents)
        {
            // A swing only starts from a press the combo accepted.
            if (!IsServer || !Character.IsAlive || pendingStep < 0)
            {
                return false;
            }

            int attackNumber = pendingStep + 1;
            pendingStep = -1;
            StartSwing(attackNumber, launch, swingEvents);
            return true;
        }

        protected override void ReleaseAllSwings()
        {
            pendingStep = -1;
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
            WeaponData weapon = Character.WeaponData;
            ComboDefinition wanted = weapon != null ? weapon.Combo : null;
            if (wanted == null || wanted.StepCount == 0)
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
