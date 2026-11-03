using UnityEngine;

namespace SimpleRPG.Audio
{
    public enum InteractionCue
    {
        Open,
        Close,
        Activate,
        Deactivate,
        Locked,
        Unlock,
    }

    /// <summary>
    /// The sounds of a world object being used: a door opening and closing, a chest lid, a lever thrown and released. Put it on the object and
    /// fill in only the cues it has; the interactable (or an animation event) calls <see cref="Play"/>.
    /// </summary>
    public sealed class InteractableAudio : MonoBehaviour
    {
        [SerializeField] private AudioEventBase open;
        [SerializeField] private AudioEventBase close;
        [SerializeField] private AudioEventBase activate;
        [SerializeField] private AudioEventBase deactivate;
        [SerializeField] private AudioEventBase locked;
        [SerializeField] private AudioEventBase unlock;

        [SerializeField]
        [Tooltip("What the object is made of, for sound tables that vary by material.")]
        private AudioTag material;

        public AudioEventBase SoundFor(InteractionCue cue)
        {
            switch (cue)
            {
                case InteractionCue.Open: return open;
                case InteractionCue.Close: return close;
                case InteractionCue.Activate: return activate;
                case InteractionCue.Deactivate: return deactivate;
                case InteractionCue.Locked: return locked;
                case InteractionCue.Unlock: return unlock;
                default: return null;
            }
        }

        public AudioHandle Play(InteractionCue cue)
        {
            return GameAudio.Play(SoundFor(cue), new AudioContext { Subject = material }, transform.position);
        }

        /// <summary>Animation event receiver: the Int parameter is the <see cref="InteractionCue"/> value.</summary>
        public void OnInteractionCue(int cue) => Play((InteractionCue)cue);

        /// <summary>
        /// Plays a cue on <paramref name="host"/> if it has an <see cref="InteractableAudio"/>. Safe on anything.
        /// </summary>
        public static void TryPlay(Component host, InteractionCue cue)
        {
            if (host != null && host.TryGetComponent(out InteractableAudio audio))
            {
                audio.Play(cue);
            }
        }
    }
}
