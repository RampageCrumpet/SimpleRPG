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

        public event NotifyDamageTaken TakeDamage;

        /// <summary>
        /// The current health of this character.
        /// </summary>
        public int Health { get; private set; }

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

        public void Initialize()
        {
            // Set the health to maximum.
            Health = maxHealth;
        }

        /// <summary>
        /// Reduce the characters health by the given damage across the network.
        /// </summary>
        /// <param name="damage"></param>
        [Rpc(SendTo.Everyone)]
        public void TakeDamageRPC(DamageInfo damage, BodyLocation location)
        {
            Health -= damage.Damage;

            Debug.Log(this.gameObject.name + " has taken " + damage + " damage at " + location.ToString() + ".");

            if(Health <= 0)
            {
                Debug.Log(this.gameObject.name + " has died.");
            }

            TakeDamage.Invoke();
        }
    }
}