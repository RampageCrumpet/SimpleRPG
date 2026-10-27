using SimpleRPG.Audio;
using System;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

namespace SimpleRPG.InventorySystem
{
    /// <summary>
    /// The datacontainer for an inventory.
    /// Authority model: the server owns the contents. Only the server mutates the grid (the Add/Remove methods refuse to run on a client)
    /// and it publishes the contents as a <see cref="NetworkList{T}"/> of <see cref="InventoryEntry"/>, which late joiners receive
    /// as part of the spawn state. Clients change inventories by calling <see cref="RequestMoveItem"/>, which the server validates.
    /// When the object isn't spawned on a network (edit mode tests, offline use) the inventory behaves as a plain local container.
    /// </summary>
    public class Inventory : NetworkBehaviour
    {
        /// <summary>
        /// The total size of our inventory grid.
        /// </summary>
        [field: SerializeField]
        [Tooltip("The total size of the inventory grid.")]
        public Vector2Int InventorySize {get; private set; }

        /// <summary>
        /// If greater than zero, any client whose character is within this many units can move items in and out of this inventory
        /// (a chest). If zero only the owning client can (a player's inventory). Enforced by the server.
        /// </summary>
        public float SharedAccessRange { get; set; }

        /// <summary>
        /// Raised on every machine after the contents of the inventory change, so views can redraw.
        /// </summary>
        public event Action Changed;

        /// <summary>
        /// The replicated contents. Everyone reads, only the server writes.
        /// </summary>
        private readonly NetworkList<InventoryEntry> entries = new NetworkList<InventoryEntry>();

        /// <summary>
        /// The contents of our inventory grid.
        /// </summary>
        Item[,] inventory;

        /// <summary>
        /// The items in this inventory by the ID that identifies them across machines.
        /// </summary>
        private Dictionary<uint, Item> itemsById = new Dictionary<uint, Item>();

        private readonly Dictionary<Item, uint> idsByItem = new Dictionary<Item, uint>();

        /// <summary>
        /// Source of unique item instance IDs. Only meaningful on the server (or offline).
        /// </summary>
        private static uint lastInstanceId;

        /// <summary>
        /// True if this machine may change the contents directly: it's the server, or the inventory isn't networked.
        /// </summary>
        private bool CanMutate => !IsSpawned || IsServer;

        public void Awake()
        {
            inventory = new Item[InventorySize.x, InventorySize.y];
        }

        public override void OnNetworkSpawn()
        {
            if (!IsServer)
            {
                // Late joiners receive the full list with the spawn but no change event, so build from it now.
                entries.OnListChanged += HandleEntriesChanged;
                RebuildFromEntries();
            }
        }

        public override void OnNetworkDespawn()
        {
            entries.OnListChanged -= HandleEntriesChanged;
        }

        private void HandleEntriesChanged(NetworkListEvent<InventoryEntry> changeEvent)
        {
            RebuildFromEntries();
        }

        /// <summary>
        /// Replaces the local contents with what the server replicated. Items that are still present keep their instance
        /// so views holding a reference to them stay valid.
        /// </summary>
        private void RebuildFromEntries()
        {
            Array.Clear(inventory, 0, inventory.Length);
            var previousItems = itemsById;
            itemsById = new Dictionary<uint, Item>();
            idsByItem.Clear();

            ItemRegistry registry = ItemRegistry.Instance;

            foreach (InventoryEntry entry in entries)
            {
                Item item;
                if (!previousItems.Remove(entry.InstanceId, out item) || (registry != null && registry.GetId(item) != entry.ItemId))
                {
                    Item template = registry != null ? registry.GetItem(entry.ItemId) : null;
                    if (template == null)
                    {
                        Debug.LogError($"{gameObject.name}: no registered item with ID {entry.ItemId}; add it to the ItemRegistry.");
                        continue;
                    }

                    item = template.CloneInstance();
                }

                itemsById[entry.InstanceId] = item;
                idsByItem[item] = entry.InstanceId;
                FillCells(item, entry.Position);
            }

            // Anything left over was removed or replaced by the server.
            foreach (Item staleItem in previousItems.Values)
            {
                Destroy(staleItem);
            }

            Changed?.Invoke();
        }

        /// <summary>
        /// Marks the grid cells an item covers.
        /// </summary>
        private void FillCells(Item item, Vector2Int location)
        {
            for(int x = location.x; x < location.x + item.ItemSize.x; x++)
            {
                for(int y = location.y; y < location.y + item.ItemSize.y; y++)
                {
                    inventory[x,y] = item;
                }
            }
        }

        /// <summary>
        /// Adds an item to the inventory at a given location and publishes it. Authority only.
        /// </summary>
        /// <param name="item"> The <see cref="Item"/> we want to add.</param>
        /// <param name="location"> The location within the inventory to add the item.</param>
        /// <param name="instanceId"> The existing ID of the item if it's moving from another inventory, 0 to assign a new one.</param>
        private void PlaceItem(Item item, Vector2Int location, uint instanceId = 0)
        {
            if (instanceId == 0)
            {
                instanceId = ++lastInstanceId;
            }

            FillCells(item, location);
            itemsById[instanceId] = item;
            idsByItem[item] = instanceId;

            if (IsSpawned)
            {
                entries.Add(new InventoryEntry
                {
                    InstanceId = instanceId,
                    ItemId = ItemRegistry.Instance.GetId(item),
                    Position = location
                });
            }

            Changed?.Invoke();
        }

        /// <summary>
        /// Remove an item from the inventory. Authority only.
        /// </summary>
        /// <param name="item"> The <see cref="Item"/> to remove from the inventory.</param>
        public void RemoveItem(Item item)
        {
            if (!CanMutate)
            {
                Debug.LogError("Only the server can remove items; use RequestMoveItem from a client.");
                return;
            }

            if (RemoveItemInternal(item))
            {
                Changed?.Invoke();
            }
        }

        private bool RemoveItemInternal(Item item)
        {
            for(int x = 0; x < inventory.GetLength(0); x++)
            {
                for(int y = 0; y < inventory.GetLength(1); y++)
                {
                    if (inventory[x,y] == item)
                    {
                        inventory[x,y] = null;
                    }
                }
            }

            if (!idsByItem.Remove(item, out uint instanceId))
            {
                return false;
            }

            itemsById.Remove(instanceId);

            if (IsSpawned)
            {
                for (int i = 0; i < entries.Count; i++)
                {
                    if (entries[i].InstanceId == instanceId)
                    {
                        entries.RemoveAt(i);
                        break;
                    }
                }
            }

            return true;
        }

        /// <summary>
        /// Adds an item to the given location in the inventory. Authority only.
        /// </summary>
        /// <param name="item"> The item we want to place.</param>
        /// <param name="location"> The location we want to place the item.</param>
        /// <returns> Returns true if the item can be placed, false if it cant</returns>
        public bool AddItem(Item item, Vector2Int location)
        {
            if (!CanPlaceAsAuthority(item, location))
            {
                return false;
            }

            PlaceItem(item, location);
            return true;
        }

        /// <summary>
        /// Place an item at the first available location in the inventory. Authority only.
        /// </summary>
        /// <param name="item"> The item to be placed.</param>
        /// <returns> True if we suceeded in placing the item, false otherwise.</returns>
        public bool AddItem(Item item)
        {
            if (TryFindFreeLocation(item, out Vector2Int location) && CanPlaceAsAuthority(item, location))
            {
                PlaceItem(item, location);
                return true;
            }

            // We were not able to place the item in the inventory.
            return false;
        }

        /// <summary>
        /// Creates a new instance of an item and places it in the inventory at the first available location. Authority only.
        /// This is useful for when we want to create a new item from a loot table and place it in the inventory.
        /// </summary>
        /// <param name="item"> The item to be placed.</param>
        /// <returns> True if we suceeded in placing the item, false otherwise.</returns>
        public bool AddItemInstance(Item item)
        {
            if (TryFindFreeLocation(item, out Vector2Int location) && CanPlaceAsAuthority(item, location))
            {
                PlaceItem(item.CloneInstance(), location);
                return true;
            }

            // We were not able to place the item in the inventory.
            return false;
        }

        /// <summary>
        /// Checks that this machine may add the item and that networked inventories know the item.
        /// </summary>
        private bool CanPlaceAsAuthority(Item item, Vector2Int location)
        {
            if (!CanMutate)
            {
                Debug.LogError("Only the server can add items; use RequestMoveItem from a client.");
                return false;
            }

            if (IsSpawned && (ItemRegistry.Instance == null || ItemRegistry.Instance.GetId(item) == 0))
            {
                Debug.LogError($"Item '{item.ItemName}' isn't in the ItemRegistry, so it can't be replicated. Add it to the registry.");
                return false;
            }

            return ValidateItemPlacement(item, location);
        }

        private bool TryFindFreeLocation(Item item, out Vector2Int location)
        {
            return TryFindFreeLocation(item, null, out location);
        }

        /// <summary>
        /// Finds the first location the item fits, treating the cells of <paramref name="ignored"/> (an item about to leave) as free.
        /// </summary>
        private bool TryFindFreeLocation(Item item, Item ignored, out Vector2Int location)
        {
            for (int x = 0; x < InventorySize.x; x++)
            {
                for (int y = 0; y < InventorySize.y; y++)
                {
                    if ((inventory[x, y] == null || inventory[x, y] == ignored) && ValidateItemPlacement(item, new Vector2Int(x, y), ignored))
                    {
                        location = new Vector2Int(x, y);
                        return true;
                    }
                }
            }

            location = default;
            return false;
        }

        /// <summary>
        /// Validates an items placement ensuring that it's within the boundaries of the inventory and doesn't overlap other items.
        /// </summary>
        /// <param name="item"> The item we're trying to place.</param>
        /// <param name="location"> The location in the <see cref="Inventory"/> we're trying to place the item at.</param>
        /// <returns> False if the item is outside the bounds of the inventory or overlaps another item, true otherwise.</returns>
        public bool ValidateItemPlacement(Item item, Vector2Int location)
        {
            return ValidateItemPlacement(item, location, null);
        }

        private bool ValidateItemPlacement(Item item, Vector2Int location, Item ignored)
        {
            for (int x = location.x; x < location.x + item.ItemSize.x; x++)
            {
                for (int y = location.y; y < location.y + item.ItemSize.y; y++)
                {
                    // We're trying to place the item outside of the boundaries of the inventory and shouldn't be allowed to do so.
                    if (x >= InventorySize.x || x < 0 || y >= InventorySize.y || y < 0)
                        return false;

                    // A slot the item would have filled is occupied by a different.
                    if (inventory[x, y] != null && inventory[x, y] != item && inventory[x, y] != ignored)
                        return false;
                }
            }

            // No problems were found with the items placement. It must be a valid placement.
            return true;
        }

        /// <summary>
        /// Moves an item that is in this inventory to a location in <paramref name="destination"/> (which may be this inventory).
        /// Runs on whichever machine has authority; the move is validated first so a rejected move changes nothing.
        /// </summary>
        /// <returns>True if the item was moved.</returns>
        public bool TryMoveItem(Item item, Inventory destination, Vector2Int location)
        {
            MoveResult result = InventoryMoveController.TryMove(this, item, destination, location);
            if (result == MoveResult.NotAuthority)
            {
                Debug.LogError("Only the server can move items; use RequestMoveItem from a client.");
            }

            return result == MoveResult.Success;
        }

        /// <summary>
        /// True if this machine may change the contents directly: it's the server, or the inventory isn't networked.
        /// </summary>
        public bool CanMutateContents => CanMutate;

        /// <summary>
        /// Is the item currently in this inventory?
        /// </summary>
        public bool Contains(Item item)
        {
            return item != null && idsByItem.ContainsKey(item);
        }

        /// <summary>
        /// Gets the ID that identifies this item instance. It stays the same when the item moves between inventories.
        /// </summary>
        /// <returns>The ID, or an invalid ID if the item is not in this inventory.</returns>
        public SimpleRPG.Gameplay.ItemInstanceId GetInstanceId(Item item)
        {
            return item != null && idsByItem.TryGetValue(item, out uint id) ? new SimpleRPG.Gameplay.ItemInstanceId(id) : default;
        }

        /// <summary>
        /// Can this inventory publish the item to clients? Always true when the inventory isn't networked.
        /// </summary>
        public bool CanReplicate(Item item)
        {
            return !IsSpawned || (ItemRegistry.Instance != null && ItemRegistry.Instance.GetId(item) != 0);
        }

        /// <summary>
        /// Performs a move that <see cref="InventoryMoveController"/> has already validated.
        /// </summary>
        internal void CommitMove(Item item, Inventory destination, Vector2Int location)
        {
            uint instanceId = idsByItem[item];
            RemoveItemInternal(item);
            destination.PlaceItem(item, location, instanceId);

            if (destination != this)
            {
                Changed?.Invoke();
            }
        }

        /// <summary>
        /// Finds the item in this inventory with the given instance ID.
        /// </summary>
        public bool TryGetItem(uint instanceId, out Item item)
        {
            return itemsById.TryGetValue(instanceId, out item);
        }

        /// <summary>
        /// Checks whether an item would fit somewhere in this inventory, optionally once <paramref name="leaving"/> (an item in this inventory
        /// that is about to go) has made room. Changes nothing.
        /// </summary>
        internal bool TryFindRoom(Item item, Item leaving, out Vector2Int location)
        {
            return TryFindFreeLocation(item, leaving, out location);
        }

        /// <summary>
        /// Takes an item out of the inventory without destroying it, returning the instance ID it had so it can be placed elsewhere
        /// (an equipment slot) and come back as the same item. Authority only.
        /// </summary>
        internal bool Detach(Item item, out uint instanceId, out Vector2Int location)
        {
            instanceId = 0;
            location = default;
            if (!CanMutate || !idsByItem.TryGetValue(item, out instanceId))
            {
                return false;
            }

            foreach ((Item stored, Vector2Int position) in GetItems())
            {
                if (stored == item)
                {
                    location = position;
                    break;
                }
            }

            RemoveItemInternal(item);
            Changed?.Invoke();
            return true;
        }

        /// <summary>
        /// Puts a detached item back at a location the caller has validated, under the instance ID it had. Authority only.
        /// </summary>
        internal void Attach(Item item, Vector2Int location, uint instanceId)
        {
            PlaceItem(item, location, instanceId);
        }

        /// <summary>
        /// Asks for an item to be moved from this inventory to <paramref name="destination"/>. On the server this happens immediately;
        /// on a client the request is sent to the server, which validates it, and the result arrives through <see cref="Changed"/>.
        /// </summary>
        /// <returns>True if the move happened or the request was sent; false if it can't possibly succeed.</returns>
        public bool RequestMoveItem(Item item, Inventory destination, Vector2Int location)
        {
            if (destination == null)
            {
                return false;
            }

            if (CanMutate && destination.CanMutate)
            {
                bool moved = TryMoveItem(item, destination, location);
                if (moved) PlayMoveSound(item, destination);
                return moved;
            }

            if (destination == null || item == null || !idsByItem.TryGetValue(item, out uint instanceId))
            {
                return false;
            }

            MoveItemRpc(instanceId, new NetworkBehaviourReference(destination), location);

            // The player's own drop is heard at once; if the server refuses it the item simply snaps back.
            PlayMoveSound(item, destination);
            return true;
        }

        // Sound of the local player completing a move: carried to another container is a placement, shuffled inside one is a move.
        private void PlayMoveSound(Item item, Inventory destination)
        {
            if (!IsServer || IsHost || !IsSpawned)
            {
                InventoryAudio.Play(item, destination == this ? InventoryAudioAction.Move : InventoryAudioAction.Place);
            }
        }

        [Rpc(SendTo.Server)]
        private void MoveItemRpc(uint instanceId, NetworkBehaviourReference destinationReference, Vector2Int location, RpcParams rpcParams = default)
        {
            ulong senderClientId = rpcParams.Receive.SenderClientId;

            if (!itemsById.TryGetValue(instanceId, out Item item) ||
                !destinationReference.TryGet(out Inventory destination) ||
                !CanBeAccessedBy(senderClientId) ||
                !destination.CanBeAccessedBy(senderClientId))
            {
                return;
            }

            TryMoveItem(item, destination, location);
        }

        /// <summary>
        /// Can the given client move items in or out of this inventory?
        /// </summary>
        private bool CanBeAccessedBy(ulong clientId)
        {
            // A dead character cannot handle items, whether its own or a container's.
            foreach (NetworkObject deadCheck in NetworkManager.SpawnManager.GetClientOwnedObjects(clientId))
            {
                if (deadCheck.TryGetComponent(out Character dead) && !dead.IsAlive)
                {
                    return false;
                }
            }

            if (OwnerClientId == clientId && SharedAccessRange <= 0f)
            {
                return true;
            }

            if (SharedAccessRange <= 0f)
            {
                return false;
            }

            // Characters are spawned with ownership rather than as player objects, so look at the characters the client owns.
            foreach (NetworkObject owned in NetworkManager.SpawnManager.GetClientOwnedObjects(clientId))
            {
                if (owned.TryGetComponent(out Character _) &&
                    Vector3.Distance(owned.transform.position, transform.position) <= SharedAccessRange)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Gets all items in the inventory along with their positions.
        /// </summary>
        /// <returns> An enumerable of tuples containing the item and its position.</returns>
        public IEnumerable<(Item item, Vector2Int position)> GetItems()
        {
            var addedItems = new HashSet<Item>();

            // By starting at 0, 0 we can ensure we always find the origin location of the item first.
            for (int x = 0; x < inventory.GetLength(0); x++)
            {
                for (int y = 0; y < inventory.GetLength(1); y++)
                {
                    var item = inventory[x, y];
                    if (item != null && !addedItems.Contains(item))
                    {
                        yield return (item, new Vector2Int(x, y));
                        addedItems.Add(item);
                    }
                }
            }
        }
    }
}
