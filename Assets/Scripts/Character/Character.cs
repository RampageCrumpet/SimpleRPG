using SimpleRPG.Abilities;
using System.Collections.Generic;
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
        private List<Ability> personalAbilityCollection = new List<Ability>();

        /// <summary>
        /// The current health of this character.
        /// </summary>
        private int health;

        /// <summary>
        /// A public read only collection of our abilities with properties un
        /// </summary>
        public IReadOnlyCollection<Ability> PersonalAbilities
        {
            get => personalAbilityCollection.AsReadOnly();
        }

        /// <summary>
        /// Adds an ability
        /// </summary>
        public void AddAbilitry(Ability ability)
        {
            personalAbilityCollection.Add((Ability)ScriptableObject.CreateInstance(ability.GetType()));
            abilities.Add(ability);
        }

        public void Initialize()
        {
            // Set the health to maximum.
            health = maxHealth;

            // Populate our list of activatable abilities from the start.
            foreach (Ability ability in abilities)
            {
                personalAbilityCollection.Add(Instantiate(ability));
            }
        }
    }
}