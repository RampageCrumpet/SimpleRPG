using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace SimpleRPG.LevelGeneration
{
    /// <summary>
    /// Builds a finished <see cref="Layout"/> in the scene. It makes no decisions: every room already has its place, and the
    /// spawner only instantiates prefabs there and closes the doors the layout says are sealed.
    /// </summary>
    public class LevelSpawner
    {
        private readonly IReadOnlyList<RoomBlueprint> prefabs;
        private readonly float cellSize;
        private readonly float floorHeight;

        /// <summary>
        /// Called with each room's root GameObject right after it is instantiated, before any other code can see it.
        /// </summary>
        public Action<GameObject> RoomInstantiated { get; set; }

        /// <summary>
        /// Called once for each lock after its doors have been locked, so the game can fit whatever opens it.
        /// </summary>
        public Action<LockedDoor> LockApplied { get; set; }

        /// <summary>
        /// Called once for each key with the room it lies in, so the game can put the key item there.
        /// </summary>
        public Action<int, RoomBlueprint> KeyRoomAssigned { get; set; }

        /// <param name="prefabs"> The room prefabs, in the order of <see cref="RoomDefinition.Index"/>.</param>
        /// <param name="cellSize"> The size of one world cell in unity units.</param>
        /// <param name="floorHeight"> The height of one floor in unity units. Floor 0 is at the prefab's own height.</param>
        public LevelSpawner(IReadOnlyList<RoomBlueprint> prefabs, float cellSize, float floorHeight = 0f)
        {
            this.floorHeight = floorHeight;
            this.prefabs = prefabs;
            this.cellSize = cellSize;
        }

        /// <summary>
        /// Instantiates every room in a layout and seals its sealed doors.
        /// </summary>
        /// <param name="layout"> The layout to build.</param>
        /// <param name="parent"> What to parent the rooms to. May be null.</param>
        /// <returns> The instantiated rooms, in the order of <see cref="Layout.Rooms"/>.</returns>
        public IReadOnlyList<RoomBlueprint> Spawn(Layout layout, Transform parent)
        {
            var instances = new List<RoomBlueprint>();

            foreach (PlacedRoom room in layout.Rooms)
            {
                RoomBlueprint instance = Instantiate(room, parent);
                instances.Add(instance);
                RoomInstantiated?.Invoke(instance.gameObject);
            }

            foreach (PlacedDoor door in layout.SealedDoors)
            {
                Seal(door, instances[door.Room.Id]);
            }

            // Doors that join flush share a plane, and each room draws its own door there: the lower-numbered room's draws the pair.
            foreach ((PlacedDoor first, PlacedDoor second) in LockSolver.Joins(layout.Rooms, layout.SealedDoors))
            {
                if (first.LengthCells == 0)
                {
                    HideDoor(second, instances[second.Room.Id]);
                }
            }

            foreach (LockedDoor locked in layout.Locks)
            {
                Lock(locked.Door, instances[locked.Door.Room.Id]);
                Lock(locked.Mate, instances[locked.Mate.Room.Id]);
                LockApplied?.Invoke(locked);
                KeyRoomAssigned?.Invoke(locked.KeyId, instances[locked.KeyRoom.Id]);
            }

            return instances;
        }

        private static void HideDoor(PlacedDoor door, RoomBlueprint instance)
        {
            ConnectionBlueprint connection = instance.Connections.ElementAtOrDefault(door.Index);
            if (connection == null || !connection.TryHideDoor())
            {
                Debug.LogWarning($"{door} joins flush but '{instance.name}' has no door object for door {door.Index}, so the two doors will z-fight.", instance);
            }
        }

        private static void Lock(PlacedDoor door, RoomBlueprint instance)
        {
            ConnectionBlueprint connection = instance.Connections.ElementAtOrDefault(door.Index);
            if (connection == null || !connection.TryLock())
            {
                Debug.LogWarning($"{door} is locked but '{instance.name}' has no lock object for door {door.Index}, so it will look open.", instance);
            }
        }

        private RoomBlueprint Instantiate(PlacedRoom room, Transform parent)
        {
            RoomBlueprint prefab = prefabs[room.Definition.Index];
            Quaternion rotation = Quaternion.Euler(0, 90 * room.Turns, 0);

            // The room's lowest corner belongs on its tile position, but the prefab root may sit above/right of that corner by OriginOffset cells.
            Vector3 rotatedOriginOffset = rotation * new Vector3(room.Definition.OriginOffset.x * cellSize, 0, room.Definition.OriginOffset.y * cellSize);
            Vector3 worldPosition = new Vector3(
                room.Position.x * cellSize - rotatedOriginOffset.x,
                prefab.transform.position.y + room.Floor * floorHeight,
                room.Position.y * cellSize - rotatedOriginOffset.z);

            return UnityEngine.Object.Instantiate(prefab, worldPosition, rotation, parent);
        }

        private static void Seal(PlacedDoor door, RoomBlueprint instance)
        {
            // The instance keeps the prefab's door order, which is the order the doors were baked in.
            ConnectionBlueprint connection = instance.Connections.ElementAtOrDefault(door.Index);
            if (connection == null || !connection.TrySeal())
            {
                Debug.LogError($"Cannot seal {door}: '{instance.name}' has no sealing object for door {door.Index}.", instance);
            }
        }
    }
}
