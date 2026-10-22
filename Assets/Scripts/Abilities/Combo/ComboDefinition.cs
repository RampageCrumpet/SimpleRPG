using System;
using System.Collections.Generic;
using UnityEngine;

namespace SimpleRPG.Abilities
{
    /// <summary>
    /// An ordered chain of melee attacks a weapon performs. A press inside one step's chain window cuts that step short and starts the next.
    /// Immutable at runtime; the combo state lives in the character's combo logic, never here. It has no registry id because only a step
    /// index ever goes over the network.
    /// </summary>
    [CreateAssetMenu(fileName = "ComboDefinition", menuName = "Abilities/Combo Definition")]
    public sealed class ComboDefinition : ScriptableObject
    {
        /// <summary>
        /// Seconds a step's duration may differ from its clip's length. One frame at 60 fps, so a clip trimmed by a frame still passes.
        /// </summary>
        public const float ClipLengthTolerance = 1f / 60f;

        // Absorbs float rounding when an author puts one time exactly on another, such as the hit ending when the step does.
        private const float Epsilon = 1e-4f;

        [SerializeField] private ComboStep[] steps = Array.Empty<ComboStep>();

        /// <summary>
        /// The steps in chain order. The first is the one a fresh chain starts with.
        /// </summary>
        public IReadOnlyList<ComboStep> Steps => steps;

        public int StepCount => steps.Length;

        /// <summary>
        /// Creates a combo from code, for tools and tests. Authored combos are assets.
        /// </summary>
        public static ComboDefinition Create(IEnumerable<ComboStep> steps)
        {
            ComboDefinition combo = CreateInstance<ComboDefinition>();
            combo.steps = new List<ComboStep>(steps).ToArray();
            return combo;
        }

        /// <summary>
        /// Creates the combo logic for this combo. Each character that performs it needs its own machine, because the machine holds the running state.
        /// </summary>
        public ComboMachine CreateMachine()
        {
            ComboStepTiming[] timings = new ComboStepTiming[steps.Length];
            for (int i = 0; i < steps.Length; i++)
            {
                timings[i] = steps[i].Timing;
            }

            return new ComboMachine(timings);
        }

        /// <summary>
        /// Lists everything wrong with the combo's data, or nothing if it is sound. Each message names the step it is about (counting from 1).
        /// </summary>
        public List<string> Validate()
        {
            List<string> problems = new List<string>();
            if (steps.Length == 0)
            {
                problems.Add("The combo has no steps.");
                return problems;
            }

            for (int i = 0; i < steps.Length; i++)
            {
                ValidateStep(steps[i], i + 1, isLast: i == steps.Length - 1, problems);
            }

            return problems;
        }

        private static void ValidateStep(ComboStep step, int number, bool isLast, List<string> problems)
        {
            string prefix = "Step " + number + ": ";
            if (step == null)
            {
                problems.Add(prefix + "is empty.");
                return;
            }

            if (string.IsNullOrWhiteSpace(step.State))
            {
                problems.Add(prefix + "has no animator state.");
            }

            if (step.Clip == null)
            {
                problems.Add(prefix + "has no clip.");
            }
            else if (Mathf.Abs(step.Duration - step.Clip.length) > ClipLengthTolerance)
            {
                problems.Add(prefix + "duration " + step.Duration + " s does not match clip '" + step.Clip.name + "' (" + step.Clip.length + " s).");
            }

            if (step.Duration <= 0f)
            {
                problems.Add(prefix + "duration must be greater than zero.");
            }

            if (step.HitEnd <= step.HitStart)
            {
                problems.Add(prefix + "hit window must end after it starts.");
            }

            if (step.HitEnd > step.Duration + Epsilon)
            {
                problems.Add(prefix + "hit window ends after the step does.");
            }

            if (isLast)
            {
                if (step.ChainOpen != 0f || step.ChainClose != 0f)
                {
                    problems.Add(prefix + "is the last step, so it can't have a chain window.");
                }
            }
            else
            {
                if (!step.HasChainWindow)
                {
                    problems.Add(prefix + "needs a chain window to reach step " + (number + 1) + ".");
                }
                else
                {
                    if (step.ChainOpen < step.HitEnd - Epsilon)
                    {
                        problems.Add(prefix + "chain window opens before the hit window ends.");
                    }

                    if (step.ChainClose > step.Duration + Epsilon)
                    {
                        problems.Add(prefix + "chain window closes after the step ends.");
                    }
                }
            }

            if (step.StartOffset >= step.Duration)
            {
                problems.Add(prefix + "start offset skips the whole step.");
            }

            if (step.DamageScalar < 0f)
            {
                problems.Add(prefix + "damage scalar can't be negative.");
            }
        }
    }
}
