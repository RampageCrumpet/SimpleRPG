using UnityEngine;
using SimpleRPG.ObjectInteractions;
using SimpleRPG.Abilities;

namespace SimpleRPG
{
    public class Interactor : MonoBehaviour
    {
        /// <summary>
        /// The interactable object that the player is currently interacting with. Is null if the player is not interacting with anything.
        /// </summary>
        private Interactable interactable;

        /// <summary>
        /// The source location of the interaction.
        /// </summary>
        private Vector3 interactionLocation;

        /// <summary>
        /// The maximum distance in unity units the player can move from the interaction location before the interaction is cancelled.
        /// </summary>
        private const float maximumMoveDistance = 0.5f;

        // Start is called once before the first execution of Update after the MonoBehaviour is created
        void Start()
        {
            // Subscribe to the ability activation event.
            foreach (var abilityInstance in GetComponent<Character>().PersonalAbilities)
            {
                abilityInstance.OnAbilityActivated += HandleAbilityActivated;
            }
        } 

        // Update is called once per frame
        void Update()
        {
            CheckInteractionDistance();
        }

        public void StartInteraction(Interactable interactable)
        {
            interactionLocation = transform.position;
            this.interactable = interactable;
            interactable.Interact();
        }

        /// <summary>
        /// Check to ensure that the player hasn't left the interaction range of the object they are interacting with.
        /// </summary>
        private void CheckInteractionDistance()
        {
            if((this.gameObject.transform.position - interactionLocation).magnitude > maximumMoveDistance)
            {
                interactable.StopInteraction();
                interactable = null;
            }
        }

        private void HandleAbilityActivated(AbilityInstance abilityInstance)
        {
            if(interactable != null)
            {
                interactable.StopInteraction();
                interactable = null;
            }
        }

        void OnDestroy()
        {
            // Unsubscribe from the ability activation event.
            foreach (var abilityInstance in GetComponent<Character>().PersonalAbilities)
            {
                abilityInstance.OnAbilityActivated -= HandleAbilityActivated;
            }
        }

    }
}
