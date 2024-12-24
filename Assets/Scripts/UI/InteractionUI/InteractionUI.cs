using SimpleRPG.ObjectInteractions;
using System.Linq;
using TMPro;
using Unity.VisualScripting.YamlDotNet.Core.Tokens;
using UnityEngine;

namespace SimpleRPG.UI
{
    /// <summary>
    /// This class is in charge of displaying the interaction prompt to the player.
    /// </summary>
    public class InteractionUI : MonoBehaviour
    {
        /// <summary>
        /// The <see cref="Interactor"/> that is in charge of interacting with objects in the world.
        /// </summary>
        private Interactor interactor;

        /// <summary>
        /// The <see cref="InteractionPromptUI"/> in charge of displaying the interacted objects name and interaction type.
        /// </summary>
        private InteractionPromptUI interactionPrompt;

        /// <summary>
        /// The <see cref="InteractionCountdownUI"/> in charge of displaying the countdown to the player.
        /// </summary>
        private InteractionCountdownUI interactionCountdown;

        public void Start()
        {
            interactionPrompt = this.gameObject.GetComponentInChildren<InteractionPromptUI>();
            interactionPrompt.SetInteractionPromptDisplayStatus(false);

            interactionCountdown = this.gameObject.GetComponentInChildren<InteractionCountdownUI>();
            interactionCountdown.SetCountdownDisplayStatus(false);
        }

        public void Update()
        {   
            // Only update the UI if our interactor has been set.
            if(interactor != null)
            {
                Interactable interactionTarget = interactor.FindInteractionTarget();
                HandleInteraction(interactionTarget);
                HandleInteractionDisplay(interactionTarget);
            }
        }

        public void SetInteractor(Interactor interactor)
        {
            this.interactor = interactor;
        }

        /// <summary>
        /// Handles hiding and showing the <see cref="InteractionPromptUI"/> and <see cref="InteractionCountdownUI"/>.
        /// </summary>
        /// <param name="interactionTarget"></param>
        private void HandleInteractionDisplay(Interactable interactionTarget)
        {
            if (interactionTarget != null)
            {
                //If we're not interacting with anything, show the prompt.
                interactionPrompt.SetInteractionPromptDisplayStatus(!interactor.IsInteracting);
                interactionPrompt.SetInteractionPromptText(interactionTarget);
            }
            else
            {
                //If we're interacting with something, hide the prompt.
                interactionPrompt.SetInteractionPromptDisplayStatus(false);
            }

            // If we're interacting with an object make sure the countdown is displayed.
            bool showInteractionCountdown = interactor.IsInteracting && interactor.InteractionTimeRemaining > 0;
            interactionCountdown.SetCountdownDisplayStatus(showInteractionCountdown);
        }

        /// <summary>
        /// Handles the interaction between the player and the interactable object.
        /// </summary>
        private void HandleInteraction(Interactable interactionTarget)
        {
            if (Input.GetButtonDown("Interact") && interactionTarget != null)
            {
                if (interactor.IsInteracting)
                {
                    interactor.StopInteraction();
                }
                else
                {
                    interactor.StartInteraction(interactionTarget);
                    interactionCountdown.SetCountdownText(interactor);
                    interactionCountdown.SetCountdownDisplayStatus(true);
                }
            }
        }
    }
}
