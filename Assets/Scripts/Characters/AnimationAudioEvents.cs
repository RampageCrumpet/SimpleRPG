using SimpleRPG.Audio;
using UnityEngine;

namespace SimpleRPG
{
    /// <summary>
    /// Plays the audio clip an animation event names, so a clip carries its own sounds and the same component serves every character. Set the
    /// event's function to <c>PlayClip</c> and its Object parameter to the <see cref="AudioClip"/>. The clip plays on this object's
    /// <see cref="AudioSource"/>, whose volume and 3D settings set how loud and how far it is heard.
    /// </summary>
    [RequireComponent(typeof(AudioSource))]
    public sealed class AnimationAudioEvents : MonoBehaviour
    {
        private AudioSource source;

        private void Awake()
        {
            source = GetComponent<AudioSource>();
        }

        /// <summary>
        /// Animation event receiver.
        /// </summary>
        /// <param name="clip">The event's Object parameter; anything that is not an <see cref="AudioClip"/> is ignored.</param>
        public void PlayClip(Object clip)
        {
            if (clip is AudioClip audioClip)
            {
                source.PlayOneShot(audioClip);
            }
        }

        /// <summary>
        /// Animation event receiver for the audio system. Set the event's function to <c>PlayEvent</c> and its Object parameter to an audio event
        /// or table. The sound plays attached to this object through the central audio service, so it gets the event's mixer routing, variation,
        /// limits and 3D settings instead of this object's AudioSource. A missing or unsupported object is ignored.
        /// </summary>
        public void PlayEvent(Object audioEvent)
        {
            if (audioEvent is AudioEventBase request)
            {
                GameAudio.PlayAttached(request, default, transform);
            }
        }
    }
}
