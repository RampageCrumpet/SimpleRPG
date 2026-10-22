using SimpleRPG.Gameplay;
using SimpleRPG.InventorySystem;
using System;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using EntityId = SimpleRPG.Gameplay.EntityId;

namespace SimpleRPG
{
    /// <summary>
    /// What a character has equipped, one item per <see cref="EquipmentSlot"/>.
    /// Authority model: only the server changes equipment, through <see cref="Equip"/> and <see cref="Unequip"/>, and publishes it as a
    /// <see cref="NetworkList{T}"/> that every client (including late joiners) reads. Each change raises item equipped and unequipped events
    /// on the server so rules that depend on equipment can follow it.
    /// An equipped item is identified by the same instance ID the item has in an <see cref="Inventory"/>. Moving items between an inventory and
    /// equipment slots is not wired up yet; callers pass the item and its instance ID.
    /// </summary>
    [RequireComponent(typeof(Character))]
    public class Equipment : NetworkBehaviour
    {
        /// <summary>
        /// One equipped item as replicated.
        /// </summary>
        public struct EquippedEntry : INetworkSerializable, IEquatable<EquippedEntry>
        {
            public int Slot;
            public uint InstanceId;
            public int ItemId;

            public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
            {
                serializer.SerializeValue(ref Slot);
                serializer.SerializeValue(ref InstanceId);
                serializer.SerializeValue(ref ItemId);
            }

            public bool Equals(EquippedEntry other) => Slot == other.Slot && InstanceId == other.InstanceId && ItemId == other.ItemId;

            public override bool Equals(object obj) => obj is EquippedEntry other && Equals(other);

            public override int GetHashCode() => HashCode.Combine(Slot, InstanceId, ItemId);
        }

        /// <summary>
        /// Raised on every machine after equipment changes, so views can redraw.
        /// </summary>
        public event Action Changed;

        /// <summary>
        /// The charges left in one equipped item, as replicated to the owner.
        /// </summary>
        public struct ChargeEntry : INetworkSerializable, IEquatable<ChargeEntry>
        {
            public uint InstanceId;
            public int Charges;

            public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
            {
                serializer.SerializeValue(ref InstanceId);
                serializer.SerializeValue(ref Charges);
            }

            public bool Equals(ChargeEntry other) => InstanceId == other.InstanceId && Charges == other.Charges;

            public override bool Equals(object obj) => obj is ChargeEntry other && Equals(other);

            public override int GetHashCode() => HashCode.Combine(InstanceId, Charges);
        }

        private readonly NetworkList<EquippedEntry> entries = new NetworkList<EquippedEntry>();

        /// <summary>
        /// The charges of the equipped items that have a charge pool. Only the owning client can read it: other players have no business knowing
        /// how many charges your staff has left. Only the server writes it.
        /// </summary>
        private readonly NetworkList<ChargeEntry> charges = new NetworkList<ChargeEntry>(null, NetworkVariableReadPermission.Owner, NetworkVariableWritePermission.Server);

        private ItemStateStore itemStates;

        private readonly Dictionary<EquipmentSlot, Item> items = new Dictionary<EquipmentSlot, Item>();

        private readonly Dictionary<EquipmentSlot, uint> instanceIds = new Dictionary<EquipmentSlot, uint>();

        private Character character;

        /// <summary>
        /// The server's record of equipment, used to raise events. Null on a client.
        /// </summary>
        private EquipmentSet equipmentSet;

        private void Awake()
        {
            character = GetComponent<Character>();
        }

        public override void OnNetworkSpawn()
        {
            if (IsServer)
            {
                // Built from this object's own network ID: the Character component may not have finished spawning yet.
                equipmentSet = new EquipmentSet(new EntityId(NetworkObjectId), GameplayServices.Current.Events);

                // Charges are spent elsewhere; the owner is told when an equipped item's count changes.
                itemStates = GameplayServices.Current.ItemStates;
                itemStates.Changed += HandleItemStateChanged;
            }
            else
            {
                // Late joiners receive the full list with the spawn but no change event, so build from it now.
                entries.OnListChanged += HandleEntriesChanged;
                RebuildFromEntries();
            }
        }

        public override void OnNetworkDespawn()
        {
            entries.OnListChanged -= HandleEntriesChanged;
            equipmentSet = null;

            if (itemStates != null)
            {
                itemStates.Changed -= HandleItemStateChanged;
                itemStates = null;
            }
        }

        /// <summary>
        /// The charges left in the item in a slot, or -1 if the slot is empty, the item has no charge pool, or this machine is not allowed to know
        /// (charges are only sent to the owner).
        /// </summary>
        public int GetCharges(EquipmentSlot slot)
        {
            uint instanceId = GetInstanceId(slot);
            if (instanceId == 0)
            {
                return -1;
            }

            if (IsServer && itemStates != null)
            {
                return ItemCharges.Remaining(itemStates, new ItemInstanceId(instanceId));
            }

            for (int i = 0; i < charges.Count; i++)
            {
                if (charges[i].InstanceId == instanceId) return charges[i].Charges;
            }

            return -1;
        }

        private void HandleItemStateChanged(ItemInstanceId item, DefinitionId counter, int value)
        {
            if (counter == ItemCharges.PoolId && IsEquipped((uint)item.Value))
            {
                SetCharges((uint)item.Value, value);
            }
        }

        private void SetCharges(uint instanceId, int value)
        {
            for (int i = 0; i < charges.Count; i++)
            {
                if (charges[i].InstanceId == instanceId)
                {
                    charges[i] = new ChargeEntry { InstanceId = instanceId, Charges = value };
                    return;
                }
            }

            charges.Add(new ChargeEntry { InstanceId = instanceId, Charges = value });
        }

        private void RemoveCharges(uint instanceId)
        {
            for (int i = 0; i < charges.Count; i++)
            {
                if (charges[i].InstanceId == instanceId)
                {
                    charges.RemoveAt(i);
                    return;
                }
            }
        }

        /// <summary>
        /// Gets the item in a slot, or null if it is empty.
        /// </summary>
        public Item Get(EquipmentSlot slot) => items.TryGetValue(slot, out Item item) ? item : null;

        /// <summary>
        /// Gets the instance ID of the item in a slot, or 0 if it is empty.
        /// </summary>
        public uint GetInstanceId(EquipmentSlot slot) => instanceIds.TryGetValue(slot, out uint id) ? id : 0;

        /// <summary>
        /// Gets the instance ID of the item in a slot as a gameplay ID. Invalid if the slot is empty.
        /// </summary>
        public ItemInstanceId GetItemInstanceId(EquipmentSlot slot) => new ItemInstanceId(GetInstanceId(slot));

        /// <summary>
        /// Every equipped item with its slot and instance ID.
        /// </summary>
        public IEnumerable<(EquipmentSlot slot, uint instanceId, Item item)> Equipped
        {
            get
            {
                foreach (KeyValuePair<EquipmentSlot, Item> pair in items)
                {
                    yield return (pair.Key, instanceIds[pair.Key], pair.Value);
                }
            }
        }

        /// <summary>
        /// The weapon in the main hand, or null if the main hand is empty or holds something that isn't a weapon.
        /// </summary>
        public WeaponData MainHandWeapon => Get(EquipmentSlot.mainHand) as WeaponData;

        /// <summary>
        /// Puts an item in a slot, taking out what was there. Server only.
        /// </summary>
        /// <param name="slot">The slot. The consumable slot holds no equipment.</param>
        /// <param name="item">The item. It must be in the <see cref="ItemRegistry"/> so clients can find it.</param>
        /// <param name="instanceId">The item's instance ID, never 0. An item can only be in one slot at a time.</param>
        /// <returns>False if the request was refused.</returns>
        public bool Equip(EquipmentSlot slot, Item item, uint instanceId)
        {
            if (!IsServer || equipmentSet == null)
            {
                Debug.LogWarning(name + " ignored an equip request: equipment is server authoritative.");
                return false;
            }

            if (item == null || instanceId == 0 || slot == EquipmentSlot.Consumable)
            {
                return false;
            }

            ItemRegistry registry = ItemRegistry.Instance;
            int itemId = registry != null ? registry.GetId(item) : 0;
            if (itemId == 0)
            {
                Debug.LogError($"Item '{item.ItemName}' isn't in the ItemRegistry, so it can't be replicated. Add it to the registry.");
                return false;
            }

            // The change is recorded and published before the events go out, so a rule reacting to an event sees the new equipment.
            if (IsEquipped(instanceId))
            {
                return false;
            }

            if (items.ContainsKey(slot))
            {
                RemoveEntry(slot);
            }

            items[slot] = item;
            instanceIds[slot] = instanceId;
            entries.Add(new EquippedEntry { Slot = (int)slot, InstanceId = instanceId, ItemId = itemId });

            // An item with a charge pool brings its starting charges the first time it is ever equipped; after that the pool only goes down.
            ItemInstanceId instance = new ItemInstanceId(instanceId);
            ItemCharges.Ensure(itemStates, instance, item.StartingCharges);
            if (itemStates.TryGetCounter(instance, ItemCharges.PoolId, out int remaining))
            {
                SetCharges(instanceId, remaining);
            }

            Changed?.Invoke();

            equipmentSet.Equip((int)slot, new ItemInstanceId(instanceId));
            return true;
        }

        /// <summary>
        /// Takes the item out of a slot. Server only.
        /// </summary>
        /// <returns>False if the slot was empty or this isn't the server.</returns>
        public bool Unequip(EquipmentSlot slot)
        {
            if (!IsServer || equipmentSet == null)
            {
                Debug.LogWarning(name + " ignored an unequip request: equipment is server authoritative.");
                return false;
            }

            if (!items.ContainsKey(slot))
            {
                return false;
            }

            RemoveEntry(slot);
            Changed?.Invoke();

            equipmentSet.Unequip((int)slot);
            return true;
        }

        /// <summary>
        /// True if an item instance is already in any slot, including the one it is being equipped to.
        /// </summary>
        private bool IsEquipped(uint instanceId)
        {
            foreach (KeyValuePair<EquipmentSlot, uint> pair in instanceIds)
            {
                if (pair.Value == instanceId)
                {
                    return true;
                }
            }

            return false;
        }

        private void RemoveEntry(EquipmentSlot slot)
        {
            if (instanceIds.TryGetValue(slot, out uint removedInstance))
            {
                RemoveCharges(removedInstance);
            }

            items.Remove(slot);
            instanceIds.Remove(slot);

            for (int i = 0; i < entries.Count; i++)
            {
                if (entries[i].Slot == (int)slot)
                {
                    entries.RemoveAt(i);
                    break;
                }
            }
        }

        private void HandleEntriesChanged(NetworkListEvent<EquippedEntry> changeEvent)
        {
            RebuildFromEntries();
        }

        private void RebuildFromEntries()
        {
            items.Clear();
            instanceIds.Clear();

            ItemRegistry registry = ItemRegistry.Instance;
            foreach (EquippedEntry entry in entries)
            {
                Item item = registry != null ? registry.GetItem(entry.ItemId) : null;
                if (item == null)
                {
                    Debug.LogError($"{name}: no registered item with ID {entry.ItemId}; add it to the ItemRegistry.");
                    continue;
                }

                items[(EquipmentSlot)entry.Slot] = item;
                instanceIds[(EquipmentSlot)entry.Slot] = entry.InstanceId;
            }

            Changed?.Invoke();
        }
    }
}
