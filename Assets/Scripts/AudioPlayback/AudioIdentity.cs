using SimpleRPG;
using UnityEngine;

namespace SimpleRPG.Audio
{
    /// <summary>
    /// What a character sounds like: what its body is made of (bone, flesh, plate), its voice when hurt and its death sound. Player and monster
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
        [Tooltip("Impact sound for blows landing on this character, used when the attacker's weapon names none. Leave empty to use the shared material table.")]
        private AudioEventBase impactOverride;

        private Character character;

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

        private void Play(AudioEventBase sound)
        {
            GameAudio.PlayAttached(sound, new AudioContext { Subject = subject, Target = bodyMaterial }, transform);
        }
    }
}
