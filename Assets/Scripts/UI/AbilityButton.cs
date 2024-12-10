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
        /// <summary>
        /// The button axis we use to trigger the abillity associated with this AbillityCooldown.
        /// </summary>
        [Tooltip("The button axis we use to trigger the abillity associated with this AbillityCooldown.")]
        public string abillityButtonAxisName = "Fire1";

        [Tooltip("The mask used to darken this abillity when it's cooling down.")]
        public Image darkMask;

        [Tooltip("The Text Mesh we use to display the cooldown time.")]
        public TMP_Text cooldownText;

        private Image abilityIconImage;
        private Image darkAbillityIcon;

        private AbilityInstance abilityInstance;

        /// <summary>
        /// Initialize the UI button after creation.
        /// </summary>
        /// <param name="abilityInstance"> The <see cref="AbilityInstance"/> we want this button to gather information from.</param>
        public void Initialize(AbilityInstance abilityInstance)
        {
            abilityIconImage = this.GetComponent<Image>();
            darkAbillityIcon = this.GetComponentsInChildren<Image>().Single(x => x.gameObject != this.gameObject);

            this.abilityInstance = abilityInstance;
            abilityIconImage.sprite = abilityInstance.Ability.AbilityIcon;
            //darkAbillityIcon.sprite = ability.abillitySprite;
        }

        /// <summary>
        /// Activate the abillity associate with this button.
        /// </summary>
        public void Activate()
        {
            this.abilityInstance.Activate();
        }

        // Update is called once per frame
        void Update()
        {
            UpdateCooldown();

            if(Input.GetAxis(abillityButtonAxisName) > 0)
            {
                this.abilityInstance.Activate();
            }
        }

        /// <summary>
        /// Updates the cooldown display.
        /// </summary>
        private void UpdateCooldown()
        {
            if (abilityInstance != null)
            {
                //Scale the dark mask so the abillity is properly visible behind it.
                darkAbillityIcon.fillAmount = abilityInstance.CooldownTimeLeft / abilityInstance.Ability.CooldownTime;

                // If the ability is cooling down we want the text to be visible.
                if (abilityInstance.CooldownTimeLeft >= 0)
                {
                    cooldownText.text = Mathf.Round(abilityInstance.CooldownTimeLeft).ToString();
                }
                else
                {
                    cooldownText.text = "";
                }
            }
        }
    }
}