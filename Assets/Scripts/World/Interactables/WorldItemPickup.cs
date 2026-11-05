using SimpleRPG.Audio;
using SimpleRPG.InventorySystem;
using Unity.Netcode;
using UnityEngine;

namespace SimpleRPG.ObjectInteractions
{
    /// <summary>A catalog item in the world. The server validates the caller and transfers it only if inventory space is available.</summary>
    public sealed class WorldItemPickup : Interactable, IDroppedItemForm
    {
        [SerializeField] private Item item;
        public Item Item => item;
        private bool collected;

        /// <summary>
        /// The level of the loot source that placed this item, which decides which affix tiers it can roll when it is picked up. Set by the server
        /// where the item is placed; the lowest level otherwise. Not used by an item a player dropped, which has already rolled.
        /// </summary>
        public int ItemLevel { get; set; } = SimpleRPG.InventorySystem.ItemLevel.Minimum;

        // A pickup a player dropped is that very item instance: it keeps what it rolled and picks up as itself. Held by the server only.
        private Item carried;
        private uint carriedInstanceId;

        // What the carried item rolled, for every machine to read (a tooltip, say). A pickup that is only the asset has none.
        private readonly NetworkVariable<ItemAffixes> affixes = new NetworkVariable<ItemAffixes>();

        /// <summary>What the item on the ground rolled. Empty for an item that is still just the asset, which rolls when it is picked up.</summary>
        public ItemAffixes Affixes => carried != null ? carried.Affixes : affixes.Value;

        /// <inheritdoc/>
        public void InitializeDropped(Item instance, uint instanceId)
        {
            if (instance == null || (IsSpawned && !IsServer))
            {
                return;
            }

            carried = instance;
            carriedInstanceId = instanceId;
            if (IsSpawned)
            {
                affixes.Value = instance.Affixes;

                // Every machine hears the item land; a pickup the dungeon placed does not announce itself.
                DroppedRpc();
            }
        }

        /// <summary>
        /// The server removing the pickup is how every machine learns it was collected, so the sound plays from the despawn. A despawn caused by
        /// the session ending is not a pickup.
        /// </summary>
        public override void OnNetworkDespawn()
        {
            if (!IsServer && NetworkManager != null && !NetworkManager.ShutdownInProgress && item != null)
            {
                InventoryAudio.PlayAt(item, InventoryAudioAction.Pickup, transform.position);
            }

            // A dropped item nobody picked up ends with the pickup; one that was picked up is in an inventory now and is not ours to destroy.
            if (IsServer && carried != null)
            {
                Destroy(carried);
                carried = null;
            }

            base.OnNetworkDespawn();
        }

        [Rpc(SendTo.ClientsAndHost)]
        private void DroppedRpc()
        {
            if (item != null) InventoryAudio.PlayAt(item, InventoryAudioAction.Drop, transform.position);
        }

        public override bool IsLookedAtVia(Collider collider) => collider != null && collider.GetComponentInParent<WorldItemPickup>() == this;

        // The server timed the interaction and now takes the item for the player who made it. A one-shot interaction ends itself.
        public override void ActivateInteraction(Interactor interactor)
        {
            if (IsSpawned && IsServer) TryPickUp(interactor.Character);
            interactor.StopInteraction();
        }

        public override void FinishInteraction(Interactor interactor) { }

        public bool TryPickUp(Character character)
        {
            if (!IsServer || !IsSpawned || collected || item == null || character == null || !character.IsAlive ||
                character.NetworkManager != NetworkManager || Vector3.Distance(character.transform.position, transform.position) > InteractionRange) return false;
            var inventory = character.GetComponent<Inventory>();
            if (inventory == null) return false;

            if (carried != null)
            {
                // The dropped instance goes back as itself, in the first place it fits.
                if (!inventory.TryFindRoom(carried, out Vector2Int room)) return false;
                inventory.PutBack(carried, room, carriedInstanceId);
                carried = null;
            }
            else if (!inventory.AddItemInstance(item, ItemLevel))
            {
                return false;
            }

            collected = true;
            NetworkObject.Despawn(true);
            return true;
        }
    }
}
