using System.Collections.Generic;
using SimpleRPG.HitDetection;
using SimpleRPG.Abilities;
using UnityEngine;

namespace SimpleRPG.Audio
{
    /// <summary>
    /// Remembers sounds that have just been claimed so the same gameplay moment is never heard twice. A predicted sound (the owner's swing) and
    /// the server's confirmation of it describe one moment and share a key; whichever arrives first plays, the other finds the key taken.
    /// </summary>
    public sealed class AudioEventLedger
    {
        private readonly Dictionary<long, float> claimed = new Dictionary<long, float>();
        private readonly List<long> expired = new List<long>();

        /// <summary>
        /// A key for one moment: who, what kind of thing, and which occurrence.
        /// </summary>
        public static long Key(ulong entity, int kind, int occurrence)
        {
            return unchecked((long)(entity * 1000003UL) ^ ((long)kind << 40) ^ (uint)occurrence);
        }

        /// <summary>
        /// Claims a moment. Returns false if it was already claimed within <paramref name="window"/> seconds, meaning the sound has already played.
        /// </summary>
        public bool TryClaim(long key, float now, float window = 0.25f)
        {
            if (claimed.TryGetValue(key, out float at) && now - at < window)
            {
                return false;
            }

            claimed[key] = now;
            if (claimed.Count > 64)
            {
                expired.Clear();
                foreach (KeyValuePair<long, float> pair in claimed)
                {
                    if (now - pair.Value >= window)
                    {
                        expired.Add(pair.Key);
                    }
                }

                foreach (long old in expired)
                {
                    claimed.Remove(old);
                }
            }

            return true;
        }
    }

    /// <summary>
    /// The sound side of a character's melee. Swing sounds come from the combo step's own data and play at that step's marked moment, joining a
    /// swing already part-way through if the news was late; an owner's predicted swing that the server then refuses or replaces is cancelled
    /// with it. Impact sounds play only from the server's confirmed hits, never from entering an attack state.
    /// </summary>
    [RequireComponent(typeof(MeleeDeliveryBase))]
    public sealed class CombatAudio : MonoBehaviour
    {
        [SerializeField]
        [Tooltip("Material of the attacker's blows when its weapon names none (a monster's claws).")]
        private AudioTag defaultSourceMaterial;

        private readonly AudioEventLedger ledger = new AudioEventLedger();
        private MeleeDeliveryBase delivery;
        private SimpleRPG.Character character;
        private AudioEventBase pendingSwing;
        private float pendingAt;
        private int pendingAttack;

        private void Awake()
        {
            delivery = GetComponent<MeleeDeliveryBase>();
            character = GetComponent<SimpleRPG.Character>();
        }

        private void OnEnable()
        {
            delivery.MeleeAttackStarted += OnAttackStarted;
            delivery.MeleeAttackCancelled += OnAttackCancelled;
            delivery.MeleeHitConfirmed += OnHitConfirmed;
        }

        private void OnDisable()
        {
            delivery.MeleeAttackStarted -= OnAttackStarted;
            delivery.MeleeAttackCancelled -= OnAttackCancelled;
            delivery.MeleeHitConfirmed -= OnHitConfirmed;
            pendingSwing = null;
        }

        private void Update()
        {
            if (pendingSwing != null && Time.time >= pendingAt)
            {
                PlaySwing(pendingSwing, pendingAttack);
                pendingSwing = null;
            }
        }

        /// <summary>
        /// An attack started showing on this machine, <paramref name="secondsIntoAttack"/> seconds ago if the news was late.
        /// </summary>
        public void OnAttackStarted(int attackNumber, float secondsIntoAttack)
        {
            pendingSwing = null;
            ComboStep step = StepFor(attackNumber);
            if (step == null || step.SwingSound == null)
            {
                return;
            }

            float wait = step.SwingSoundTime - secondsIntoAttack;
            if (wait < -0.15f)
            {
                // The marked moment is already well past; a swing sound now would be late and wrong.
                return;
            }

            if (wait <= 0f)
            {
                PlaySwing(step.SwingSound, attackNumber);
                return;
            }

            pendingSwing = step.SwingSound;
            pendingAt = Time.time + wait;
            pendingAttack = attackNumber;
        }

        public void OnAttackCancelled()
        {
            pendingSwing = null;
        }

        private void PlaySwing(AudioEventBase sound, int attackNumber)
        {
            if (!ledger.TryClaim(AudioEventLedger.Key(EntityKey(), 1, attackNumber), Time.time, 0.15f))
            {
                return;
            }

            AudioContext context = new AudioContext
            {
                Source = WeaponMaterial(),
                Intensity = Mathf.Clamp01(StepFor(attackNumber) != null ? StepFor(attackNumber).DamageScalar : 1f),
            };
            GameAudio.PlayAttached(sound, context, transform);
        }

        /// <summary>
        /// The server confirmed a weapon hit on a living target.
        /// </summary>
        public void OnHitConfirmed(MeleeHitCue cue)
        {
            if (!ledger.TryClaim(AudioEventLedger.Key(EntityKey(), 2, cue.AttackNumber * 31 + (cue.Victim != null ? System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(cue.Victim) : 0)), Time.time))
            {
                return;
            }

            WeaponData weapon = character != null ? character.WeaponData : null;
            AudioIdentity target = cue.Victim != null ? cue.Victim.GetComponent<AudioIdentity>() : null;
            AudioRouting routing = AudioRouting.Instance;

            // The most specific statement wins: this weapon's own impact, then the target's, then the shared material table.
            AudioEventBase sound = SelectImpactSound(
                weapon != null ? weapon.ImpactSound : null,
                target != null ? target.ImpactOverride : null,
                routing != null ? routing.DefaultImpactTable : null);

            ComboStep step = StepFor(cue.AttackNumber);
            MovementAudioState worn = cue.Victim != null ? cue.Victim.GetComponent<MovementAudioState>() : null;
            AudioContext context = new AudioContext
            {
                Source = WeaponMaterial(),
                Target = target != null ? target.BodyMaterial : null,
                Subject = target != null ? target.Subject : null,
                Armor = worn != null ? worn.Armor : null,
                Region = AudioContext.RegionOf(cue.Location),
                Intensity = Mathf.Clamp01(step != null ? step.DamageScalar : 1f),
            };

            Vector3 where = cue.Point != Vector3.zero ? cue.Point : (cue.Victim != null ? cue.Victim.transform.position : transform.position);
            GameAudio.Play(sound, context, where);
        }

        /// <summary>
        /// Which impact sound a hit uses: the weapon's own, else the target's, else the shared material table.
        /// </summary>
        public static AudioEventBase SelectImpactSound(AudioEventBase weaponImpact, AudioEventBase targetImpact, AudioEventBase shared)
        {
            return weaponImpact != null ? weaponImpact : targetImpact != null ? targetImpact : shared;
        }

        private AudioTag WeaponMaterial()
        {
            WeaponData weapon = character != null ? character.WeaponData : null;
            return weapon != null && weapon.AudioMaterial != null ? weapon.AudioMaterial : defaultSourceMaterial;
        }

        private ComboStep StepFor(int attackNumber)
        {
            WeaponData weapon = character != null ? character.WeaponData : null;
            ComboDefinition combo = weapon != null ? weapon.Combo : null;
            int index = attackNumber - 1;
            return combo != null && index >= 0 && index < combo.StepCount ? combo.Steps[index] : null;
        }

        private ulong EntityKey() => character != null && character.IsSpawned ? character.NetworkObjectId : (ulong)System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(this);
    }
}
