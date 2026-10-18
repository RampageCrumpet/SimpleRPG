using System;
using Unity.Netcode;
using UnityEngine;

namespace SimpleRPG.InventorySystem
{
    /// <summary>
    /// One item in a replicated inventory: which item it is, where its origin cell is, and an ID that identifies this particular instance.
    /// </summary>
    public struct InventoryEntry : INetworkSerializable, IEquatable<InventoryEntry>
    {
        /// <summary>
        /// Identifies this item instance across inventories and machines. Assigned by the server, never 0.
        /// </summary>
        public uint InstanceId;

        /// <summary>
        /// The <see cref="ItemRegistry"/> ID of the item.
        /// </summary>
        public int ItemId;

        /// <summary>
        /// The inventory cell holding the item's origin (its smallest x/y cell).
        /// </summary>
        public Vector2Int Position;

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref InstanceId);
            serializer.SerializeValue(ref ItemId);
            serializer.SerializeValue(ref Position);
        }

        public bool Equals(InventoryEntry other)
        {
            return InstanceId == other.InstanceId && ItemId == other.ItemId && Position == other.Position;
        }

        public override bool Equals(object obj)
        {
            return obj is InventoryEntry other && Equals(other);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(InstanceId, ItemId, Position);
        }
    }
}
