using System.Collections.Generic;
using UnityEngine;

namespace SimpleRPG.Audio
{
    /// <summary>
    /// Runs the current area's ambience: starts its beds, schedules its intermittent sounds around the listener, and changes over when the
    /// listener enters an <see cref="AmbienceZone"/> with another profile. Place one in the scene. Does nothing without an audio device.
    /// </summary>
    public sealed class AmbienceController : MonoBehaviour
    {
        [SerializeField]
        [Tooltip("Used when the listener is in no zone.")]
        private AmbienceProfile defaultProfile;

        [SerializeField, Min(0.1f)] private float zoneCheckSeconds = 0.5f;

        private readonly List<AudioHandle> bedHandles = new List<AudioHandle>();
        private readonly List<int> due = new List<int>(4);
        private IAudioRandom random = new SystemAudioRandom();
        private AmbienceProfile active;
        private AmbienceScheduler scheduler;
        private float nextZoneCheck;
        private Transform listener;

        public AmbienceProfile Active => active;

        public AmbienceProfile DefaultProfile
        {
            get => defaultProfile;
            set => defaultProfile = value;
        }

        public void SetRandom(IAudioRandom source) => random = source ?? random;

        private void OnEnable()
        {
            SetProfile(defaultProfile);
        }

        private void OnDisable()
        {
            SetProfile(null);
        }

        private void Update()
        {
            if (AudioService.IsHeadless)
            {
                return;
            }

            if (Time.unscaledTime >= nextZoneCheck)
            {
                nextZoneCheck = Time.unscaledTime + zoneCheckSeconds;
                Transform ear = Listener();
                AmbienceZone zone = ear != null ? AmbienceZone.At(ear.position) : null;
                AmbienceProfile wanted = zone != null && zone.Profile != null ? zone.Profile : defaultProfile;
                if (wanted != active)
                {
                    SetProfile(wanted);
                }
            }

            Tick(Time.unscaledTime);
        }

        /// <summary>
        /// Runs the intermittent schedule to <paramref name="now"/> and plays what is due.
        /// </summary>
        public void Tick(float now)
        {
            if (scheduler == null)
            {
                return;
            }

            due.Clear();
            scheduler.Tick(now, due);
            Transform ear = Listener();
            for (int i = 0; i < due.Count; i++)
            {
                IntermittentSound entry = active.Intermittent[due[i]];
                float angle = random.Value() * Mathf.PI * 2f;
                float distance = Mathf.Lerp(entry.minDistance, Mathf.Max(entry.minDistance, entry.maxDistance), random.Value());
                Vector3 origin = ear != null ? ear.position : transform.position;
                GameAudio.Play(entry.sound, origin + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * distance);
            }
        }

        /// <summary>
        /// Changes to another profile: the old beds fade out, the new ones start, and the intermittent schedule restarts.
        /// </summary>
        public void SetProfile(AmbienceProfile profile)
        {
            foreach (AudioHandle handle in bedHandles)
            {
                GameAudio.Stop(handle);
            }

            bedHandles.Clear();
            active = profile;
            scheduler = null;
            if (profile == null)
            {
                return;
            }

            foreach (AudioEventBase bed in profile.Beds)
            {
                AudioHandle handle = GameAudio.Play2D(bed);
                if (handle.IsValid)
                {
                    bedHandles.Add(handle);
                }
            }

            scheduler = new AmbienceScheduler(profile, random, Time.unscaledTime);
        }

        private Transform Listener()
        {
            if (listener == null)
            {
                AudioListener found = FindFirstObjectByType<AudioListener>();
                listener = found != null ? found.transform : null;
            }

            return listener;
        }
    }
}
