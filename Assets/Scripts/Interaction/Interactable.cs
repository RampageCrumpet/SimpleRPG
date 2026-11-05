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

        /// <summary>
        /// False while the player cannot interact with this object, for example a living enemy that only becomes lootable when it dies.
        /// </summary>
        public virtual bool CanBeInteractedWith => true;

        private Renderer[] outlineRenderers;

        /// <summary>
        /// How a player's machine names this interactable to the server when it asks to interact: the networked object it belongs to and which
        /// interactable that is. By default it is a component of its own networked object; one that is not on a networked object of its own
        /// overrides this and the <see cref="IInteractableHost"/> that resolves it. False if it cannot be named (it is not spawned).
        /// </summary>
        public virtual bool TryGetHandle(out NetworkObjectReference anchor, out int id)
        {
            anchor = default;
            id = 0;
            if (NetworkObject == null || !NetworkObject.IsSpawned)
            {
                return false;
            }

            anchor = NetworkObject;
            id = System.Array.IndexOf(GetComponents<Interactable>(), this);
            return id >= 0;
        }

        /// <summary>
        /// Finds the interactable a player's machine named, on the server. The reverse of <see cref="TryGetHandle"/>.
        /// </summary>
        public static bool TryResolve(NetworkObjectReference anchor, int id, out Interactable interactable)
        {
            interactable = null;
            if (!anchor.TryGet(out NetworkObject networkObject))
            {
                return false;
            }

            if (networkObject.TryGetComponent(out IInteractableHost host))
            {
                return host.TryFindInteractable(id, out interactable);
            }

            Interactable[] all = networkObject.GetComponents<Interactable>();
            if (id < 0 || id >= all.Length)
            {
                return false;
            }

            interactable = all[id];
            return true;
        }

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
        /// The interaction has run its course and takes effect. <b>Runs on the server</b> (or offline), never on the interacting player's machine:
        /// the server timed it, so a client cannot hurry it. Anything the player's own machine must do (open a menu, play a sound) goes through
        /// <see cref="NotifyInteractor"/>. A one-shot interaction (picking something up) ends the interaction itself with
        /// <see cref="Interactor.StopInteraction"/>.
        /// </summary>
        /// <param name="interactor"> The Interactor activating this interactable.</param>
        public abstract void ActivateInteraction(Interactor interactor);

        /// <summary>
        /// The interaction has ended, whether it ran its course or was cancelled (the player moved away, acted, died). Runs on the server.
        /// </summary>
        /// <param name="interactor"> The Interactor finishing the interaction.</param>
        public abstract void FinishInteraction(Interactor interactor);

        /// <summary>
        /// Tells the machine of the player who is interacting something happened: a menu should open, a sound should play. Called on the server;
        /// it reaches that player's machine and no other, and runs on the spot where the server is that player's machine or there is no network.
        /// </summary>
        /// <param name="interactor">The player interacting, or null where there is none (then it runs here).</param>
        /// <param name="message">What happened, in whatever codes the interactable defines.</param>
        protected void NotifyInteractor(Interactor interactor, byte message)
        {
            if (interactor == null || !IsSpawned || !IsServer || interactor.IsOwner)
            {
                OnInteractorMessage(message);
                return;
            }

            InteractorMessageRpc(message, RpcTarget.Single(interactor.OwnerClientId, RpcTargetUse.Temp));
        }

        /// <summary>Runs on the interacting player's machine when the server called <see cref="NotifyInteractor"/>.</summary>
        protected virtual void OnInteractorMessage(byte message)
        {
        }

        [Rpc(SendTo.SpecifiedInParams)]
        private void InteractorMessageRpc(byte message, RpcParams rpcParams = default) => OnInteractorMessage(message);

        public override void OnDestroy()
        {
            //Remove ourselves from the list of interactable objects.
            InteractableManager.UnregisterInteractable(this);

            base.OnDestroy();
        }
    }
}

namespace SimpleRPG.ObjectInteractions
{
    /// <summary>
    /// A networked object that stands for interactables which are not networked objects themselves (a lock on a door), so the server can find the
    /// one a player named. Its own id scheme: whatever <see cref="Interactable.TryGetHandle"/> of those interactables says.
    /// </summary>
    public interface IInteractableHost
    {
        bool TryFindInteractable(int id, out Interactable interactable);
    }
}
