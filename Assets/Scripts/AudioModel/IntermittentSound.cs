using System;
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
}
