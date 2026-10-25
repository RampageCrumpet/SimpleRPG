using System;
using UnityEngine;
using UnityEngine.Audio;

namespace SimpleRPG.Audio
{
    /// <summary>The mixer bus a sound plays on.</summary>
    public enum AudioCategory
    {
        Combat,
        Characters,
        Magic,
        World,
        Ambience,
        UI,
    }

    public enum AudioPlaybackMode
    {
        /// <summary>Plays once and ends by itself.</summary>
        OneShot,
        /// <summary>Loops until stopped. The caller keeps the handle and stops it when the owner ends.</summary>
        Sustained,
    }

    public enum AudioSpatialMode
    {
        /// <summary>Positioned in the world and attenuated by distance.</summary>
        World3D,
        /// <summary>Heard the same everywhere: UI and ambience beds.</summary>
        NonSpatial,
    }

    /// <summary>What happens when an event is already playing as many times as it may.</summary>
    public enum AudioLimitBehavior
    {
        /// <summary>The oldest copy is cut off to make room.</summary>
        StopOldest,
        /// <summary>The new request is ignored.</summary>
        DropNew,
    }

    [Serializable]
    public struct WeightedClip
    {
        public AudioClip clip;

        [Min(0f)]
        public float weight;

        public WeightedClip(AudioClip clip, float weight = 1f)
        {
            this.clip = clip;
            this.weight = weight;
        }
    }

    /// <summary>
    /// One sound within an event. Several layers play together (a swing's whoosh and its cloth rustle). A layer picks one clip from its list by
    /// weight, or hands the choice to a Unity Audio Random Container.
    /// </summary>
    [Serializable]
    public sealed class AudioLayer
    {
        [Tooltip("Variations. One is picked by weight each time, never the same one twice running when there is a choice.")]
        public WeightedClip[] clips = Array.Empty<WeightedClip>();

        [Tooltip("Optional Unity Audio Random Container (or other audio resource). When set it replaces Clips: Unity does the random choice, and its own volume and pitch settings apply.")]
        public AudioResource resource;

        [Range(0f, 1f)]
        public float volume = 1f;

        [Tooltip("Random volume change, as a fraction either way. 0.1 is plus or minus 10 percent.")]
        [Range(0f, 1f)]
        public float volumeVariation;

        [Tooltip("Pitch as a ratio. 1 is unchanged.")]
        [Range(0.1f, 3f)]
        public float pitch = 1f;

        [Tooltip("Random pitch change in semitones either way.")]
        [Range(0f, 12f)]
        public float pitchVariationSemitones;

        [Tooltip("Seconds after the event starts before this layer begins.")]
        [Min(0f)]
        public float delay;

        [Tooltip("Chance this layer plays at all, so an occasional layer (a bone rattle) is not heard every time.")]
        [Range(0f, 1f)]
        public float chance = 1f;

        [Tooltip("Loop this layer. Sustained events loop every layer that has this set.")]
        public bool loop;

        public bool HasContent
        {
            get
            {
                if (resource != null)
                {
                    return true;
                }

                if (clips != null)
                {
                    foreach (WeightedClip entry in clips)
                    {
                        if (entry.clip != null && entry.weight > 0f)
                        {
                            return true;
                        }
                    }
                }

                return false;
            }
        }
    }

    /// <summary>
    /// One authored sound: what plays (layers and variations), how it is heard (spatial settings, mixer category) and how many may play at once.
    /// Immutable at runtime: variation history and playback state live in the <see cref="AudioService"/>.
    /// </summary>
    [CreateAssetMenu(fileName = "AudioEvent", menuName = "Audio/Event")]
    public sealed class AudioEventDefinition : AudioEventBase
    {
        [SerializeField] private AudioCategory category = AudioCategory.World;

        [SerializeField] private AudioPlaybackMode mode = AudioPlaybackMode.OneShot;

        [SerializeField] private AudioLayer[] layers = { new AudioLayer() };

        [Range(0f, 1f)]
        [SerializeField] private float volume = 1f;

        [Tooltip("Chance the whole event plays when requested.")]
        [Range(0f, 1f)]
        [SerializeField] private float chance = 1f;

        [Header("Spatial")]
        [SerializeField] private AudioSpatialMode spatial = AudioSpatialMode.World3D;

        [Tooltip("Inside this distance the sound is at full volume.")]
        [Min(0.01f)]
        [SerializeField] private float minDistance = 1f;

        [Tooltip("Beyond this distance the sound is no longer heard (linear) or stops getting quieter (logarithmic).")]
        [Min(0.02f)]
        [SerializeField] private float maxDistance = 25f;

        [SerializeField] private AudioRolloffMode rolloff = AudioRolloffMode.Logarithmic;

        [Tooltip("Used when Rolloff is Custom: volume (0 to 1) against distance as a fraction of Max Distance.")]
        [SerializeField] private AnimationCurve customRolloff = AnimationCurve.Linear(0f, 1f, 1f, 0f);

        [Range(0f, 5f)]
        [SerializeField] private float dopplerLevel;

        [Header("Limits")]
        [Tooltip("When the mixer is full, a more important sound takes the place of a less important one.")]
        [Range(0, 100)]
        [SerializeField] private int importance = 50;

        [Tooltip("The most copies of this event that play at once. 0 for no limit.")]
        [Min(0)]
        [SerializeField] private int maxConcurrent = 4;

        [SerializeField] private AudioLimitBehavior whenFull = AudioLimitBehavior.StopOldest;

        [Tooltip("Shortest time between two plays of this event, in seconds. Stops a burst of identical requests from stacking.")]
        [Min(0f)]
        [SerializeField] private float minInterval;

        [Tooltip("Sustained events fade out over this long when stopped.")]
        [Min(0f)]
        [SerializeField] private float fadeOutSeconds = 0.1f;

        public AudioCategory Category => category;
        public AudioPlaybackMode Mode => mode;
        public AudioLayer[] Layers => layers ?? Array.Empty<AudioLayer>();
        public float Volume => volume;
        public float Chance => chance;
        public AudioSpatialMode Spatial => spatial;
        public float MinDistance => minDistance;
        public float MaxDistance => Mathf.Max(maxDistance, minDistance + 0.01f);
        public AudioRolloffMode Rolloff => rolloff;
        public AnimationCurve CustomRolloff => customRolloff;
        public float DopplerLevel => dopplerLevel;
        public int Importance => importance;
        public int MaxConcurrent => maxConcurrent;
        public AudioLimitBehavior WhenFull => whenFull;
        public float MinInterval => minInterval;
        public float FadeOutSeconds => fadeOutSeconds;

        /// <summary>True when at least one layer has something to play.</summary>
        public bool HasContent
        {
            get
            {
                foreach (AudioLayer layer in Layers)
                {
                    if (layer != null && layer.HasContent)
                    {
                        return true;
                    }
                }

                return false;
            }
        }

        internal override AudioEventDefinition ResolveAt(in AudioContext context, int depth) => this;

        /// <summary>
        /// Creates an event from code, for tools and tests. Authored events are assets.
        /// </summary>
        public static AudioEventDefinition Create(
            string name,
            AudioCategory category,
            AudioPlaybackMode mode,
            AudioLayer[] layers,
            AudioSpatialMode spatial = AudioSpatialMode.World3D,
            int maxConcurrent = 4,
            int importance = 50,
            AudioLimitBehavior whenFull = AudioLimitBehavior.StopOldest,
            float minInterval = 0f,
            float chance = 1f,
            float fadeOutSeconds = 0.1f)
        {
            AudioEventDefinition definition = CreateInstance<AudioEventDefinition>();
            definition.name = name;
            definition.category = category;
            definition.mode = mode;
            definition.layers = layers;
            definition.spatial = spatial;
            definition.maxConcurrent = maxConcurrent;
            definition.importance = importance;
            definition.whenFull = whenFull;
            definition.minInterval = minInterval;
            definition.chance = chance;
            definition.fadeOutSeconds = fadeOutSeconds;
            return definition;
        }

        /// <summary>A layer playing one clip, for tools and tests.</summary>
        public static AudioLayer Layer(AudioClip clip, bool loop = false, float volume = 1f)
        {
            return new AudioLayer { clips = new[] { new WeightedClip(clip) }, loop = loop, volume = volume };
        }

        public override void Validate(AudioValidationReport report)
        {
            string who = name;
            if (Layers.Length == 0)
            {
                report.Errors.Add(who + ": has no layers.");
            }

            for (int i = 0; i < Layers.Length; i++)
            {
                AudioLayer layer = Layers[i];
                if (layer == null)
                {
                    report.Errors.Add(who + ": layer " + (i + 1) + " is empty.");
                    continue;
                }

                if (!layer.HasContent)
                {
                    report.Warnings.Add(who + ": layer " + (i + 1) + " has no clip, so it is silent.");
                }
                else if (layer.clips != null && layer.resource == null)
                {
                    foreach (WeightedClip entry in layer.clips)
                    {
                        if (entry.clip == null)
                        {
                            report.Warnings.Add(who + ": layer " + (i + 1) + " has a variation slot with no clip.");
                        }
                    }
                }

                if (mode == AudioPlaybackMode.Sustained && !layer.loop)
                {
                    report.Warnings.Add(who + ": layer " + (i + 1) + " is in a sustained event but does not loop, so it ends by itself.");
                }

                if (mode == AudioPlaybackMode.OneShot && layer.loop)
                {
                    report.Errors.Add(who + ": layer " + (i + 1) + " loops in a one-shot event, which would never end. Make the event sustained.");
                }
            }

            if (maxDistance <= minDistance)
            {
                report.Errors.Add(who + ": Max Distance must be larger than Min Distance.");
            }

            if (spatial == AudioSpatialMode.NonSpatial && mode == AudioPlaybackMode.OneShot && category == AudioCategory.Ambience)
            {
                report.Warnings.Add(who + ": a one-shot ambience sound without 3D position will be heard at one volume everywhere.");
            }
        }
    }
}
