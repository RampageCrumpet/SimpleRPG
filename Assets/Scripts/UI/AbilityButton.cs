using SimpleRPG.Abilities;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SimpleRPG.UI
{
    /// <summary>
    /// This script handles controller the cooldown display for an abillity.
    /// </summary>
    public class AbilityButton : MonoBehaviour
    {
        [Tooltip("The mask used to darken this ability when it's cooling down.")]
        public Image darkMask;

        [Tooltip("The Text Mesh we use to display the cooldown time.")]
        public TMP_Text cooldownText;

        private Image abilityIconImage;
        private Image darkAbilityIcon;

        private IAbilitySlot abilitySlot;

        /// <summary>
        /// Initialize the UI button after creation.
        /// </summary>
        /// <param name="abilitySlot"> The <see cref="IAbilitySlot"/> we want this button to gather information from.</param>
        public void Initialize(IAbilitySlot abilitySlot)
        {
            abilityIconImage = this.GetComponent<Image>();
            darkAbilityIcon = this.GetComponentsInChildren<Image>().Single(x => x.gameObject != this.gameObject);

            this.abilitySlot = abilitySlot;
            abilityIconImage.sprite = abilitySlot.Icon;
            //darkAbilityIcon.sprite = ability.abillitySprite;
        }

        /// <summary>
        /// Activate the abillity associate with this button.
        /// </summary>
        public void Activate()
        {
            this.abilitySlot.Activate();
        }

        // Update is called once per frame
        void Update()
        {
            // The player's controls (LocalPlayerControls) activate abilities; the button only shows the slot's state.
            UpdateCooldown();
        }

        /// <summary>
        /// Updates the cooldown display.
        /// </summary>
        private void UpdateCooldown()
        {
            if (abilitySlot != null)
            {
                //Scale the dark mask so the abillity is properly visible behind it.
                darkAbilityIcon.fillAmount = abilitySlot.CooldownTime > 0f ? abilitySlot.CooldownTimeLeft / abilitySlot.CooldownTime : 0f;

                // If the ability is cooling down we want the text to be visible.
                if (abilitySlot.CooldownTimeLeft >= 0)
                {
                    cooldownText.text = Mathf.Round(abilitySlot.CooldownTimeLeft).ToString();
                }
                else
                {
                    cooldownText.text = "";
                }
            }
        }
    }
}
