using SimpleRPG.Abilities;
using System.Collections.Generic;
using UnityEngine;

namespace SimpleRPG.UI
{
    public class PlayerUIController : MonoBehaviour
    {
        /// <summary>
        /// The object we want to use as a blueprint for spawning all of our abillity buttons.
        /// </summary>
        [SerializeField]
        [Tooltip("The abillity button prefab.")]
        GameObject abilityButtonPrefab;

        private readonly List<AbilityButton> abilityButtons = new List<AbilityButton>();

        [SerializeField]
        [Tooltip("The layout control for the players ability buttons.")]
        private GameObject abillityBar;

        /// <summary>
        /// Create the abillity buttons.
        /// </summary>
        public void CreateAbilityButtons(Character character)
        {
            // Create a button for each of our abilities.
            foreach (Ability ability in character.PersonalAbilities)
            {
                GameObject newAbilityButtonGameObject = Object.Instantiate(abilityButtonPrefab, abillityBar.transform);
                AbilityButton newAbilityButton = newAbilityButtonGameObject.GetComponent<AbilityButton>();
                newAbilityButton.Initialize(ability);
                abilityButtons.Add(newAbilityButton);
            }
        }
    }
}