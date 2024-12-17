using SimpleRPG.ObjectInteractions;
using TMPro;
using UnityEngine;

namespace SimpleRPG.UI
{
    /// <summary>
    /// This 
    /// </summary>
    public class InteractionPromptUI : MonoBehaviour
    {
        /// <summary>
        /// The <see cref="TextMeshPro"/> in charge of displaying the interacted objects name.
        /// </summary>
        [SerializeField]
        [Tooltip("The text that displays the name of the object being interacted with.")]
        private TMP_Text interactionNameText;

        /// <summary>
        /// The <see cref="TextMeshPro"/> in charge of displaying the kind of interaction that will happen.
        /// </summary>
        [SerializeField]
        [Tooltip("The text that displays what kind of interaction will happen.")]
        private TMP_Text interactionText;


        /// <summary>
        /// Show's or hides the interaction prompt.
        /// </summary>
        /// <param name="status"> Should the interaction prompt be shown.</param>
        public void SetInteractionPromptDisplayStatus(bool status)
        {
            interactionNameText.enabled = status;
            interactionText.enabled = status;
        }

        /// <summary>
        /// Sets the interaction prompt's text to display the information from the given interactable.
        /// </summary>
        /// <param name="interactable"> The Interactable we want to display in the prompt.</param>
        public void SetInteractionPromptText(Interactable interactable)
        {
            interactionNameText.text = interactable.InteractionName;
            interactionText.text = interactable.InteractionText;
        }
    }
}
