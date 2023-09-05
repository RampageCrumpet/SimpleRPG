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
        [SerializeField]
        [Tooltip("The button axis we use to trigger the abillity associated with this AbillityCooldown.")]
        public string abillityButtonAxisName = "Fire1";

        [SerializeField]
        [Tooltip("The mask used to darken this abillity when it's cooling down.")]
        public Image darkMask;

        [SerializeField]
        [Tooltip("The Text Mesh we use to display the cooldown time.")]
        public TMP_Text cooldownText;

        private Image abilityIconImage;
        private Image darkAbillityIcon;


        private Ability ability;

        public void Initialize(Ability ability)
        {
            abilityIconImage = this.GetComponent<Image>();
            darkAbillityIcon = this.GetComponentsInChildren<Image>().Single(x => x.gameObject != this.gameObject);

            this.ability = ability;
            abilityIconImage.sprite = ability.abillitySprite;
            //darkAbillityIcon.sprite = ability.abillitySprite;
        }

        /// <summary>
        /// Activate the abillity associate with this button.
        /// </summary>
        public void Activate()
        {
            this.ability.Activate();
        }


        // Update is called once per frame
        void Update()
        {
            UpdateCooldown();

            if(Input.GetKeyDown(abillityButtonAxisName))
            {
                this.ability.Activate();
            }
        }

        /// <summary>
        /// Updates the cooldown display.
        /// </summary>
        private void UpdateCooldown()
        {
            float cooldownTimeLeft = ability.LastActivationTime + ability.cooldownTime - Time.time;
            
            //Scale the dark mask so the abillity is properly visible behind it.
            darkAbillityIcon.fillAmount = cooldownTimeLeft / ability.cooldownTime;

            // If the ability is cooling down we want the text to be visible.
            if (cooldownTimeLeft >= 0)
            {
                cooldownText.text = Mathf.Round(cooldownTimeLeft).ToString();
            }
            else
            {
                cooldownText.text = "";
            }
        }
    }
}