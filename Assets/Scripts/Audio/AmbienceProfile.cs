using System;
using System.Collections.Generic;
using UnityEngine;

namespace SimpleRPG.Audio
{
    /// <summary>
    /// What a place sounds like when nothing is happening. Deliberately sparse: low beds, rare sounds, and long quiet intervals.
    /// </summary>
    [CreateAssetMenu(fileName = "AmbienceProfile", menuName = "Audio/Ambience Profile")]
    public sealed class AmbienceProfile : ScriptableObject
    {
        [Tooltip("Sustained, non-spatial sounds under everything else. Keep them quiet, or leave this empty for silence.")]
        [SerializeField] private AudioEventBase[] beds = Array.Empty<AudioEventBase>();

        [SerializeField] private IntermittentSound[] intermittent = Array.Empty<IntermittentSound>();

        [Tooltip("After an intermittent sound, the chance of a long silence before any other.")]
        [Range(0f, 1f)]
        [SerializeField] private float quietChance = 0.35f;

        [Min(0f)] [SerializeField] private float quietMinSeconds = 20f;

        [Min(0f)] [SerializeField] private float quietMaxSeconds = 60f;

        /// <summary>Creates a profile from code, for tools and tests. Authored profiles are assets.</summary>
        public static AmbienceProfile Create(string name, AudioEventBase[] beds, IntermittentSound[] intermittent, float quietChance = 0f, float quietMin = 0f, float quietMax = 0f)
        {
            AmbienceProfile profile = CreateInstance<AmbienceProfile>();
            profile.name = name;
            profile.beds = beds ?? Array.Empty<AudioEventBase>();
            profile.intermittent = intermittent ?? Array.Empty<IntermittentSound>();
            profile.quietChance = quietChance;
            profile.quietMinSeconds = quietMin;
            profile.quietMaxSeconds = quietMax;
            return profile;
        }

        public IReadOnlyList<AudioEventBase> Beds => beds;
        public IReadOnlyList<IntermittentSound> Intermittent => intermittent;
        public float QuietChance => quietChance;
        public float QuietMinSeconds => quietMinSeconds;
        public float QuietMaxSeconds => quietMaxSeconds;
    }
}
