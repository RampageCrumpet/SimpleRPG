using UnityEngine;

namespace SimpleRPG.ObjectInteractions
{
    public abstract class Interactable : MonoBehaviour
    {
        /// <summary>
        /// The range at which the player can interact with this object.
        /// </summary>
        [property: SerializeField]
        [Tooltip("The range at which the player can interact with this object.")]
        public float InteractionRange { get; private set; }

        /// <summary>
        /// The name of the object that will be displayed when the player can interact with it.
        /// </summary>
        [property: SerializeField]
        [Tooltip("The name of the object that will be displayed when the player can interact with it.")]
        public string InteractionName { get; private set; }

        /// <summary>
        /// The time it takes to interact with this object in seconds.
        /// </summary>
        [property: SerializeField]
        [Tooltip("The time it takes to interact with this object in seconds.")]
        public float InteractionTime { get; private set; }

        /// <summary>
        /// The time the player last interacted with this object.
        /// </summary>
        public float InteractionTimeRemaining { get; private set; }

        private bool isInteracting = false;

        /// <summary>
        /// Start an interaction with the object.
        /// </summary>
        public virtual void Interact()
        {
            InteractionTimeRemaining = InteractionTime;
        }

        /// <summary>
        /// Stops the interaction with the object.
        /// </summary>
        public virtual void StopInteraction()
        {
            isInteracting = false;
        }

        public void Update()
        {
            InteractionTimeRemaining -= Time.deltaTime;

            if(InteractionTimeRemaining <= 0 && isInteracting)
            {
                FinishInteraction();
            }
        }

        /// <summary>
        /// We've finished interacting with the interactable object and want to trigger it's effect.
        /// </summary>
        protected abstract void FinishInteraction();
    }
}
