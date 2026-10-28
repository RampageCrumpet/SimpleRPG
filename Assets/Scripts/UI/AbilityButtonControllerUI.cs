using SimpleRPG.Abilities;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace SimpleRPG.UI
{
    public class AbilityButtonControllerUI : MonoBehaviour, ILocalPlayerView
    {
        /// <summary>
        /// The object we want to use as a blueprint for spawning all of our ability buttons.
        /// </summary>
        [SerializeField]
        [Tooltip("The ability button prefab.")]
        private GameObject abilityButtonPrefab;

        private readonly List<AbilityButton> abilityButtons = new List<AbilityButton>();

        /// <summary>
        /// The <see cref="GameObject"/> that holds the UI for our abilities.
        /// </summary>
        [SerializeField]
        [UnityEngine.Serialization.FormerlySerializedAs("abillityBar")]
        [Tooltip("The layout control for the players ability buttons.")]
        private GameObject abilityBar;

        /// <summary>
        /// Create the ability buttons.
        /// </summary>
        public void CreateAbilityButtons(IEnumerable<IAbilitySlot> abilitySlots)
        {
            // Create a button for each of our abilities.
            for (int x = 0; x < abilitySlots.Count(); x++)
            {
                GameObject newAbilityButtonGameObject = Object.Instantiate(abilityButtonPrefab, abilityBar.transform);
                AbilityButton newAbilityButton = newAbilityButtonGameObject.GetComponent<AbilityButton>();
                newAbilityButton.Initialize(abilitySlots.ElementAt(x));
                abilityButtons.Add(newAbilityButton);
            }
        }

        /// <inheritdoc/>
        public void BindLocalPlayer(Character character)
        {
            UnbindLocalPlayer();
            CreateAbilityButtons(character.Loadout.Slots);
        }

        /// <inheritdoc/>
        public void UnbindLocalPlayer()
        {
            foreach (AbilityButton button in abilityButtons)
            {
                if (button != null) Object.Destroy(button.gameObject);
            }

            abilityButtons.Clear();
        }
    }
}