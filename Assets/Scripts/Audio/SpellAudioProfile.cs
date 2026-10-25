using UnityEngine;

namespace SimpleRPG.Audio
{
    /// <summary>
    /// The sounds of one spell or projectile through its life: gathering the cast, letting it go, its flight, what it does on arrival and any
    /// effect that keeps going. Assign one to a projectile prefab (through <see cref="ProjectileAudio"/>) or call its events from an ability.
    /// </summary>
    [CreateAssetMenu(fileName = "SpellAudioProfile", menuName = "Audio/Spell Profile")]
    public sealed class SpellAudioProfile : ScriptableObject
    {
        [Tooltip("The caster gathering the spell. Usually sustained, so it can be cut off if the cast is interrupted.")]
        [SerializeField] private AudioEventBase castPrepare;

        [Tooltip("The moment the spell leaves the caster.")]
        [SerializeField] private AudioEventBase release;

        [Tooltip("Plays along the whole flight. Must be sustained; it ends when the projectile does.")]
        [SerializeField] private AudioEventBase flight;

        [Tooltip("The arrival: a hit or a wall.")]
        [SerializeField] private AudioEventBase impact;

        [Tooltip("A lingering effect, such as a burning ground. Sustained, owned by whatever creates the effect.")]
        [SerializeField] private AudioEventBase periodic;

        [Tooltip("Which spell this is, for tables that vary by spell.")]
        [SerializeField] private AudioTag subject;

        public AudioEventBase CastPrepare => castPrepare;
        public AudioEventBase Release => release;
        public AudioEventBase Flight => flight;
        public AudioEventBase Impact => impact;
        public AudioEventBase Periodic => periodic;
        public AudioTag Subject => subject;
    }
}
