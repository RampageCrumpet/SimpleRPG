using UnityEngine;
using SimpleRPG.ObjectInteractions;
using SimpleRPG.Abilities;
using NUnit.Framework;
using System.Collections.Generic;
using System.Linq;

namespace SimpleRPG
{
    public class Interactor : MonoBehaviour
    {
        /// <summary>
        /// The interactable object that the player is currently interacting with. Is null if the player is not interacting with anything.
        /// </summary>
        public Interactable Interactable {get; private set; }

        /// <summary>
        /// The source location of the interaction.
        /// </summary>
        private Vector3 interactionLocation;

        /// <summary>
        /// The maximum distance in unity units the player can move from the interaction location before the interaction is cancelled.
        /// </summary>
        private const float maximumMoveDistance = 0.5f;


        /// <summary>
        /// The time the player last interacted with this object.
        /// </summary>
        public float InteractionTimeRemaining { get; private set; }


        /// <summary>
        /// Backing field for the IsInteracting property.
        /// </summary>
        private bool isInteracting = false;

        /// <summary>
        /// Is the interaction effect currently active?
        /// </summary>
        private bool isInteractionActive = false;

        /// <summary>
        /// Is this Interactor currently interacting with something.
        /// </summary>
        public bool IsInteracting
        {
            get
            {
                return isInteracting && Interactable != null;
            }
            set
            {
                isInteracting = value;
            }
        }

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
        private void Update()
        {
            CheckInteractionDistance();

            InteractionTimeRemaining -= Time.deltaTime;

            // Check to see if we've completed interacting with the object.
            if (InteractionTimeRemaining <= 0 && IsInteracting && !isInteractionActive)
            {
                isInteractionActive = true;
                Interactable.ActivateInteraction(this);
            }
        }

        /// <summary>
        /// Start interacting with the interactable.
        /// </summary>
        /// <param name="interactable"> The <see cref="ObjectInteractions.Interactable"/> we want to start interacting with.</param>
        public void StartInteraction(Interactable interactable)
        {
            InteractionTimeRemaining = interactable.InteractionTime;

            interactionLocation = transform.position;
            this.Interactable = interactable;
            this.IsInteracting = true;
        }

        public void StopInteraction()
        {
            if(Interactable != null)
            {
                this.IsInteracting = false;
                this.isInteractionActive = false;
                Interactable.FinishInteraction(this);
                Interactable = null;
            }
        }

        /// <summary>
        /// Check to ensure that the player hasn't left the interaction range of the object they are interacting with.
        /// </summary>
        private void CheckInteractionDistance()
        {
            if((this.gameObject.transform.position - interactionLocation).magnitude > maximumMoveDistance)
            {
                StopInteraction();
            }
        }

        /// <summary>
        /// Stop the interaction with the interactable if the player takes any action.
        /// </summary>
        /// <param name="abilityInstance">The abillity that was used.</param>
        private void HandleAbilityActivated(AbilityInstance abilityInstance)
        {
            if(Interactable != null)
            {
                StopInteraction();
            }
        }

        /// <summary>
        /// Finds the interactable object that the player is looking at within range and returns it if one exists.
        /// </summary>
        /// <returns> Returns the interactable object the player is looking at within range if one exists, otherwise returns null.</returns>
        public Interactable FindInteractionTarget()
        {
            Camera mainCamera = Camera.main;
            Ray ray = mainCamera.ScreenPointToRay(new Vector3(Screen.width / 2, Screen.height / 2));
            RaycastHit hit;

            if (Physics.Raycast(ray, out hit))
            {
                foreach (var interactable in InteractableManager.Interactables)
                {
                    if (interactable.gameObject == hit.collider.gameObject && Vector3.Distance(transform.position, interactable.transform.position) <= interactable.InteractionRange)
                    {
                        return interactable;
                    }
                }
            }

            return null;
        }

        private void OnDestroy()
        {
            // Unsubscribe from the ability activation event.
            foreach (var abilityInstance in GetComponent<Character>().PersonalAbilities)
            {
                abilityInstance.OnAbilityActivated -= HandleAbilityActivated;
            }
        }

    }
}
