using SimpleRPG.Audio;
using SimpleRPG.ObjectInteractions;
using Unity.Netcode;
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

        // A lock is not a networked object, so a player's machine names it by its key through the key ring, which is.
        public override bool TryGetHandle(out NetworkObjectReference anchor, out int id)
        {
            anchor = default;
            id = keyId;
            if (ring == null || ring.NetworkObject == null || !ring.NetworkObject.IsSpawned)
            {
                return false;
            }

            anchor = ring.NetworkObject;
            return true;
        }

        private const byte UnlockMessage = 0;
        private const byte LockedMessage = 1;

        /// <inheritdoc/>
        public override void ActivateInteraction(Interactor interactor)
        {
            // The server decides whether the party holds the key and the player is near the door.
            KeyUseResult result = KeyUseResult.NotHeld;
            if (ring != null && (ring.IsServer || !ring.IsSpawned))
            {
                float? distance = ring.DistanceToLock(keyId, interactor.transform.position);
                result = distance.HasValue ? ring.TryOpen(keyId, distance.Value) : KeyUseResult.NotHeld;
            }

            NotifyInteractor(interactor, result == KeyUseResult.Opened ? UnlockMessage : LockedMessage);
            interactor.StopInteraction();
        }

        /// <summary>The player who tried the door hears whether it opened.</summary>
        protected override void OnInteractorMessage(byte message)
        {
            if (message == UnlockMessage)
            {
                InteractableAudio.TryPlay(this, InteractionCue.Unlock);
            }
            else
            {
                Debug.Log($"This door is locked (key {keyId}).");
                InteractableAudio.TryPlay(this, InteractionCue.Locked);
            }
        }

        /// <inheritdoc/>
        public override void FinishInteraction(Interactor interactor)
        {
        }
    }
}
