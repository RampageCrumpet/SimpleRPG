using System.Collections.Generic;
using SimpleRPG.Audio;
using UnityEditor;
using UnityEngine;

namespace SimpleRPG.AudioTools
{
    /// <summary>
    /// Plays audio events in the editor without starting a dungeon or entering play mode. It uses the same <see cref="VoicePlanner"/> as the
    /// game, so what is heard (variation, layers, chances) is what the game would do, minus the mixer and 3D placement: previews are 2D.
    /// </summary>
    public static class AudioPreview
    {
        private static readonly List<AudioSource> Sources = new List<AudioSource>();
        private static readonly List<PlannedVoice> Plan = new List<PlannedVoice>();
        private static readonly VoicePlanner Planner = new VoicePlanner();
        private static readonly IAudioRandom Random = new SystemAudioRandom();

        static AudioPreview()
        {
            AssemblyReloadEvents.beforeAssemblyReload += Stop;
            EditorApplication.playModeStateChanged += _ => Stop();
        }

        /// <summary>Starts a preview of a resolved event. Returns the number of layers started.</summary>
        public static int Play(AudioEventDefinition definition)
        {
            Stop();
            if (definition == null)
            {
                return 0;
            }

            Plan.Clear();
            Planner.Plan(definition, Random, Plan);
            foreach (PlannedVoice voice in Plan)
            {
                GameObject host = EditorUtility.CreateGameObjectWithHideFlags("AudioPreview", HideFlags.HideAndDontSave, typeof(AudioSource));
                AudioSource source = host.GetComponent<AudioSource>();
                source.spatialBlend = 0f;
                source.volume = voice.Volume;
                source.pitch = voice.Pitch;
                source.loop = voice.Loop;
                if (voice.Resource != null)
                {
                    source.resource = voice.Resource;
                }
                else
                {
                    source.clip = voice.Clip;
                }

                if (voice.Delay > 0f)
                {
                    source.PlayDelayed(voice.Delay);
                }
                else
                {
                    source.Play();
                }

                Sources.Add(source);
            }

            return Sources.Count;
        }

        /// <summary>Previews whatever a request resolves to in a context.</summary>
        public static AudioEventDefinition Play(AudioEventBase request, in AudioContext context)
        {
            AudioEventDefinition resolved = request != null ? request.Resolve(context) : null;
            Play(resolved);
            return resolved;
        }

        public static bool IsPlaying
        {
            get
            {
                foreach (AudioSource source in Sources)
                {
                    if (source != null && source.isPlaying)
                    {
                        return true;
                    }
                }

                return false;
            }
        }

        public static void Stop()
        {
            foreach (AudioSource source in Sources)
            {
                if (source != null)
                {
                    source.Stop();
                    Object.DestroyImmediate(source.gameObject);
                }
            }

            Sources.Clear();
        }
    }
}
