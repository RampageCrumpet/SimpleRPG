using SimpleRPG;
using UnityEngine;

namespace SimpleRPG.Audio
{
    /// <summary>
    /// What a character sounds like: what its body is made of (bone, flesh, plate), its voice when hurt, its death sound and the sound its body
    /// makes landing on the ground (the collapse, a separate moment from dying: the animation calls <see cref="OnCollapse"/> when it lands). Player and monster
    /// carry the same component with different assets, so a new creature gets its sounds by configuration. Hurt and death follow the
    /// replicated health and life state, so they play on every machine without any audio message.
    /// </summary>
    [RequireComponent(typeof(Character))]
    public sealed class AudioIdentity : MonoBehaviour
    {
        [SerializeField]
        [Tooltip("What a blow to this body hits: bone, flesh, plate. Impact tables use it.")]
        private AudioTag bodyMaterial;

        [SerializeField]
        [Tooltip("Which creature this is, for tables that vary by creature.")]
        private AudioTag subject;

        [SerializeField]
        [Tooltip("Plays when this character is hurt and survives.")]
        private AudioEventBase hurt;

        [SerializeField]
        [Tooltip("Plays when this character dies.")]
        private AudioEventBase death;

        [SerializeField]
        [Tooltip("Plays when the dead body lands on the ground. Resolved against the ground it lands on, the creature and what its body is made of. Call OnCollapse from the death animation (an animation event) at the moment it lands.")]
        private AudioEventBase collapse;

        [SerializeField]
        [Tooltip("What counts as ground under the body when it collapses.")]
        private LayerMask groundMask = ~0;

        [SerializeField]
        [Tooltip("Impact sound for blows landing on this character, used when the attacker's weapon names none. Leave empty to use the shared material table.")]
        private AudioEventBase impactOverride;

        private Character character;
        private readonly IAudioRandom random = new SystemAudioRandom();

        public AudioTag BodyMaterial => bodyMaterial;
        public AudioTag Subject => subject;
        public AudioEventBase ImpactOverride => impactOverride;

        private void Awake() => character = GetComponent<Character>();

        private void OnEnable()
        {
            character.OnTakeDamage += HandleHurt;
            character.OnDeath += HandleDeath;
        }

        private void OnDisable()
        {
            if (character != null)
            {
                character.OnTakeDamage -= HandleHurt;
                character.OnDeath -= HandleDeath;
            }
        }

        private void HandleHurt()
        {
            // A killing blow is heard as a death, not as a hurt cry first.
            if (character.Health > 0 && character.IsAlive)
            {
                Play(hurt);
            }
        }

        private void HandleDeath() => Play(death);

        /// <summary>
        /// The dead body lands. Meant for an animation event on the death clip, at the frame it hits the ground, so the sound follows the animation
        /// whatever its length. Like every sound it is heard on each machine from its own animation and is never sent over the network.
        /// </summary>
        public void OnCollapse()
        {
            SurfaceSample sample = SurfaceQuery.Below(transform.position, 1f, groundMask, transform);
            Collapse(transform.position, sample);
        }

        /// <summary>Plays the collapse on a known surface. Public so a test or a scripted fall can supply the surface.</summary>
        public void Collapse(Vector3 position, in SurfaceSample sample)
        {
            AudioContext context = new AudioContext { Subject = subject, Target = bodyMaterial, Surface = sample.Surface };
            GameAudio.Play(collapse, context, position);
            if (sample.Overlay != null)
            {
                GameAudio.Play(collapse, context.With(AudioFacet.Surface, sample.Overlay), position);
            }

            // A bridge may creak under a falling body as it does under a footstep.
            SurfaceQuery.PlayContactSounds(sample.BaseSource, AudioContactKind.Impact, context, position, random);
            SurfaceQuery.PlayContactSounds(sample.OverlaySource, AudioContactKind.Impact, context, position, random);
        }

        private void Play(AudioEventBase sound)
        {
            GameAudio.PlayAttached(sound, new AudioContext { Subject = subject, Target = bodyMaterial }, transform);
        }
    }
}
