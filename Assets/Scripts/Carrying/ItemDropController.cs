using Unity.Netcode;
using UnityEngine;

namespace SimpleRPG.InventorySystem
{
    /// <summary>
    /// Why a request to drop an item was accepted or refused.
    /// </summary>
    public enum DropResult
    {
        Success,
        NotAuthority,
        NotYourInventory,
        NoLivingCharacter,
        ItemNotInInventory,
        CannotBeDropped
    }

    /// <summary>
    /// Owns the rules for putting an item from a character's inventory on the ground. Like <see cref="InventoryMoveController"/> a request is
    /// checked in full before anything changes, so it either happens completely or not at all. The item that lands on the ground is the same
    /// instance that left the inventory, with the same instance id and the affixes it rolled, and picking it up returns that instance;
    /// nothing is created or rolled again. The client only says which item; where it lands is the server's decision. Server (or offline) only.
    /// </summary>
    public static class ItemDropController
    {
        // How far in front of the character the item is put down, and how high above the ground the search for the floor starts.
        private const float ReachInFront = 1f;
        private const float SearchHeight = 0.5f;
        private const float SearchDepth = 2f;
        private const float ShoulderHeight = 1f;

        /// <summary>
        /// Checks dropping an item from an inventory on behalf of a client, without changing anything.
        /// </summary>
        /// <param name="requester">The client asking. Only the owner of a character's inventory may drop from it.</param>
        public static DropResult Validate(Inventory inventory, Item item, ulong requester)
        {
            if (inventory == null || item == null || !inventory.Contains(item))
            {
                return DropResult.ItemNotInInventory;
            }

            if (!inventory.CanMutateContents)
            {
                return DropResult.NotAuthority;
            }

            // A chest's contents are taken, not dropped, and nobody drops from another player's inventory.
            if (!inventory.TryGetComponent(out Character character) || inventory.OwnerClientId != requester)
            {
                return DropResult.NotYourInventory;
            }

            if (!character.IsAlive)
            {
                return DropResult.NoLivingCharacter;
            }

            GameObject prefab = item.SourceAsset.WorldPrefab;
            return prefab != null && prefab.TryGetComponent(out IDroppedItemForm _) ? DropResult.Success : DropResult.CannotBeDropped;
        }

        /// <summary>
        /// Validates and then drops the item in front of the character.
        /// </summary>
        /// <param name="dropped">The object now on the ground, when the drop succeeded.</param>
        public static DropResult TryDrop(Inventory inventory, Item item, ulong requester, out IDroppedItemForm dropped)
        {
            dropped = null;
            DropResult result = Validate(inventory, item, requester);
            if (result != DropResult.Success)
            {
                return result;
            }

            Character character = inventory.GetComponent<Character>();
            GameObject instance = Object.Instantiate(item.SourceAsset.WorldPrefab, DropPoint(character), Quaternion.Euler(0f, character.transform.eulerAngles.y, 0f));

            // Validation covered everything, so this only fails if the item vanished in between; nothing has been spawned yet.
            if (!inventory.TryTakeOut(item, out uint instanceId, out Vector2Int _))
            {
                Object.Destroy(instance);
                return DropResult.ItemNotInInventory;
            }

            if (inventory.IsSpawned)
            {
                instance.GetComponent<NetworkObject>().Spawn(true);
            }

            dropped = instance.GetComponent<IDroppedItemForm>();
            dropped.InitializeDropped(item, instanceId);
            return DropResult.Success;
        }

        // A little in front of the character, on the floor. If a wall is in the way the item goes down at their feet instead of behind it.
        private static Vector3 DropPoint(Character character)
        {
            int solid = LayerMask.GetMask("World", "Walkable");
            Vector3 feet = character.transform.position + Vector3.up * SearchHeight;
            Vector3 origin = character.transform.position + Vector3.up * ShoulderHeight;
            Vector3 target = character.transform.position + character.transform.forward * ReachInFront + Vector3.up * SearchHeight;

            Vector3 toTarget = target - origin;
            if (Physics.Raycast(origin, toTarget.normalized, toTarget.magnitude, solid, QueryTriggerInteraction.Ignore))
            {
                target = feet;
            }

            return Physics.Raycast(target, Vector3.down, out RaycastHit ground, SearchDepth, solid, QueryTriggerInteraction.Ignore)
                ? ground.point + Vector3.up * 0.02f
                : target;
        }
    }
}
