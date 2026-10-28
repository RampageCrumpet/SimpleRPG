using SimpleRPG.ObjectInteractions;
using SimpleRPG.Rendering;
using UnityEngine;

namespace SimpleRPG.UI
{
    /// <summary>
    /// This class is in charge of displaying the interaction prompt to the player.
    /// </summary>
    public class InteractionUI : MonoBehaviour, ILocalPlayerView
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
                // The player's controls refresh the target and act on the interact key; this only shows what they found.
                Interactable interactionTarget = interactor.Target;
                HandleInteractionDisplay(interactionTarget);
                InteractableOutline.SetTargets(interactionTarget != null ? interactionTarget.OutlineRenderers : null);
            }
        }

        private void OnDisable()
        {
            InteractableOutline.SetTargets(null);
        }

        public void SetInteractor(Interactor interactor)
        {
            this.interactor = interactor;
        }

        /// <inheritdoc/>
        public void BindLocalPlayer(Character character)
        {
            SetInteractor(character != null ? character.GetComponent<Interactor>() : null);
        }

        /// <inheritdoc/>
        public void UnbindLocalPlayer()
        {
            interactor = null;
            interactionPrompt?.SetInteractionPromptDisplayStatus(false);
            interactionCountdown?.SetCountdownDisplayStatus(false);
            InteractableOutline.SetTargets(null);
        }

        /// <summary>
        /// Handles hiding and showing the <see cref="InteractionPromptUI"/> and <see cref="InteractionCountdownUI"/>.
        /// </summary>
        /// <param name="interactionTarget"></param>
        private void HandleInteractionDisplay(Interactable interactionTarget)
        {
            if (interactor.IsInteracting)
            {
                interactionCountdown.SetCountdownText(interactor);
            }

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
    }
}
