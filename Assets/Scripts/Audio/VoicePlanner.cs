using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;

namespace SimpleRPG.Audio
{
    /// <summary>
    /// Source of randomness for sound variation. Separate from the gameplay stream on purpose: which clip is heard must never change what happens.
    /// </summary>
    public interface IAudioRandom
    {
        /// <summary>A value in [0, 1).</summary>
        float Value();
    }

    public sealed class SystemAudioRandom : IAudioRandom
    {
        private readonly System.Random random;

        public SystemAudioRandom(int seed) => random = new System.Random(seed);

        public SystemAudioRandom() => random = new System.Random();

        public float Value() => (float)random.NextDouble();
    }

    /// <summary>One sound to start, fully decided: the clip, volume, pitch and delay.</summary>
    public struct PlannedVoice
    {
        public AudioClip Clip;
        public AudioResource Resource;
        public float Volume;
        public float Pitch;
        public float Delay;
        public bool Loop;
        public int Layer;
    }

    /// <summary>
    /// Turns an event into the voices that should start: applies the event and layer chances, picks a clip by weight without repeating the last
    /// one, and applies volume and pitch variation. Remembers the last clip per layer, so one planner serves one listener. Plain C#, no scene.
    /// </summary>
    public sealed class VoicePlanner
    {
        private readonly Dictionary<long, int> lastClip = new Dictionary<long, int>();

        /// <summary>
        /// Adds the voices for one play of <paramref name="definition"/> to <paramref name="output"/>.
        /// </summary>
        /// <returns>How many were added. Zero for silence: the event's chance failed, or nothing in it can play.</returns>
        public int Plan(AudioEventDefinition definition, IAudioRandom random, List<PlannedVoice> output)
        {
            if (definition == null || random == null)
            {
                return 0;
            }

            if (definition.Chance < 1f && random.Value() >= definition.Chance)
            {
                return 0;
            }

            int added = 0;
            AudioLayer[] layers = definition.Layers;
            for (int i = 0; i < layers.Length; i++)
            {
                AudioLayer layer = layers[i];
                if (layer == null || !layer.HasContent)
                {
                    continue;
                }

                if (layer.chance < 1f && random.Value() >= layer.chance)
                {
                    continue;
                }

                AudioClip clip = null;
                if (layer.resource == null)
                {
                    clip = PickClip(definition, i, layer, random);
                    if (clip == null)
                    {
                        continue;
                    }
                }

                float volume = layer.volume * definition.Volume;
                if (layer.volumeVariation > 0f)
                {
                    volume *= 1f + (random.Value() * 2f - 1f) * layer.volumeVariation;
                }

                float pitch = layer.pitch;
                if (layer.pitchVariationSemitones > 0f)
                {
                    pitch *= Mathf.Pow(2f, (random.Value() * 2f - 1f) * layer.pitchVariationSemitones / 12f);
                }

                output.Add(new PlannedVoice
                {
                    Clip = clip,
                    Resource = layer.resource,
                    Volume = Mathf.Clamp01(volume),
                    Pitch = pitch,
                    Delay = layer.delay,
                    Loop = layer.loop && definition.Mode == AudioPlaybackMode.Sustained,
                    Layer = i,
                });
                added++;
            }

            return added;
        }

        private AudioClip PickClip(AudioEventDefinition definition, int layerIndex, AudioLayer layer, IAudioRandom random)
        {
            WeightedClip[] clips = layer.clips;
            long key = ((long)System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(definition) << 8) ^ (uint)layerIndex;
            if (!lastClip.TryGetValue(key, out int last))
            {
                last = -1;
            }

            int usable = 0;
            for (int i = 0; i < clips.Length; i++)
            {
                if (clips[i].clip != null && clips[i].weight > 0f)
                {
                    usable++;
                }
            }

            if (usable == 0)
            {
                return null;
            }

            // The variation just heard is skipped unless it is the only one.
            float total = 0f;
            for (int i = 0; i < clips.Length; i++)
            {
                if (IsCandidate(clips[i], i, last, usable))
                {
                    total += clips[i].weight;
                }
            }

            float roll = random.Value() * total;
            int chosen = -1;
            for (int i = 0; i < clips.Length; i++)
            {
                if (!IsCandidate(clips[i], i, last, usable))
                {
                    continue;
                }

                chosen = i;
                roll -= clips[i].weight;
                if (roll < 0f)
                {
                    break;
                }
            }

            lastClip[key] = chosen;
            return chosen >= 0 ? clips[chosen].clip : null;
        }

        private static bool IsCandidate(WeightedClip entry, int index, int last, int usable)
        {
            return entry.clip != null && entry.weight > 0f && (usable == 1 || index != last);
        }
    }
}
