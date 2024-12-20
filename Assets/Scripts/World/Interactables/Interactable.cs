using UnityEngine;

namespace SimpleRPG.ObjectInteractions
{
    /// <summary>
    /// This class is the base class for all interactable objects in the game.
    /// </summary>
    public abstract class Interactable : MonoBehaviour
    {
        /// <summary>
        /// The range at which the player can interact with this object.
        /// </summary>
        [field: SerializeField]
        [Tooltip("The range at which the player can interact with this object.")]
        public float InteractionRange { get; private set; }

        /// <summary>
        /// The name of the object that will be displayed when the player can interact with it.
        /// </summary>
        [field: SerializeField]
        [Tooltip("The name of the object that will be displayed when the player can interact with it.")]
        public string InteractionName { get; private set; }

        [field: SerializeField]
        [Tooltip("The type of interaction that will be displayed to the player when they try to interact with this object.")]
        public string InteractionText { get; private set; }

        /// <summary>
        /// The time it takes to interact with this object in seconds.
        /// </summary>
        [field: SerializeField]
        [Tooltip("The time it takes to interact with this object in seconds.")]
        public float InteractionTime { get; private set; }

        public void Start()
        {
            //Register this interactable so that it can be interacted with.
            InteractableManager.RegisterInteractable(this);
        }

        /// <summary>
        /// We've finished interacting with the interactable object and want to trigger it's effect.
        /// </summary>
        public abstract void FinishInteraction();

        public void OnDestroy()
        {
            //Remove ourselves from the list of interactable objects.
            InteractableManager.UnregisterInteractable(this);
        }
    }
}
