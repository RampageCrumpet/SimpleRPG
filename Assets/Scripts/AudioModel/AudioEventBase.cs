using System.Collections.Generic;
using UnityEngine;

namespace SimpleRPG.Audio
{
    /// <summary>
    /// What is wrong, or suspicious, in an audio definition. Errors are definitions that cannot work; warnings are ones that will be silent or odd.
    /// </summary>
    public sealed class AudioValidationReport
    {
        public readonly List<string> Errors = new List<string>();
        public readonly List<string> Warnings = new List<string>();

        public bool IsClean => Errors.Count == 0 && Warnings.Count == 0;
    }

    /// <summary>
    /// Anything gameplay can ask to be played: a single sound (<see cref="AudioEventDefinition"/>) or a table that picks one from context
    /// (<see cref="AudioEventTable"/>). Gameplay code holds this type and never needs to know which it is.
    /// </summary>
    public abstract class AudioEventBase : ScriptableObject
    {
        /// <summary>Tables nested deeper than this resolve to nothing, which also ends an accidental cycle.</summary>
        public const int MaxResolveDepth = 4;

        /// <summary>
        /// The sound this request means in this context, or null for silence. Never throws.
        /// </summary>
        public AudioEventDefinition Resolve(in AudioContext context) => ResolveAt(context, 0);

        internal abstract AudioEventDefinition ResolveAt(in AudioContext context, int depth);

        /// <summary>
        /// Adds anything wrong with this definition to <paramref name="report"/>.
        /// </summary>
        public abstract void Validate(AudioValidationReport report);
    }
}
