using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace LevelGeneration
{
    /// <summary>
    /// Turns the packed values on room prefabs into the plain <see cref="RoomDefinition"/>s the layout generator reads.
    /// This is the only place prefab data crosses into the pure layout code.
    /// </summary>
    public static class RoomDefinitionBaker
    {
        /// <summary>
        /// Bakes every room in a roomset. Definition indexes match positions in <see cref="Roomset.RoomCollection"/>.
        /// </summary>
        public static List<RoomDefinition> Bake(Roomset roomset)
        {
            return roomset.RoomCollection
                .Select((room, index) => Bake(room, index, roomset.cellSize))
                .ToList();
        }

        /// <summary>
        /// Bakes one room.
        /// </summary>
        /// <param name="room"> A packed room.</param>
        /// <param name="index"> The room's position in its roomset.</param>
        /// <param name="cellSize"> The size of one world cell in unity units.</param>
        public static RoomDefinition Bake(RoomBlueprint room, int index, float cellSize)
        {
            var doors = new List<DoorDefinition>();

            foreach (ConnectionBlueprint connection in room.Connections)
            {
                // The packed location is the grid point the door stands on, measured from the room's lowest corner.
                Side side = GridMath.FromDirection(connection.Forward);
                int length = Mathf.Max(1, Mathf.RoundToInt(connection.ConnectionLength / cellSize));
                // Doors are centred on a grid line, so they cover an even number of cells; narrower than two cells still covers the pair either side.
                int width = Mathf.Max(2, 2 * Mathf.CeilToInt(connection.ConnectionWidth / cellSize / 2f));

                doors.Add(new DoorDefinition(doors.Count, connection.location, side, length, width, ConnectionType.IdFor(connection.Type)));
            }

            return new RoomDefinition(index, room.name, room.Size, room.OriginOffset, doors, room.SelectionWeight, room.MaxCount, room.Role);
        }
    }
}
