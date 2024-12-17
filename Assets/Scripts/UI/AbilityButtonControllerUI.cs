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
        public void CreateAbilityButtons(IEnumerable<AbilityInstance> abilityInstances)
        {
            // Create a button for each of our abilities.
            for (int x = 0; x < abilityInstances.Count(); x++)
            {
                GameObject newAbilityButtonGameObject = Object.Instantiate(abilityButtonPrefab, abillityBar.transform);
                AbilityButton newAbilityButton = newAbilityButtonGameObject.GetComponent<AbilityButton>();
                newAbilityButton.Initialize(abilityInstances.ElementAt(x));
                newAbilityButton.abillityButtonAxisName = $"Spell{x+1}";
                abilityButtons.Add(newAbilityButton);
            }
        }
    }
}