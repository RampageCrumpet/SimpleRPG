using System.Collections.Generic;
using System.Linq;
using Unity.Netcode;
using UnityEngine;

namespace SimpleRPG.ObjectInteractions
{
    /// <summary>
    /// This class is the base class for all interactable objects in the game.
    /// </summary>
    public abstract class Interactable : NetworkBehaviour
    {
        [Header("Interactable Settings")]
        /// <summary>
        /// The range at which the player can interact with this object.
        /// </summary>
        [field: SerializeField]
        [Tooltip("The range at which the player can interact with this object.")]
        public float InteractionRange { get; protected set; }

        /// <summary>
        /// The name of the object that will be displayed when the player can interact with it.
        /// </summary>
        [field: SerializeField]
        [Tooltip("The name of the object that will be displayed when the player can interact with it.")]
        public string InteractionName { get; protected set; }

        [field: SerializeField]
        [Tooltip("The type of interaction that will be displayed to the player when they try to interact with this object.")]
        public string InteractionText { get; protected set; }

        /// <summary>
        /// The time it takes to interact with this object in seconds.
        /// </summary>
        [field: SerializeField]
        [Tooltip("The time it takes to interact with this object in seconds.")]
        public float InteractionTime { get; protected set; }

        private Renderer[] outlineRenderers;

        /// <summary>
        /// The renderers outlined while the player can interact with this object. By default every mesh under it.
        /// </summary>
        public virtual IReadOnlyList<Renderer> OutlineRenderers =>
            outlineRenderers ??= GetComponentsInChildren<Renderer>().Where(r => r is MeshRenderer || r is SkinnedMeshRenderer).ToArray();

        public virtual void Start()
        {
            //Register this interactable so that it can be interacted with.
            InteractableManager.RegisterInteractable(this);
        }

        /// <summary>
        /// Whether the player looking at a collider is looking at this interactable. By default only this object's own collider counts.
        /// </summary>
        /// <param name="collider"> The collider under the player's crosshair.</param>
        public virtual bool IsLookedAtVia(Collider collider)
        {
            return collider.gameObject == gameObject;
        }

        /// <summary>
        /// Activates the effect of the interactable.
        /// </summary>
        /// <param name="interactor"> The Interactor activating this interactable.</param>
        public abstract void ActivateInteraction(Interactor interactor);

        /// <summary>
        /// Finish interacting with the interactable and apply the effects of the interaction.
        /// </summary>
        /// <param name="interactor"> The Interactor finishing the interaction.</param>
        public abstract void FinishInteraction(Interactor interactor);

        public override void OnDestroy()
        {
            //Remove ourselves from the list of interactable objects.
            InteractableManager.UnregisterInteractable(this);

            base.OnDestroy();
        }
    }
}
