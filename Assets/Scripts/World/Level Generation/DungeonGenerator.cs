using System.Collections.Generic;
using System.Linq;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.UIElements;
using static UnityEditor.FilePathAttribute;

namespace LevelGeneration
{
    public class DungeonGenerator
    {
        /// <summary>
        /// The seed we want to use for generating our level.
        /// </summary>
        public int Seed { get; private set; }

        /// <summary>
        /// The connections that have not yet had a room attached to them.
        /// </summary>
        private List<Connection> openConnections = new List<Connection>();

        /// <summary>
        /// Connections with a room attached to them.
        /// </summary>
        private List<Connection> closedConnections = new List<Connection>();

        /// <summary>
        /// A map for each tile to the world grid. Each room can span multiple tiles.
        /// </summary>
        private Dictionary<Vector2Int, Room> worldGrid;

        /// <summary>
        /// A complete list of rooms we can draw from.
        /// </summary>
        private List<RoomBlueprint> roomBlueprints = new List<RoomBlueprint>();

        /// <summary>
        /// A list of all of the rooms we've placed.
        /// </summary>
        private List<Room> placedRooms = new List<Room>();

        /// <summary>
        /// The random number generator we want to use for our level generation.
        /// </summary>
        private System.Random randomNumberGenerator;

        /// <summary>
        /// The size of each cell in unity units.
        /// </summary>
        private float cellSize;

        /// <summary>
        /// Randomly generate a world given a seed and a size.
        /// </summary>
        /// <param name="seed"> The seed we want to use for our random generator.</param>
        /// <param name="worldSize"> The size of each individual "tile" that can have something placed on it in this world.</param>
        public DungeonGenerator(int seed, List<RoomBlueprint> roomBlueprints, float cellSize)
        {
            Seed = seed;
            randomNumberGenerator = new System.Random(Seed);
            worldGrid = new Dictionary<Vector2Int, Room>();
            this.cellSize = cellSize;
            this.roomBlueprints = roomBlueprints;
        }

        /// <summary>
        /// Selects a random room applicable to the connection.
        /// </summary>
        /// <param name="connection"> Picks a room at random from our list of blueprints and creates a room for it.</param>
        /// <returns> Returns a random room..</returns>
        private Room SelectRandomRoom(Connection connection)
        {
            return new Room(roomBlueprints[randomNumberGenerator.Next(roomBlueprints.Count)]);
        }

        /// <summary>
        /// Validates that a room can acutally be placed at the given location.
        /// </summary>
        /// <param name="room"> The room we want to check to see if we can place.</param>
        /// <param name="location"> The tile location of the bottom left tile of this room.</param>
        /// <returns>True if the room can be placed here without issue, false is placing the room here would close off rooms or place the room outside of the map.</returns>
        bool ValidateRoomPlacement(Room room, Vector2Int location)
        {
            for (int x = 0; x < room.Size.x; x++)
            {
                for(int y = 0; y < room.Size.y; y++)
                {
                    Vector2Int targetTileLocation = new Vector2Int(x, y) + location + room.RoomOrigin;

                    if (worldGrid.ContainsKey(targetTileLocation))
                    {
                        return false;
                    }
                }
            }

            //// Ensure that none of the placement locations are already occupied.
            //for (int x = room.RoomOrigin.x; x < room.RoomOrigin.x + room.Size.x; x++)
            //{
            //    for (int y = room.RoomOrigin.y; y < room.RoomOrigin.y + room.Size.y; y++)
            //    {
            //        Vector2Int localTile = new Vector2Int(x, y);
            //        Vector2Int worldTile = localTile + location;

            //        if (worldGrid.ContainsKey(worldTile))
            //        {
            //               return false;
            //        }
            //    }
            //}

            // If the new connections are not valid the room placement is not valid.
            if(!ValidateNewConnections(room))
            {
                return false;
            }

            // If the new room will invalidate an existing connection it's placement is not valid.
            if(!ValidateExistingConnections(room))
            {
                return false;
            }

            // If no rules are broken the room must be valid.
            return true;
        }

        /// <summary>
        /// Validates that the new connections for a recently placed room will be able to connect to an existing connection or will be facing empty space.
        /// </summary>
        /// <returns> True if none of the new connections will be placed facing a wall.</returns>
        private bool ValidateNewConnections(Room room)
        {
            // Ensure that no new connection will be closed off by pointing at a wall.
            foreach (Connection connection in room.Connections)
            {
                //If the spot is filled and doesn't have an open connection it'll be blocked off by a wall.
                if (worldGrid.ContainsKey(connection.GlobalTilePosition))
                {
                    int connectionTileSize = Mathf.RoundToInt(connection.ConnectionLength / cellSize);

                    if (!openConnections.Any(x => x.GlobalTilePosition + (x.Forward * connectionTileSize) == connection.GlobalTilePosition))
                    {
                        return false;
                    }
                }
            }

            // If no issues were found with the rooms placement it must be a valid placement.
            return true;
        }

        /// <summary>
        /// Validates that a given room will not cut off any existing connections if placed at it's current location.
        /// </summary>
        /// <param name="room"> The room we want to place.</param>
        /// <returns> True if the room's placement is valid, false otherwise.</returns>
        private bool ValidateExistingConnections(Room room)
        {
            // Ensure that no new connection will be closed off by pointing at a wall.
            foreach (Connection connection in openConnections)
            {
                int connectionTileSize = Mathf.RoundToInt(connection.ConnectionLength / cellSize);
                Vector2Int openConnectionTargetTile = connection.GlobalTilePosition + (connection.Forward * connectionTileSize);

                // Calculate the min and max bounds of the new room in tile space
                Vector2Int min = room.Position + room.RoomOrigin;
                Vector2Int max = room.Position + room.RoomOrigin + room.Size;

                // Check if the open connection's target tile is within the new room's bounds
                bool connectionIsWithinRoomBounds =
                    openConnectionTargetTile.x >= min.x && openConnectionTargetTile.x < max.x &&
                    openConnectionTargetTile.y >= min.y && openConnectionTargetTile.y < max.y;

                if (connectionIsWithinRoomBounds)
                {
                    // Check if any of the new room's connections, when placed at 'location', match this open connection's global tile position
                    bool matchesNewRoomConnection = room.Connections.Any(roomConnection =>
                        (roomConnection.GlobalTilePosition) == openConnectionTargetTile
                    );

                    if (!matchesNewRoomConnection)
                    {
                        // The connection would be blocked by a wall, so placement is invalid
                        return false;
                    }
                }

            }

            // If no issues were found with the rooms placement it must be a valid placement.
            return true;
        }

        /// <summary>
        /// Counts the number of open connections that would exist after this room is placed.
        /// </summary>
        /// <param name="room"> The room we want to place.</param>
        /// <param name="location"> The location we want to place the room.</param>
        /// <returns> Returns an integer representing the numbner of open connections that would exist after placing this room.</returns>
        int GetOpenConnectionsAfterRoomPlacement(Room room)
        {
            int changeInConnections = 0;

            // Find the count of all of the new connections that wont be closed off by pointing at an occupied tile.
            foreach (Connection connection in room.Connections)
            {
                int connectionTileSize = Mathf.RoundToInt(connection.ConnectionLength / cellSize);

                // Is there an open connection at this tile facing back?
                bool matchesExistingOpenConnection = openConnections.Any(openConnection =>
                    connection.GlobalTilePosition + (connection.Forward * connectionTileSize) == openConnection.GlobalTilePosition &&
                    connection.CanConnect(openConnection)
                );

                if (!matchesExistingOpenConnection)
                {
                    changeInConnections++;
                }

                ////If the new connection is not pointing into the world grid it'll be an open connection we can build off of so our number of connectiosn will increase.
                //if (!worldGrid.ContainsKey(connection.GlobalTilePosition + (connection.Forward * connectionTileSize)))
                //{
                //    changeInConnections++;
                //}
            }

            // Find the count of all of the existing connections that will be closed off by pointing at a space occupied by the new room.
            foreach (Connection connection in openConnections)
            {
                int connectionTileSize = Mathf.RoundToInt(connection.ConnectionLength / cellSize);
                Vector2Int connectionTarget = connection.GlobalTilePosition + (connection.Forward * connectionTileSize);

                // Calculate the min and max bounds of the room in tile space
                Vector2Int min = room.Position + room.RoomOrigin;
                Vector2Int max = room.Position + room.Size;

                // Check if the target tile is within the room's bounds
                bool connectionIsWithinRoomBounds = connectionTarget.x >= min.x && connectionTarget.x < max.x
                    && connectionTarget.y >= min.y && connectionTarget.y < max.y;

                if (connectionIsWithinRoomBounds)
                {
                    changeInConnections--;
                }
            }

            return openConnections.Count + changeInConnections;
        }

        /// <summary>
        /// Places a room at the given position on the grid.
        /// </summary>
        /// <param name="room"> The room to be placed.</param>
        /// <param name="position"> The tile location we want to place the rooms origin at.</param>
        /// <param name="worldParent">The parent we want to instatiate the room as a child of.</param>
        private void PlaceRoom(Room room, Vector2Int position, Transform worldParent)
        {
            // Move the room to it's final location.
            room.Position = position;

            // Mark the occupied grid cells
            for (int x = 0; x < room.Size.x; x++)
            {
                for (int y = 0; y < room.Size.y; y++)
                {
                    Vector2Int targetTileLocation = new Vector2Int(x, y) + room.RoomOrigin + position;

                    worldGrid.Add(targetTileLocation, room);
                }
            }

            // Add the new connections from the recently placed room
            foreach (Connection connection in room.Connections)
            {
                // Try to find a matching open connection
                Connection matchingConnection = openConnections.FirstOrDefault(openConnection =>
                {
                    //TODO: Ensure the connections are facing the opposite way.
                    return openConnection.GlobalTilePosition + openConnection.Forward == connection.GlobalTilePosition;
                });

                if (matchingConnection != null)
                {
                    openConnections.Remove(matchingConnection);
                    closedConnections.Add(matchingConnection);
                    closedConnections.Add(connection);
                }
                else
                {
                    openConnections.Add(connection);
                }
            }

            placedRooms.Add(room);
            InstantiateRoom(room, position);
        }

        private void InstantiateRoom(Room room, Vector2Int position)
        {
            // Instantiate the room at the correct world position and rotation
            Vector3 worldPlacementLocation = new Vector3(
                position.x * cellSize,
                room.RoomBlueprint.transform.position.y,
                position.y * cellSize
            );

            RoomBlueprint placedRoom = GameObject.Instantiate(
                room.RoomBlueprint,
                worldPlacementLocation,
                room.Rotation
            ).GetComponent<RoomBlueprint>();
        }

        /// <summary>
        /// Generates a level.
        /// </summary>
        /// <param name="minimumNumberOfRooms"> The minimum number of rooms this level will contain.</param>
        /// <param name="parentTransform"> The parent transform we want to attach th</param>
        public void GenerateLevel(int minimumNumberOfRooms, Transform parentTransform)
        {
            //Place a starting room to seed our dungeon.
            Room startingRoom = SelectRandomRoom(null);
            startingRoom.Rotation = Quaternion.identity;
            PlaceRoom(startingRoom, Vector2Int.zero, parentTransform);

            int roomPlacementsAttempted = 0;

            // Continue placing rooms while our room count hasn't been reached or we have open connections to fill.
            while (placedRooms.Count < minimumNumberOfRooms || openConnections.Count > 0)
            {
                // The connection we want to build off of.
                Connection openConnection = openConnections[randomNumberGenerator.Next(openConnections.Count - 1)];

                //TODO: We can pick a room using better logic than by pure chance.
                Room newRoom = SelectRandomRoom(openConnection);

                roomPlacementsAttempted++;

                var placementLocations = FindRoomPlacementLocations(newRoom, openConnection);
                foreach (PlacementLocation placementInformation in placementLocations)
                {
                    newRoom.Rotation = placementInformation.Rotation;
                    newRoom.Position = placementInformation.Position;

                    int openConnectionsAfterRoomPlacement = GetOpenConnectionsAfterRoomPlacement(newRoom);

                    // Try to place the room if we haven't placed enough rooms or if placing the room will reduce the total 
                    if ((placedRooms.Count < minimumNumberOfRooms && openConnectionsAfterRoomPlacement != 0) || (placedRooms.Count >= minimumNumberOfRooms && openConnectionsAfterRoomPlacement < openConnections.Count))
                    {

                        if (ValidateRoomPlacement(newRoom, placementInformation.Position))
                        {
                            roomPlacementsAttempted = 0;
                            Debug.Log("Placing room at " + placementInformation.Position);
                            PlaceRoom(newRoom, placementInformation.Position, parentTransform);
                            break;
                        }
                    }
                }

                // If we're just absolutely failing to place rooms we have something terribly wrong.
                if (roomPlacementsAttempted >= roomBlueprints.Count * 4 * 10)
                {
                    Debug.LogError("Level generation is failing to place a room");
                    return;
                }
            }
        }

        /// <summary>
        /// Finds all viable spots to place the room using the direction of the connection.
        /// </summary>
        /// <param name="room">The room we want to place.</param>
        /// <param name="connection">The connection we want to build off of.</param>
        /// <returns>List of possible placement locations (position and rotation).</returns>
        private List<PlacementLocation> FindRoomPlacementLocations(Room room, Connection connection)
        {
            List<PlacementLocation> placementLocations = new List<PlacementLocation>();
            Quaternion[] rotations = {
                Quaternion.identity,
                Quaternion.Euler(0, 90, 0),
                Quaternion.Euler(0, 180, 0),
                Quaternion.Euler(0, 270, 0)
            };

            // Randomize the order of the rotations array
            rotations = rotations.OrderBy(x => randomNumberGenerator.Next()).ToArray();

            foreach (Quaternion rotation in rotations)
            {
                // Rotate the room and update its connections
                room.Rotation = rotation;
                room.Position = Vector2Int.zero;

                foreach (Connection roomConnection in room.Connections)
                {
                    // Now, roomConnection.Forward and LocalTilePosition are already rotated
                    if (roomConnection.Forward == -connection.Forward)
                    {
                        //Calculate the connections thickness in tiles.
                        int connectionTileSize = Mathf.RoundToInt(connection.ConnectionLength / cellSize);

                        // Calculate where to place the room so that the rotated connection aligns with the open connection's tile
                        Vector2Int roomTargetLocation = (connection.GlobalTilePosition + (connection.Forward * connectionTileSize)) - roomConnection.GlobalTilePosition;

                        placementLocations.Add(new PlacementLocation
                        {
                            Position = roomTargetLocation,
                            Rotation = rotation
                        });
                    }
                }
            }

            return placementLocations;
        }

        private struct PlacementLocation
        {
            public Vector2Int Position;
            public Quaternion Rotation;
        }
    }
}
