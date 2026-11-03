using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace SimpleRPG.Audio
{
    /// <summary>
    /// Where a sound is heard from.
    /// </summary>
    public struct AudioPlacement
    {
        public Vector3 Position;

        /// <summary>If set, the sound follows this transform. If it is destroyed, a sustained sound stops.</summary>
        public Transform Follow;

        public Vector3 LocalOffset;

        public static AudioPlacement At(Vector3 position) => new AudioPlacement { Position = position };

        public static AudioPlacement Attached(Transform target, Vector3 localOffset = default)
        {
            return new AudioPlacement { Follow = target, LocalOffset = localOffset, Position = target != null ? target.TransformPoint(localOffset) : localOffset };
        }

        /// <summary>For non-spatial sounds, where position means nothing.</summary>
        public static AudioPlacement None => default;
    }

    /// <summary>
    /// A ticket for one playing event, used to stop a sustained sound. A handle that no longer refers to anything (the sound ended, was cut off, or
    /// never started) is harmless: asking it to stop does nothing.
    /// </summary>
    public readonly struct AudioHandle
    {
        internal AudioHandle(uint id) => Id = id;

        internal uint Id { get; }

        public bool IsValid => Id != 0;

        /// <summary>True while the sound it names is still playing.</summary>
        public bool IsPlaying => IsValid && AudioService.Current != null && AudioService.Current.IsPlaying(this);

        public void Stop(bool fade = true)
        {
            if (IsValid && AudioService.Current != null)
            {
                AudioService.Current.Stop(this, fade);
            }
        }
    }

    /// <summary>
    /// The one place sounds are started and ended. It owns a pool of voices (an AudioSource each, made once and reused), applies the event's
    /// spatial settings and mixer category, enforces per-event and global limits, follows moving transforms and releases every voice when its
    /// sound ends. Gameplay talks to <see cref="GameAudio"/>, not to this.
    /// <para>
    /// A session with no graphics device (a dedicated server) never creates the service: every request is a no-op there.
    /// </para>
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class AudioService : MonoBehaviour
    {
        private sealed class Voice
        {
            public GameObject Object;
            public Transform Transform;
            public AudioSource Source;
            public float StartAt;
            public float EndAt;
            public float Volume;
            public bool Loops;
            public bool KnownLength;
        }

        private sealed class Instance
        {
            public uint Id;
            public AudioEventDefinition Definition;
            public Transform Follow;
            public bool Attached;
            public Vector3 Offset;
            public float StartedAt;
            public float FadeStart;
            public float FadeLength;
            public bool Stopping;
            public readonly List<Voice> Voices = new List<Voice>(4);
        }

        private static AudioService current;
        private static bool shuttingDown;

        private readonly List<Voice> freeVoices = new List<Voice>();
        private readonly List<Instance> active = new List<Instance>();
        private readonly Dictionary<uint, Instance> byId = new Dictionary<uint, Instance>();
        private readonly Stack<Instance> freeInstances = new Stack<Instance>();
        private readonly Dictionary<AudioEventDefinition, float> lastPlayed = new Dictionary<AudioEventDefinition, float>();
        private readonly List<PlannedVoice> plan = new List<PlannedVoice>(8);
        private readonly VoicePlanner planner = new VoicePlanner();

        private AudioListener listener;
        private float listenerSearchedAt = float.NegativeInfinity;
        private IAudioRandom random = new SystemAudioRandom();
        private uint lastId;
        private int voicesInUse;
        private int voicesCreated;
        private AudioRouting routing;
        private int maxVoices = 40;
        private Func<float> clock = () => Time.unscaledTime;

        /// <summary>
        /// Forces headless on or off. Null means: headless exactly when there is no graphics device.
        /// </summary>
        public static bool? HeadlessOverride { get; set; }

        /// <summary>True in a session that cannot output audio: a dedicated server or a run with no graphics device.</summary>
        public static bool IsHeadless => HeadlessOverride ?? (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null);

        /// <summary>
        /// The running service, or null if there is none yet. Does not create one.
        /// </summary>
        public static AudioService Current => current;

        /// <summary>
        /// The service, created on first use. Null when headless or while the application is closing.
        /// </summary>
        public static AudioService GetOrCreate()
        {
            if (current != null)
            {
                return current;
            }

            if (shuttingDown || IsHeadless)
            {
                return null;
            }

            GameObject host = new GameObject("AudioService");
            DontDestroyOnLoad(host);
            return host.AddComponent<AudioService>();
        }

        /// <summary>Events playing now.</summary>
        public int ActiveCount => active.Count;

        /// <summary>Voices in use now (an event with three layers uses three).</summary>
        public int VoicesInUse => voicesInUse;

        /// <summary>Voices that exist, in use or waiting. Never exceeds the configured maximum.</summary>
        public int VoicesCreated => voicesCreated;

        public int MaxVoices => maxVoices;

        /// <summary>Replaces the random source and clock. For tests.</summary>
        public void SetTestHooks(IAudioRandom randomSource, Func<float> timeSource)
        {
            if (randomSource != null) random = randomSource;
            if (timeSource != null) clock = timeSource;
        }

        public void Configure(AudioRouting audioRouting, int voiceLimit)
        {
            routing = audioRouting;
            maxVoices = Mathf.Max(4, voiceLimit);

            // A smaller limit than the pool was warmed for: drop the spare voices that are over it.
            while (voicesCreated > maxVoices && freeVoices.Count > 0)
            {
                Voice spare = freeVoices[freeVoices.Count - 1];
                freeVoices.RemoveAt(freeVoices.Count - 1);
                Destroy(spare.Object);
                voicesCreated--;
            }
        }

        private void Awake()
        {
            if (current != null && current != this)
            {
                Destroy(this);
                return;
            }

            current = this;
            routing = AudioRouting.Instance;
            if (routing != null)
            {
                maxVoices = routing.MaxVoices;
                int prewarm = Mathf.Min(routing.PrewarmVoices, maxVoices);
                for (int i = 0; i < prewarm; i++)
                {
                    freeVoices.Add(CreateVoice());
                }
            }
        }

        private void OnDestroy()
        {
            if (current == this)
            {
                current = null;
            }
        }

        private void OnApplicationQuit()
        {
            shuttingDown = true;
        }

        // Domain reload may be off in the editor, so the flag has to be cleared on entering play mode.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            shuttingDown = false;
            current = null;
        }

        private void Update() => Tick(clock());

        /// <summary>
        /// Advances every playing sound to time <paramref name="now"/>: follows transforms, runs fades and releases what has ended.
        /// </summary>
        public void Tick(float now)
        {
            for (int i = active.Count - 1; i >= 0; i--)
            {
                Instance instance = active[i];
                UpdateFollow(instance);

                if (instance.Attached && instance.Follow == null && instance.Definition.Mode == AudioPlaybackMode.Sustained && !instance.Stopping)
                {
                    // Attached to a transform that was destroyed: a loop with no owner has no one to stop it.
                    BeginStop(instance, now, true);
                }

                if (instance.Stopping)
                {
                    float progress = instance.FadeLength <= 0f ? 1f : (now - instance.FadeStart) / instance.FadeLength;
                    if (progress >= 1f)
                    {
                        Release(instance, i);
                        continue;
                    }

                    foreach (Voice voice in instance.Voices)
                    {
                        voice.Source.volume = voice.Volume * (1f - progress);
                    }

                    continue;
                }

                if (HasEnded(instance, now))
                {
                    Release(instance, i);
                }
            }
        }

        private static void UpdateFollow(Instance instance)
        {
            if (instance.Follow == null)
            {
                return;
            }

            Vector3 position = instance.Follow.TransformPoint(instance.Offset);
            foreach (Voice voice in instance.Voices)
            {
                voice.Transform.position = position;
            }
        }

        private static bool HasEnded(Instance instance, float now)
        {
            foreach (Voice voice in instance.Voices)
            {
                if (voice.Loops)
                {
                    return false;
                }

                if (voice.KnownLength ? now < voice.EndAt : (now < voice.StartAt + 0.1f || voice.Source.isPlaying))
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// Starts an event. Returns an invalid handle when nothing was started: silence by context, no content, a limit, or headless.
        /// </summary>
        public AudioHandle Play(AudioEventBase request, in AudioContext context, in AudioPlacement placement)
        {
            if (request == null)
            {
                return default;
            }

            AudioEventDefinition definition = request.Resolve(context);
            return definition == null ? default : Play(definition, placement);
        }

        /// <summary>
        /// Starts one resolved event.
        /// </summary>
        public AudioHandle Play(AudioEventDefinition definition, in AudioPlacement placement)
        {
            if (definition == null)
            {
                return default;
            }

            float now = clock();
            if (definition.MinInterval > 0f && lastPlayed.TryGetValue(definition, out float last) && now - last < definition.MinInterval)
            {
                return default;
            }

            // With no listener nothing can be heard, and Unity would log a complaint for every sound.
            if (!HasListener(now))
            {
                return default;
            }

            // An event with no clip is silent by design (placeholders); the validator reports it, the game does not complain.
            plan.Clear();
            if (planner.Plan(definition, random, plan) == 0)
            {
                return default;
            }

            if (!MakeRoomForEvent(definition, plan.Count, now))
            {
                return default;
            }

            lastPlayed[definition] = now;

            Instance instance = freeInstances.Count > 0 ? freeInstances.Pop() : new Instance();
            instance.Id = ++lastId;
            instance.Definition = definition;
            instance.Follow = placement.Follow;
            instance.Attached = placement.Follow != null;
            instance.Offset = placement.LocalOffset;
            instance.StartedAt = now;
            instance.Stopping = false;
            instance.Voices.Clear();

            Vector3 position = placement.Follow != null ? placement.Follow.TransformPoint(placement.LocalOffset) : placement.Position;
            for (int i = 0; i < plan.Count; i++)
            {
                Voice voice = TakeVoice();
                if (voice == null)
                {
                    break;
                }

                StartVoice(voice, definition, plan[i], position, now);
                instance.Voices.Add(voice);
            }

            if (instance.Voices.Count == 0)
            {
                instance.Definition = null;
                freeInstances.Push(instance);
                return default;
            }

            active.Add(instance);
            byId[instance.Id] = instance;
            return new AudioHandle(instance.Id);
        }

        // Looks for a listener at most once a second, so a scene change or a destroyed camera is noticed without a search per sound.
        private bool HasListener(float now)
        {
            if (listener != null && listener.isActiveAndEnabled)
            {
                return true;
            }

            if (now - listenerSearchedAt >= 1f)
            {
                listenerSearchedAt = now;
                listener = FindAnyObjectByType<AudioListener>();
            }

            return listener != null && listener.isActiveAndEnabled;
        }

        public bool IsPlaying(AudioHandle handle) => handle.IsValid && byId.TryGetValue(handle.Id, out Instance instance) && !instance.Stopping;

        /// <summary>
        /// Ends a playing event. A sustained one fades out over its definition's fade time unless <paramref name="fade"/> is false.
        /// </summary>
        public void Stop(AudioHandle handle, bool fade = true)
        {
            if (handle.IsValid && byId.TryGetValue(handle.Id, out Instance instance) && !instance.Stopping)
            {
                BeginStop(instance, clock(), fade);
                if (instance.FadeLength <= 0f)
                {
                    int index = active.IndexOf(instance);
                    if (index >= 0)
                    {
                        Release(instance, index);
                    }
                }
            }
        }

        /// <summary>Ends everything now. For scene changes and shutdown.</summary>
        public void StopAll()
        {
            for (int i = active.Count - 1; i >= 0; i--)
            {
                Release(active[i], i);
            }
        }

        private void BeginStop(Instance instance, float now, bool fade)
        {
            instance.Stopping = true;
            instance.FadeStart = now;
            instance.FadeLength = fade && instance.Definition.Mode == AudioPlaybackMode.Sustained ? instance.Definition.FadeOutSeconds : 0f;
        }

        private bool MakeRoomForEvent(AudioEventDefinition definition, int voicesNeeded, float now)
        {
            if (definition.MaxConcurrent > 0)
            {
                int count = 0;
                Instance oldest = null;
                foreach (Instance instance in active)
                {
                    if (instance.Definition == definition && !instance.Stopping)
                    {
                        count++;
                        if (oldest == null || instance.StartedAt < oldest.StartedAt)
                        {
                            oldest = instance;
                        }
                    }
                }

                if (count >= definition.MaxConcurrent)
                {
                    if (definition.WhenFull == AudioLimitBehavior.DropNew)
                    {
                        return false;
                    }

                    ReleaseNow(oldest);
                }
            }

            // Global limit: make room by cutting off the least important, oldest sound, but never one that matters more than this.
            while (voicesInUse + voicesNeeded > maxVoices)
            {
                Instance victim = null;
                foreach (Instance instance in active)
                {
                    if (victim == null
                        || instance.Definition.Importance < victim.Definition.Importance
                        || (instance.Definition.Importance == victim.Definition.Importance && instance.StartedAt < victim.StartedAt))
                    {
                        victim = instance;
                    }
                }

                if (victim == null || victim.Definition.Importance > definition.Importance)
                {
                    return false;
                }

                ReleaseNow(victim);
            }

            return true;
        }

        private void ReleaseNow(Instance instance)
        {
            int index = active.IndexOf(instance);
            if (index >= 0)
            {
                Release(instance, index);
            }
        }

        private Voice TakeVoice()
        {
            Voice voice;
            if (freeVoices.Count > 0)
            {
                voice = freeVoices[freeVoices.Count - 1];
                freeVoices.RemoveAt(freeVoices.Count - 1);
            }
            else if (voicesCreated < maxVoices)
            {
                voice = CreateVoice();
            }
            else
            {
                return null;
            }

            voicesInUse++;
            return voice;
        }

        private Voice CreateVoice()
        {
            GameObject host = new GameObject("AudioVoice");
            host.transform.SetParent(transform, false);
            host.hideFlags = HideFlags.DontSave;
            AudioSource source = host.AddComponent<AudioSource>();
            source.playOnAwake = false;
            host.SetActive(false);
            voicesCreated++;
            return new Voice { Object = host, Transform = host.transform, Source = source };
        }

        private void StartVoice(Voice voice, AudioEventDefinition definition, in PlannedVoice planned, Vector3 position, float now)
        {
            voice.Object.SetActive(true);
            voice.Transform.position = position;
            AudioSource source = voice.Source;

            source.Stop();
            source.clip = null;
            source.resource = null;
            if (planned.Resource != null)
            {
                source.resource = planned.Resource;
            }
            else
            {
                source.clip = planned.Clip;
            }

            source.loop = planned.Loop;
            source.volume = planned.Volume;
            source.pitch = planned.Pitch;
            source.outputAudioMixerGroup = routing != null ? routing.GroupFor(definition.Category) : null;
            source.dopplerLevel = definition.DopplerLevel;
            source.priority = Mathf.Clamp(255 - Mathf.RoundToInt(definition.Importance * 2.55f), 0, 255);

            if (definition.Spatial == AudioSpatialMode.World3D)
            {
                source.spatialBlend = 1f;
                source.minDistance = definition.MinDistance;
                source.maxDistance = definition.MaxDistance;
                source.rolloffMode = definition.Rolloff;
                if (definition.Rolloff == AudioRolloffMode.Custom)
                {
                    source.SetCustomCurve(AudioSourceCurveType.CustomRolloff, definition.CustomRolloff);
                }
            }
            else
            {
                source.spatialBlend = 0f;
            }

            voice.Volume = planned.Volume;
            voice.Loops = planned.Loop;
            voice.StartAt = now + planned.Delay;
            voice.KnownLength = planned.Resource == null && planned.Clip != null;
            voice.EndAt = voice.KnownLength ? voice.StartAt + planned.Clip.length / Mathf.Max(0.01f, Mathf.Abs(planned.Pitch)) + 0.05f : 0f;

            if (planned.Delay > 0f)
            {
                source.PlayDelayed(planned.Delay);
            }
            else
            {
                source.Play();
            }
        }

        private void Release(Instance instance, int activeIndex)
        {
            foreach (Voice voice in instance.Voices)
            {
                voice.Source.Stop();
                voice.Source.clip = null;
                voice.Source.resource = null;
                voice.Object.SetActive(false);
                freeVoices.Add(voice);
                voicesInUse--;
            }

            instance.Voices.Clear();
            byId.Remove(instance.Id);
            active.RemoveAt(activeIndex);
            instance.Definition = null;
            instance.Follow = null;
            freeInstances.Push(instance);
        }
    }
}
