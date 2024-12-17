using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SimpleRPG
{
    public class InteractionCountdownUI : MonoBehaviour
    {
        /// <summary>
        /// The <see cref="TextMeshPro"/> in charge of displaying the countdown.
        /// </summary>
        [SerializeField]
        [Tooltip("The text that displays the countdown.")]
        private TMP_Text countdownText;

        /// <summary>
        /// The <see cref="Image"/> in charge of displaying the countdown.
        /// </summary>
        [SerializeField]
        [Tooltip("The image that displays the countdown.")]
        private Image countdownImage;

        /// <summary>
        /// The <see cref="Interactor"/> that is currently interacting with an object.
        /// </summary>
        private Interactor interactor;

        /// <summary>
        /// Show's or hides the countdown text and image.
        /// </summary>
        /// <param name="isVisible"> True if the InteractionCountdownUI should be visible, false otherwise.</param>
        public void SetCountdownDisplayStatus(bool isVisible)
        {
            countdownText.enabled = isVisible;
            countdownImage.enabled = isVisible;
        }

        public void SetCountdownText(Interactor interactor)
        {
            this.interactor = interactor;
        }

        public void OnGUI()
        {
            if (interactor != null && interactor.Interactable != null)
            {
                countdownText.text = interactor.InteractionTimeRemaining.ToString("F1");
                countdownImage.fillAmount = interactor.InteractionTimeRemaining / interactor.Interactable.InteractionTime;
            }
        }
    }
}
