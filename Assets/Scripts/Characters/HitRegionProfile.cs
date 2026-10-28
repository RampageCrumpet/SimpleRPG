using System;
using UnityEngine;

namespace SimpleRPG.HitDetection
{
    /// <summary>
    /// What a blow to each body location is worth against one kind of character. An asset, so a skeleton and a player (or a spider) carry
    /// different tables while combat resolution stays the same. A location the profile does not list takes the default factor of 1.
    /// Immutable at runtime.
    /// </summary>
    [CreateAssetMenu(menuName = "SimpleRPG/Hit Region Profile", fileName = "HitRegionProfile")]
    public sealed class HitRegionProfile : ScriptableObject
    {
        [Serializable]
        public struct Entry
        {
            public BodyLocation location;

            [Min(0f)]
            public float multiplier;

            public Entry(BodyLocation location, float multiplier)
            {
                this.location = location;
                this.multiplier = multiplier;
            }
        }

        [SerializeField]
        [Tooltip("The factor a weapon blow to each body location is multiplied by. A location not listed here is 1.")]
        private Entry[] multipliers = Array.Empty<Entry>();

        /// <summary>
        /// The factor for a blow to <paramref name="location"/>. If the table lists it twice, the first entry wins.
        /// </summary>
        public float MultiplierFor(int location)
        {
            foreach (Entry entry in multipliers)
            {
                if ((int)entry.location == location)
                {
                    return Mathf.Max(0f, entry.multiplier);
                }
            }

            return 1f;
        }

        /// <summary>
        /// Replaces the table. For tools and tests that build a profile in code; authored profiles are edited in the inspector.
        /// </summary>
        public void Configure(params Entry[] entries)
        {
            multipliers = entries ?? Array.Empty<Entry>();
        }
    }
}
