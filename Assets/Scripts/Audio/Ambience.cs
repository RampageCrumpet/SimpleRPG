using System;
using System.Collections.Generic;
using UnityEngine;

namespace SimpleRPG.Audio
{
    /// <summary>
    /// A sound that happens now and then at a random spot around the listener: a distant drip, a settling stone.
    /// </summary>
    [Serializable]
    public struct IntermittentSound
    {
        public AudioEventBase sound;

        [Min(0f)] public float minInterval;

        [Min(0f)] public float maxInterval;

        [Tooltip("Closest and farthest it is placed from the listener, in meters.")]
        [Min(0f)] public float minDistance;

        [Min(0f)] public float maxDistance;

        [Range(0f, 1f)] public float chance;
    }

    /// <summary>
    /// Decides when intermittent ambience happens. Each sound has its own random interval; after one plays there may be a quiet spell in which
    /// nothing does. Time and randomness are passed in, so the schedule is testable without a scene.
    /// </summary>
    public sealed class AmbienceScheduler
    {
        private readonly AmbienceProfile profile;
        private readonly IAudioRandom random;
        private float[] nextTime;
        private float quietUntil;

        public AmbienceScheduler(AmbienceProfile profile, IAudioRandom random, float now)
        {
            this.profile = profile;
            this.random = random;
            int count = profile != null ? profile.Intermittent.Count : 0;
            nextTime = new float[count];
            for (int i = 0; i < count; i++)
            {
                nextTime[i] = now + Interval(profile.Intermittent[i]);
            }
        }

        /// <summary>Until when nothing plays.</summary>
        public float QuietUntil => quietUntil;

        /// <summary>
        /// Adds the indices of the profile's intermittent sounds that are due at <paramref name="now"/> to <paramref name="due"/>.
        /// </summary>
        public int Tick(float now, List<int> due)
        {
            if (profile == null || now < quietUntil)
            {
                return 0;
            }

            int added = 0;
            for (int i = 0; i < nextTime.Length; i++)
            {
                if (now < nextTime[i])
                {
                    continue;
                }

                IntermittentSound entry = profile.Intermittent[i];
                nextTime[i] = now + Interval(entry);
                if (entry.sound == null || (entry.chance < 1f && random.Value() >= entry.chance))
                {
                    continue;
                }

                due.Add(i);
                added++;
                if (random.Value() < profile.QuietChance)
                {
                    quietUntil = now + Mathf.Lerp(profile.QuietMinSeconds, profile.QuietMaxSeconds, random.Value());
                    break;
                }
            }

            return added;
        }

        private float Interval(IntermittentSound entry)
        {
            return Mathf.Lerp(entry.minInterval, Mathf.Max(entry.minInterval, entry.maxInterval), random.Value());
        }
    }
}
