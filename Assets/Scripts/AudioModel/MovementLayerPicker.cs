using System;
using System.Collections.Generic;
using UnityEngine;

namespace SimpleRPG.Audio
{
    /// <summary>One candidate layer for a step.</summary>
    public struct MovementLayer
    {
        public int Key;
        public AudioEventBase Sound;
        public float Chance;
        public float MinIntensity;
        public float Cooldown;
        public string Group;
    }

    /// <summary>
    /// Decides which equipment layers join a step. Applies each layer's chance, intensity floor and cooldown, lets one layer per group through,
    /// and caps the total, starting from a random layer so the cap does not always favor the same items. Plain C#.
    /// </summary>
    public sealed class MovementLayerPicker
    {
        private readonly Dictionary<int, float> lastPlayed = new Dictionary<int, float>();
        private readonly HashSet<string> usedGroups = new HashSet<string>();

        /// <summary>
        /// Adds the sounds that join this step to <paramref name="output"/>.
        /// </summary>
        public int Pick(IReadOnlyList<MovementLayer> layers, float intensity, float now, IAudioRandom random, int maxLayers, List<AudioEventBase> output)
        {
            usedGroups.Clear();
            int count = layers.Count;
            if (count == 0 || maxLayers <= 0)
            {
                return 0;
            }

            int added = 0;
            int start = Mathf.Min(count - 1, (int)(random.Value() * count));
            for (int n = 0; n < count && added < maxLayers; n++)
            {
                MovementLayer layer = layers[(start + n) % count];
                if (layer.Sound == null || intensity < layer.MinIntensity)
                {
                    continue;
                }

                if (!string.IsNullOrEmpty(layer.Group) && usedGroups.Contains(layer.Group))
                {
                    continue;
                }

                if (lastPlayed.TryGetValue(layer.Key, out float last) && now - last < layer.Cooldown)
                {
                    continue;
                }

                if (layer.Chance < 1f && random.Value() >= layer.Chance)
                {
                    continue;
                }

                lastPlayed[layer.Key] = now;
                if (!string.IsNullOrEmpty(layer.Group))
                {
                    usedGroups.Add(layer.Group);
                }

                output.Add(layer.Sound);
                added++;
            }

            return added;
        }
    }
}
