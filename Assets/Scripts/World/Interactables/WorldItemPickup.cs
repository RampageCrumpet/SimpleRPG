using SimpleRPG.InventorySystem;
using Unity.Netcode;
using UnityEngine;

namespace SimpleRPG.ObjectInteractions
{
    /// <summary>A catalog item in the world. The server validates the caller and transfers it only if inventory space is available.</summary>
    public sealed class WorldItemPickup : Interactable
    {
        [SerializeField] private Item item;
        public Item Item => item;
        private bool collected;

        public override bool IsLookedAtVia(Collider collider) => collider != null && collider.GetComponentInParent<WorldItemPickup>() == this;

        public override void ActivateInteraction(Interactor interactor)
        {
            if (IsSpawned) PickUpRpc();
            interactor.StopInteraction();
        }

        public override void FinishInteraction(Interactor interactor) { }

        [Rpc(SendTo.Server)]
        private void PickUpRpc(RpcParams rpcParams = default)
        {
            if (!NetworkManager.ConnectedClients.TryGetValue(rpcParams.Receive.SenderClientId, out var client)) return;
            var player = client.PlayerObject != null ? client.PlayerObject.GetComponent<Player>() : null;
            TryPickUp(player != null ? player.character : null);
        }

        public bool TryPickUp(Character character)
        {
            if (!IsServer || !IsSpawned || collected || item == null || character == null || !character.IsAlive ||
                character.NetworkManager != NetworkManager || Vector3.Distance(character.transform.position, transform.position) > InteractionRange) return false;
            var inventory = character.GetComponent<Inventory>();
            if (inventory == null || !inventory.AddItemInstance(item)) return false;
            collected = true;
            NetworkObject.Despawn(true);
            return true;
        }
    }
}
