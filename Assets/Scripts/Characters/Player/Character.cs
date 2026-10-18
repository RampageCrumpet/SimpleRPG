using HitDetection;
using SimpleRPG.Abilities;
using System.Collections.Generic;
using System.Linq;
using Unity.Netcode;
using UnityEngine;

namespace SimpleRPG
{
    public class Character : NetworkBehaviour
    {
        /// <summary>
        /// The maximum health of this character.
        /// </summary>
        [SerializeField]
        [Tooltip("The maximum health of this character.")]
        private int maxHealth = 100;

        /// <summary>
        /// The list of serialized abilities this player has.
        /// These abilities act as a blueprint for the abilities this character can use but are not invokable.
        /// </summary>
        [SerializeField]
        [Tooltip("The list of abilities this character starts with.")]
        private List<Ability> abilities;

        /// <summary>
        /// Private backer for our <see cref="PersonalAbilities"/> collection.
        /// </summary>
        private List<AbilityInstance> personalAbilityCollection = new List<AbilityInstance>();

        public WeaponData WeaponData;

        public delegate void NotifyDamageTaken();

        public event NotifyDamageTaken OnTakeDamage;

        public delegate void NotifyDeath();

        public event NotifyDeath OnDeath;

        /// <summary>
        /// The replicated health value. Only the server writes it; every client reads it, so late joiners receive the
        /// current value as part of the spawn state instead of assuming full health.
        /// </summary>
        private readonly NetworkVariable<int> networkHealth = new NetworkVariable<int>();

        /// <summary>
        /// Health to apply when the character spawns, set if <see cref="Initialize"/> is called before the network object is spawned.
        /// </summary>
        private int? pendingInitialHealth;

        /// <summary>
        /// The current health of this character. Before the character spawns on the network this reports full health.
        /// </summary>
        public int Health => IsSpawned ? networkHealth.Value : (pendingInitialHealth ?? maxHealth);

        /// <summary>
        /// The list of invokable abilities this <see cref="Character"/> has.
        /// </summary>
        public IReadOnlyCollection<AbilityInstance> PersonalAbilities
        {
            get
            {
                // Ensure that our personalAbilityCollection is up to date, if not repopulate it.
                if(!personalAbilityCollection.Select(x => x.Ability).All(abilities.Contains) || personalAbilityCollection.Count != abilities.Count)
                {
                    this.personalAbilityCollection = abilities.ConvertAll(x => new AbilityInstance(x, this));
                }

                return personalAbilityCollection.AsReadOnly();
            }
        }

        public override void OnNetworkSpawn()
        {
            if (IsServer)
            {
                networkHealth.Value = pendingInitialHealth ?? maxHealth;
            }

            networkHealth.OnValueChanged += HandleHealthChanged;
        }

        public override void OnNetworkDespawn()
        {
            networkHealth.OnValueChanged -= HandleHealthChanged;
        }

        /// <summary>
        /// Sets the health to maximum. Only the server's value matters; clients receive health through replication, so calling this on a client does nothing.
        /// </summary>
        public void Initialize()
        {
            if (!IsSpawned)
            {
                pendingInitialHealth = maxHealth;
            }
            else if (IsServer)
            {
                networkHealth.Value = maxHealth;
            }
        }

        /// <summary>
        /// Reduce the characters health by the given damage. Health is server authoritative, so calls from clients are ignored.
        /// </summary>
        /// <param name="damage">The damage to apply.</param>
        /// <param name="location">The body location that was hit.</param>
        public void TakeDamage(DamageInfo damage, BodyLocation location)
        {
            if (!IsServer)
            {
                Debug.LogWarning(this.gameObject.name + " ignored a damage request on a client; health is server authoritative.");
                return;
            }

            networkHealth.Value -= damage.Damage;

            Debug.Log(this.gameObject.name + " has taken " + damage.Damage + " damage at " + location.ToString() + ".");

            if (networkHealth.Value <= 0)
            {
                Debug.Log(this.gameObject.name + " has died.");
            }
        }

        /// <summary>
        /// Raises <see cref="OnTakeDamage"/> on every machine whenever replicated health goes down.
        /// </summary>
        private void HandleHealthChanged(int previousValue, int newValue)
        {
            if (newValue < previousValue)
            {
                OnTakeDamage?.Invoke();
            }
        }

        /// <summary>
        /// Handles the death of our character.
        /// </summary>
        public void Die()
        {
            OnDeath?.Invoke();
        }
    }
}