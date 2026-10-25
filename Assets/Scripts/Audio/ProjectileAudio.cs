using UnityEngine;

namespace SimpleRPG.Audio
{
    /// <summary>
    /// Gives a projectile its sounds. Release plays when it appears, flight loops while it exists and ends with it (the loop is attached to the
    /// projectile, so it cannot outlive it), and impact plays where it ended unless it simply ran out of life. Runs on every machine from the
    /// projectile's own network spawn and despawn, so no audio message is sent.
    /// </summary>
    [RequireComponent(typeof(Projectile))]
    public sealed class ProjectileAudio : MonoBehaviour
    {
        [SerializeField] private SpellAudioProfile profile;

        private Projectile projectile;
        private AudioHandle flight;
        private Vector3 lastPosition;

        public SpellAudioProfile Profile
        {
            get => profile;
            set => profile = value;
        }

        private void Start()
        {
            if (profile == null)
            {
                return;
            }

            projectile = GetComponent<Projectile>();
            AudioContext context = new AudioContext { Subject = profile.Subject };
            GameAudio.Play(profile.Release, context, transform.position);
            flight = GameAudio.PlayAttached(profile.Flight, context, transform);
        }

        private void LateUpdate() => lastPosition = transform.position;

        private void OnDestroy()
        {
            if (flight.IsValid)
            {
                GameAudio.Stop(flight);
            }

            // Leaving play mode, quitting or unloading a scene destroys everything; none of that is an impact.
            if (profile == null || projectile == null || !Application.isPlaying || !gameObject.scene.isLoaded)
            {
                return;
            }

            bool expired = projectile.Age >= projectile.LifeSpan - 0.1f;
            if (!expired)
            {
                GameAudio.Play(profile.Impact, new AudioContext { Subject = profile.Subject }, lastPosition);
            }
        }
    }
}
