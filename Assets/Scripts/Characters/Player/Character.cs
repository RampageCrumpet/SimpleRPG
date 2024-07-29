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
        /// </summary>
        [SerializeField]
        [Tooltip("The list of abilities this character starts with.")]
        private List<Ability> abilities;

        /// <summary>
        /// Private backer for our <see cref="abilities"/> collection.
        /// </summary>
        private List<AbilityInstance> personalAbilityCollection = new List<AbilityInstance>();

        /// <summary>
        /// The current health of this character.
        /// </summary>
        public int Health { get; private set; }

        /// <summary>
        /// A public read only collection of our abilities with properties un
        /// </summary>
        public IReadOnlyCollection<AbilityInstance> PersonalAbilities
        {
            get => personalAbilityCollection.AsReadOnly();
        }

        public void Initialize()
        {
            // Set the health to maximum.
            Health = maxHealth;

            // Populate our list of activatable abilities from the start.
            this.personalAbilityCollection = abilities.ConvertAll(x => new AbilityInstance(x, this));
        }

        /// <summary>
        /// Reduce the characters health by the given damage across the network.
        /// </summary>
        /// <param name="damage"></param>
        [Rpc(SendTo.Everyone)]
        public void TakeDamageRPC(int damage)
        {
            Health -= damage;

            Debug.Log(this.gameObject.name + " has taken " + damage + " damage.");

            if(Health <= 0)
            {
                Debug.Log(this.gameObject.name + " has died.");
            }
        }
    }
}