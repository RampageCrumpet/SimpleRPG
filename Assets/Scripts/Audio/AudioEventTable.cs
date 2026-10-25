using System;
using System.Collections.Generic;
using UnityEngine;

namespace SimpleRPG.Audio
{
    /// <summary>One requirement of a table entry: the context's facet must be this tag or a descendant of it.</summary>
    [Serializable]
    public struct AudioCondition
    {
        public AudioFacet facet;
        public AudioTag tag;
    }

    /// <summary>
    /// One row of an <see cref="AudioEventTable"/>: when every condition holds, this event is a candidate. The best-matching row wins.
    /// </summary>
    [Serializable]
    public sealed class AudioTableEntry
    {
        [Tooltip("Every one of these must hold. A tag matches itself and anything that falls back to it (Dirt matches Gravel).")]
        public AudioCondition[] conditions = Array.Empty<AudioCondition>();

        [Tooltip("Only for these body regions. None means any.")]
        public AudioRegion regions;

        [Range(0f, 1f)] public float minIntensity;

        [Range(0f, 1f)] public float maxIntensity = 1f;

        [Tooltip("What plays. May be another table.")]
        public AudioEventBase result;

        /// <summary>
        /// How well this row fits the context, or a negative number if it does not fit. More conditions, closer tag matches and narrower
        /// ranges all score higher, so the most specific row wins whatever order the rows are in.
        /// </summary>
        public int Score(in AudioContext context)
        {
            int score = 0;
            if (conditions != null)
            {
                foreach (AudioCondition condition in conditions)
                {
                    if (condition.tag == null)
                    {
                        return -1;
                    }

                    AudioTag actual = context.Get(condition.facet);
                    int distance = actual != null ? actual.DistanceTo(condition.tag) : -1;
                    if (distance < 0)
                    {
                        return -1;
                    }

                    score += 1000 - 10 * distance;
                }
            }

            if (regions != AudioRegion.None)
            {
                if ((regions & context.Region) == AudioRegion.None)
                {
                    return -1;
                }

                score += 1000;
            }

            bool bounded = minIntensity > 0f || maxIntensity < 1f;
            if (bounded)
            {
                if (context.Intensity < minIntensity || context.Intensity > maxIntensity)
                {
                    return -1;
                }

                score += 500;
            }

            return score;
        }
    }

    /// <summary>
    /// Chooses an event from context: a footstep by surface and footwear, an impact by the two materials and the body region. Rows are matched by
    /// specificity, then the fallback plays if none fit. A weapon or monster can skip the table by naming its own event.
    /// </summary>
    [CreateAssetMenu(fileName = "AudioTable", menuName = "Audio/Event Table")]
    public sealed class AudioEventTable : AudioEventBase
    {
        [SerializeField] private AudioTableEntry[] entries = Array.Empty<AudioTableEntry>();

        [Tooltip("Plays when no row fits. Leave empty for silence.")]
        [SerializeField] private AudioEventBase fallback;

        public IReadOnlyList<AudioTableEntry> Entries => entries;

        public AudioEventBase Fallback => fallback;

        /// <summary>Creates a table from code, for tools and tests. Authored tables are assets.</summary>
        public static AudioEventTable Create(string name, AudioEventBase fallback, params AudioTableEntry[] entries)
        {
            AudioEventTable table = CreateInstance<AudioEventTable>();
            table.name = name;
            table.fallback = fallback;
            table.entries = entries;
            return table;
        }

        /// <summary>A row for tools and tests.</summary>
        public static AudioTableEntry Row(AudioEventBase result, params AudioCondition[] conditions)
        {
            return new AudioTableEntry { result = result, conditions = conditions };
        }

        internal override AudioEventDefinition ResolveAt(in AudioContext context, int depth)
        {
            if (depth >= MaxResolveDepth)
            {
                return null;
            }

            AudioTableEntry best = null;
            int bestScore = -1;
            if (entries != null)
            {
                foreach (AudioTableEntry entry in entries)
                {
                    if (entry == null || entry.result == null)
                    {
                        continue;
                    }

                    int score = entry.Score(context);
                    if (score > bestScore)
                    {
                        best = entry;
                        bestScore = score;
                    }
                }
            }

            if (best != null)
            {
                AudioEventDefinition found = best.result.ResolveAt(context, depth + 1);
                if (found != null)
                {
                    return found;
                }
            }

            return fallback != null ? fallback.ResolveAt(context, depth + 1) : null;
        }

        public override void Validate(AudioValidationReport report)
        {
            if (entries == null || entries.Length == 0)
            {
                if (fallback == null)
                {
                    report.Warnings.Add(name + ": has no rows and no fallback, so it is always silent.");
                }

                return;
            }

            for (int i = 0; i < entries.Length; i++)
            {
                AudioTableEntry entry = entries[i];
                if (entry == null || entry.result == null)
                {
                    report.Errors.Add(name + ": row " + (i + 1) + " has no event.");
                    continue;
                }

                if (entry.result == this)
                {
                    report.Errors.Add(name + ": row " + (i + 1) + " refers to its own table.");
                }

                if (entry.minIntensity > entry.maxIntensity)
                {
                    report.Errors.Add(name + ": row " + (i + 1) + " has Min Intensity above Max Intensity.");
                }

                if (entry.conditions != null)
                {
                    foreach (AudioCondition condition in entry.conditions)
                    {
                        if (condition.tag == null)
                        {
                            report.Errors.Add(name + ": row " + (i + 1) + " has a condition with no tag.");
                        }
                    }
                }
            }
        }
    }
}
