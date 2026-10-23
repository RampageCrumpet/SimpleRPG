using SimpleRPG.Controls;
using SimpleRPG.Abilities;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace SimpleRPG.UI
{
    public class AbilityButtonControllerUI : MonoBehaviour
    {
        /// <summary>
        /// The object we want to use as a blueprint for spawning all of our abillity buttons.
        /// </summary>
        [SerializeField]
        [Tooltip("The abillity button prefab.")]
        private GameObject abilityButtonPrefab;

        private readonly List<AbilityButton> abilityButtons = new List<AbilityButton>();

        /// <summary>
        /// The <see cref="GameObject"/> that holds the UI for our abillities.
        /// </summary>
        [field: SerializeField]
        [Tooltip("The layout control for the players ability buttons.")]
        private GameObject abillityBar;

        /// <summary>
        /// Create the abillity buttons.
        /// </summary>
        public void CreateAbilityButtons(IEnumerable<IAbilitySlot> abilitySlots)
        {
            // Create a button for each of our abilities.
            for (int x = 0; x < abilitySlots.Count(); x++)
            {
                GameObject newAbilityButtonGameObject = Object.Instantiate(abilityButtonPrefab, abillityBar.transform);
                AbilityButton newAbilityButton = newAbilityButtonGameObject.GetComponent<AbilityButton>();
                newAbilityButton.Initialize(abilitySlots.ElementAt(x));
                newAbilityButton.abilityAction = GameInput.Ability(x + 1);
                abilityButtons.Add(newAbilityButton);
            }
        }
    }
}