using SimpleRPG.ObjectInteractions;
using UnityEngine;

namespace SimpleRPG.Keys
{
    /// <summary>
    /// Added at runtime to each lock object of a locked door (one per door end). Using it asks the server to open the lock; the server only does
    /// so if the party holds the key, and every machine then switches the door's lock objects off.
    /// </summary>
    public class LockedDoorInteractable : Interactable
    {
        private DungeonKeyRing ring;
        private int keyId;

        /// <summary>
        /// Wires the door to its key and the ring that tracks it. Call right after adding the component.
        /// </summary>
        public void Setup(DungeonKeyRing keyRing, int id)
        {
            ring = keyRing;
            keyId = id;
            InteractionRange = DungeonKeyRing.DoorRange;
            InteractionName = "Locked door";
            InteractionText = "Unlock";
            InteractionTime = 0.5f;
        }

        /// <summary>
        /// A lock object has several colliders; looking at any of them counts.
        /// </summary>
        public override bool IsLookedAtVia(Collider collider)
        {
            return collider.transform.IsChildOf(transform);
        }

        /// <inheritdoc/>
        public override void ActivateInteraction(Interactor interactor)
        {
            if (ring != null && ring.Holds(keyId))
            {
                ring.RequestOpen(keyId);
            }
            else
            {
                Debug.Log($"This door is locked (key {keyId}).");
            }

            interactor.StopInteraction();
        }

        /// <inheritdoc/>
        public override void FinishInteraction(Interactor interactor)
        {
        }
    }
}
