using System;
using UnityEngine;

namespace SimpleRPG.Audio
{
    /// <summary>
    /// What gameplay code calls to make a sound. Each call takes an event (or a context table) and where it is heard; there are no AudioSources,
    /// clips or lifetimes to manage. Every call is safe whatever is missing: a null event, a missing clip, no mixer or a headless session is
    /// silence, never an exception, so a gameplay action cannot fail because of audio.
    /// </summary>
    public static class GameAudio
    {
        private static bool reportedFailure;

        /// <summary>Plays <paramref name="request"/> at a world position.</summary>
        public static AudioHandle Play(AudioEventBase request, Vector3 position) => Play(request, default, AudioPlacement.At(position));

        /// <summary>Plays <paramref name="request"/> at a world position, chosen by <paramref name="context"/> if it is a table.</summary>
        public static AudioHandle Play(AudioEventBase request, in AudioContext context, Vector3 position) => Play(request, context, AudioPlacement.At(position));

        /// <summary>Plays <paramref name="request"/> following a transform. A sustained sound stops if the transform is destroyed.</summary>
        public static AudioHandle PlayAttached(AudioEventBase request, in AudioContext context, Transform target, Vector3 localOffset = default)
        {
            return target == null ? default : Play(request, context, AudioPlacement.Attached(target, localOffset));
        }

        /// <summary>Plays a sound with no position: UI, ambience beds.</summary>
        public static AudioHandle Play2D(AudioEventBase request) => Play(request, default, AudioPlacement.None);

        public static AudioHandle Play2D(AudioEventBase request, in AudioContext context) => Play(request, context, AudioPlacement.None);

        public static AudioHandle Play(AudioEventBase request, in AudioContext context, in AudioPlacement placement)
        {
            if (request == null)
            {
                return default;
            }

            try
            {
                AudioService service = AudioService.GetOrCreate();
                return service != null ? service.Play(request, context, placement) : default;
            }
            catch (Exception exception)
            {
                if (!reportedFailure)
                {
                    reportedFailure = true;
                    Debug.LogWarning("Audio failed and was skipped; gameplay continues. " + exception);
                }

                return default;
            }
        }

        /// <summary>Ends a sound started by one of the Play calls. Safe with a handle that already ended.</summary>
        public static void Stop(AudioHandle handle, bool fade = true)
        {
            try
            {
                handle.Stop(fade);
            }
            catch (Exception exception)
            {
                Debug.LogWarning("Audio could not stop a sound: " + exception.Message);
            }
        }
    }
}
