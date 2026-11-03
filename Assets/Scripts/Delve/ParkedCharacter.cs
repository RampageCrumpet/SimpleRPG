using SimpleRPG.InventorySystem;
using System.Collections.Generic;
using UnityEngine;

namespace SimpleRPG.Delve
{
    /// <summary>
    /// A player's character put aside while its player is away: what it carried and wore, where it stood and how hurt it was. When the player
    /// comes back the same character is made again from this, in the same place with the same items under the same instance IDs; if they do not
    /// come back in time what it held is left in a loot bag. Server only.
    /// <para>
    /// The character is despawned while it waits, so nothing can reach it: a dropped connection is neither a death nor a way to escape one, but
    /// the character is not in the world while it waits.
    /// </para>
    /// </summary>
    public sealed class ParkedCharacter
    {
        /// <summary>An item in the inventory grid.</summary>
        public readonly struct CarriedItem
        {
            public CarriedItem(Item item, Vector2Int location, uint instanceId)
            {
                Item = item;
                Location = location;
                InstanceId = instanceId;
            }

            public Item Item { get; }

            public Vector2Int Location { get; }

            public uint InstanceId { get; }
        }

        /// <summary>An item in an equipment slot.</summary>
        public readonly struct WornItem
        {
            public WornItem(EquipmentSlot slot, Item item, uint instanceId)
            {
                Slot = slot;
                Item = item;
                InstanceId = instanceId;
            }

            public EquipmentSlot Slot { get; }

            public Item Item { get; }

            public uint InstanceId { get; }
        }

        public ParkedCharacter(string playerId, ulong lastClientId, Vector3 position, Quaternion rotation, int health, bool wasDead, double parkedAt)
        {
            PlayerId = playerId;
            LastClientId = lastClientId;
            Position = position;
            Rotation = rotation;
            Health = health;
            WasDead = wasDead;
            ParkedAt = parkedAt;
        }

        public string PlayerId { get; }

        /// <summary>The client the player last connected as; it is only used to name them in a delve's result.</summary>
        public ulong LastClientId { get; }

        public Vector3 Position { get; }

        public Quaternion Rotation { get; }

        public int Health { get; }

        /// <summary>True if the character was already dead when its player left. It comes back dead.</summary>
        public bool WasDead { get; }

        /// <summary>When the player left, on the server clock.</summary>
        public double ParkedAt { get; }

        public List<CarriedItem> Carried { get; } = new List<CarriedItem>();

        public List<WornItem> Worn { get; } = new List<WornItem>();

        /// <summary>True if the character held nothing at all.</summary>
        public bool IsEmpty => Carried.Count == 0 && Worn.Count == 0;

        /// <summary>Notes everything in an inventory. Changes nothing.</summary>
        public void CaptureCarried(Inventory inventory)
        {
            if (inventory == null)
            {
                return;
            }

            foreach ((Item item, Vector2Int location) in inventory.GetItems())
            {
                Carried.Add(new CarriedItem(item, location, (uint)inventory.GetInstanceId(item).Value));
            }
        }

        /// <summary>Notes everything worn. Changes nothing.</summary>
        public void CaptureWorn(Equipment equipment)
        {
            if (equipment == null)
            {
                return;
            }

            foreach ((EquipmentSlot slot, uint instanceId, Item item) in equipment.Equipped)
            {
                Worn.Add(new WornItem(slot, item, instanceId));
            }
        }

        /// <summary>Puts the carried items back in an (empty) inventory where they were. Returns how many could not be placed.</summary>
        public int RestoreCarried(Inventory inventory)
        {
            int failed = 0;
            foreach (CarriedItem carried in Carried)
            {
                if (inventory != null && inventory.ValidateItemPlacement(carried.Item, carried.Location))
                {
                    inventory.PutBack(carried.Item, carried.Location, carried.InstanceId);
                }
                else
                {
                    failed++;
                }
            }

            return failed;
        }

        /// <summary>Puts the worn items back on. Returns how many could not be.</summary>
        public int RestoreWorn(Equipment equipment)
        {
            int failed = 0;
            foreach (WornItem worn in Worn)
            {
                if (equipment == null || !equipment.Equip(worn.Slot, worn.Item, worn.InstanceId))
                {
                    failed++;
                }
            }

            return failed;
        }

        /// <summary>
        /// Puts everything in <paramref name="bag"/>, as the same items under the same instance IDs. Returns how many did not fit; those are lost.
        /// </summary>
        public int MoveContentsTo(Inventory bag)
        {
            int left = 0;
            foreach (CarriedItem carried in Carried)
            {
                left += Place(bag, carried.Item, carried.InstanceId);
            }

            foreach (WornItem worn in Worn)
            {
                left += Place(bag, worn.Item, worn.InstanceId);
            }

            return left;
        }

        private static int Place(Inventory bag, Item item, uint instanceId)
        {
            if (bag != null && bag.TryFindRoom(item, out Vector2Int room))
            {
                bag.PutBack(item, room, instanceId);
                return 0;
            }

            return 1;
        }

        /// <summary>Takes note of a character that is about to leave the world: what it holds, where it stands, how hurt it is.</summary>
        public static ParkedCharacter Capture(Character character, string playerId, ulong clientId, double now)
        {
            var parked = new ParkedCharacter(playerId, clientId, character.transform.position, character.transform.rotation, character.Health, !character.IsAlive, now);
            if (character.IsAlive)
            {
                // A dead character already left everything in a loot bag, so there is nothing of theirs to keep.
                parked.CaptureCarried(character.GetComponent<Inventory>());
                parked.CaptureWorn(character.GetComponent<Equipment>());
            }

            return parked;
        }

        /// <summary>Makes a freshly spawned character what the parked one was. Returns how many items could not be put back.</summary>
        public int RestoreInto(Character character)
        {
            int failed = RestoreCarried(character.GetComponent<Inventory>()) + RestoreWorn(character.GetComponent<Equipment>());
            if (!WasDead)
            {
                character.RestoreHealthOnServer(Health);
            }

            return failed;
        }
    }
}
