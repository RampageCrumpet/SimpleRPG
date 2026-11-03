using UnityEngine;

namespace SimpleRPG.Audio
{
    /// <summary>
    /// Puts a sustained sound on an object for as long as the object exists: a torch crackle, a dripping pipe, a projectile's flight hum. The
    /// sound follows the object and ends when the component is disabled or the object destroyed, so no loop can outlive its owner.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class AudioLoopEmitter : MonoBehaviour
    {
        [SerializeField]
        [Tooltip("A sustained event (or a table that picks one).")]
        private AudioEventBase sound;

        [SerializeField]
        [Tooltip("Facts the sound depends on, for a table.")]
        private AudioTag subject;

        [SerializeField]
        [Tooltip("Start when the object becomes active. Otherwise call Begin.")]
        private bool playOnEnable = true;

        [SerializeField]
        [Tooltip("Where on the object the sound comes from, in its local space.")]
        private Vector3 localOffset;

        private AudioHandle handle;

        public AudioEventBase Sound
        {
            get => sound;
            set => sound = value;
        }

        public bool IsPlaying => handle.IsPlaying;

        private void OnEnable()
        {
            if (playOnEnable)
            {
                Begin();
            }
        }

        private void OnDisable() => End();

        /// <summary>Starts the sound if it is not already playing.</summary>
        public void Begin()
        {
            if (sound == null || handle.IsPlaying)
            {
                return;
            }

            AudioContext context = new AudioContext { Subject = subject };
            handle = GameAudio.PlayAttached(sound, context, transform, localOffset);
        }

        /// <summary>Ends the sound.</summary>
        public void End()
        {
            if (handle.IsValid)
            {
                GameAudio.Stop(handle);
                handle = default;
            }
        }
    }
}
