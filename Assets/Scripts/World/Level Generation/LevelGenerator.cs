using System.Collections.Generic;
using UnityEngine;
using LevelGeneration;
using System.Linq;
using UnityEngine.Networking.PlayerConnection;
using static UnityEditor.FilePathAttribute;
using UnityEditor.MemoryProfiler;
using JetBrains.Annotations;

namespace LevelGeneration
{
    public class LevelGenerator
    {
        /// <summary>
        /// The seed we want to use for generating our level.
        /// </summary>
        public int Seed { get; private set; }

        /// <summary>
        /// The connections that have not yet had a room attached to them.
        /// </summary>
        private List<ConnectionBlueprint> openConnections = new List<ConnectionBlueprint>();

        /// <summary>
        /// Connections with a room attached to them.
        /// </summary>
        private List<ConnectionBlueprint> closedConnections = new List<ConnectionBlueprint>();

        /// <summary>
        /// A map for each tile to the world grid. Each room can span multiple tiles.
        /// </summary>
        private Dictionary<Vector2Int, RoomBlueprint> worldGrid;

        /// <summary>
        /// A complete list of rooms we can draw from.
        /// </summary>
        private List<RoomBlueprint> roomBlueprints = new List<RoomBlueprint>();

        /// <summary>
        /// A list of all of the rooms we've placed.
        /// </summary>
        private List<RoomBlueprint> placedRooms = new List<RoomBlueprint>();

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
        public LevelGenerator(int seed, List<RoomBlueprint> roomBlueprints, float cellSize)
        {
            Seed = seed;
            randomNumberGenerator = new System.Random(Seed);
            worldGrid = new Dictionary<Vector2Int, RoomBlueprint>();
            this.cellSize = cellSize;
            this.roomBlueprints = roomBlueprints;
        }

        /// <summary>
        /// Generates a level.
        /// </summary>
        /// <param name="minimumNumberOfRooms"> The minimum number of rooms this level will contain.</param>
        /// <param name="parentTransform"> The parent transform we want to attach th</param>
        public void GenerateLevel(int minimumNumberOfRooms, Transform parentTransform)
        {
            //Place a starting room to seed our dungeon.
            RoomBlueprint startingRoom = SelectRandomRoom(null);
            GridLocation firstRoomPlacementLocation = new GridLocation()
            {
                Location = Vector2Int.zero,
                Rotation = Quaternion.identity
            };
            PlaceRoom(startingRoom, firstRoomPlacementLocation, parentTransform);


            int roomPlacementsAttempted = 0;

            // Continue placing rooms while our room count hasn't been reached or we have open connections to fill.
            while (placedRooms.Count < minimumNumberOfRooms || openConnections.Count > 0)
            {
                // The connection we want to build off of.
                ConnectionBlueprint openConnection = openConnections[randomNumberGenerator.Next(openConnections.Count - 1)];

                //TODO: We can pick a room using better logic than by pure chance.
                RoomBlueprint newRoom = SelectRandomRoom(openConnection);

                roomPlacementsAttempted++;

                foreach (GridLocation location in FindRoomPlacementLocations(newRoom, openConnection))
                {
                    int openConnectionsAfterRoomPlacement = GetOpenConnectionsAfterRoomPlacement(newRoom, location);

                    // Try to place the room if we haven't placed enough rooms or if placing the room will reduce the total 
                    if ((placedRooms.Count < minimumNumberOfRooms && openConnectionsAfterRoomPlacement != 0) || openConnectionsAfterRoomPlacement < openConnections.Count)
                    {

                        if (ValidateRoomPlacement(newRoom, location))
                        {
                            roomPlacementsAttempted = 0;
                            PlaceRoom(newRoom, location, parentTransform);
                            break;
                        }
                    }
                }

                // If we're just absolutely failing to place rooms we have something terribly wrong.
                if (roomPlacementsAttempted >= roomBlueprints.Count * 4 * 10)
                {
                    Debug.LogError("Level generation is failing to place a room");
                    break;
                }
            }
        }

        /// <summary>
        /// Selects a random room applicable to the connection.
        /// </summary>
        /// <param name="connection"> The connection we want to find a random room to build off of. If the connection is null any random room is selected.</param>
        /// <returns> Returns a random room with at least one connection facing towards the given connection.</returns>
        private RoomBlueprint SelectRandomRoom(ConnectionBlueprint connection)
        {
            if (connection == null)
            {
                return roomBlueprints[randomNumberGenerator.Next(roomBlueprints.Count)];
            }
            else
            {
                // Filter to only rooms that have connections facing the correct directions.
                List<RoomBlueprint> filteredRoomBlueprints = roomBlueprints; //.Where(x => x.connections.Any(y => y.CanConnect(connection))).ToList();

                if (filteredRoomBlueprints.Count == 0)
                {
                    Debug.LogError("No connection connects to " + connection.gameObject.name + " at " + (connection.location + connection.Forward) + ".");
                    // Clear the open connections to ensure that we don't continue generating.
                    openConnections.Clear();
                }

                return filteredRoomBlueprints.ElementAt(randomNumberGenerator.Next(0, filteredRoomBlueprints.Count));
            }
        }

        private void PlaceRoom(RoomBlueprint room, GridLocation placementInformation, Transform worldParent)
        {
            // Instantiate the room at the correct world position and rotation
            Vector3 worldPlacementLocation = new Vector3(
                placementInformation.Location.x * cellSize,
                room.gameObject.transform.position.y,
                placementInformation.Location.y * cellSize
            );

            RoomBlueprint placedRoom = GameObject.Instantiate(
                room.gameObject,
                worldPlacementLocation,
                placementInformation.Rotation,
                worldParent
            ).GetComponent<RoomBlueprint>();

            // Compute the bounding box of the rotated room
            GetRotatedRoomBounds(room.Size, placementInformation.Location, placementInformation.Rotation, out Vector2Int min, out Vector2Int max);

            // Mark the occupied grid cells
            for (int x = min.x; x < max.x; x++)
            {
                for (int y = min.y; y < max.y; y++)
                {
                    worldGrid.Add(new Vector2Int(x, y), placedRoom);
                }
            }

            // Add the new connections from the recently placed room
            foreach (ConnectionBlueprint connection in placedRoom.Connections)
            {
                Vector2Int rotatedLocation = RotatePoint(connection.location, Vector2Int.zero, placementInformation.Rotation) + placementInformation.Location;
                Vector2Int rotatedForward = RotatePoint(connection.Forward, Vector2Int.zero, placementInformation.Rotation);
                int connectionTileThickness = Mathf.RoundToInt(connection.ConnectionLength / cellSize);
                Vector2Int rotatedThickenedForward = rotatedForward * connectionTileThickness;
                Vector2Int connectionTarget = rotatedLocation + rotatedThickenedForward;

                connection.location = rotatedLocation;

                var matchingOpenConnection = openConnections.FirstOrDefault(openConnection =>
                {
                    int openConnThickness = Mathf.RoundToInt(openConnection.ConnectionLength / cellSize);
                    Vector2Int openConnTarget = openConnection.location + openConnection.Forward * openConnThickness;
                    return openConnTarget == rotatedLocation &&
                           openConnection.Forward == -rotatedForward;
                });

                if (matchingOpenConnection != null)
                {
                    closedConnections.Add(connection);
                    closedConnections.Add(matchingOpenConnection);
                    openConnections.Remove(matchingOpenConnection);
                }
                else if (!worldGrid.ContainsKey(connectionTarget))
                {
                    openConnections.Add(connection);
                }
                else
                {
                    closedConnections.Add(connection);
                    Debug.LogError("Connections facing a wall are being generated.");
                }
            }


            placedRooms.Add(placedRoom);
        }

        /// <summary>
        /// Validates that a room can acutally be placed at the given location.
        /// </summary>
        /// <param name="room"> The room we want to check to see if we can place.</param>
        /// <param name="roomPlacementLocation"> The location we want to check for valid placement.</param>
        /// <returns>True if the room can be placed here without issue, false is placing the room here would close off rooms or place the room outside of the map.</returns>
        bool ValidateRoomPlacement(RoomBlueprint room, GridLocation roomPlacementLocation)
        {
            // Rotate the room's size based on the provided rotation
            Vector2Int rotatedSize = RotatePoint(room.Size, Vector2Int.zero, roomPlacementLocation.Rotation);

            // Compute the bounding box of the rotated room
            GetRotatedRoomBounds(room.Size, roomPlacementLocation.Location, roomPlacementLocation.Rotation, out Vector2Int min, out Vector2Int max);

            // Ensure that none of the placement locations are already occupied.
            for (int x = min.x; x < max.x; x++)
            {
                for (int y = min.y; y < max.y; y++)
                {
                    Vector2Int localTile = new Vector2Int(x, y);
                    Vector2Int rotatedTile = RotatePoint(localTile, Vector2Int.zero, roomPlacementLocation.Rotation);
                    Vector2Int worldTile = rotatedTile + roomPlacementLocation.Location;
                    if (worldGrid.ContainsKey(worldTile))
                    {
                        return false;
                    }
                }
            }

            // Ensure that no new connection will be closed off by pointing at a wall.
            foreach (ConnectionBlueprint connection in room.Connections)
            {
                // Rotate the connection's position and forward direction around the rooms origin.
                Vector2Int rotatedConnectionLocation = RotatePoint(connection.location, Vector2Int.zero, roomPlacementLocation.Rotation);
                Vector2Int rotatedForward = RotatePoint(connection.Forward, Vector2Int.zero, roomPlacementLocation.Rotation);

                // Calculate the number of tiles away we'd need to check to see if we hit another room using our given connection thickness.
                int connectionTileThickness = Mathf.RoundToInt(connection.ConnectionLength / cellSize);
                Vector2Int rotatedThickenedForward = rotatedForward * connectionTileThickness;

                // Find the location in the world our connection is pointing at.
                Vector2Int connectionTarget = roomPlacementLocation.Location + rotatedConnectionLocation + rotatedThickenedForward;

                bool matchesOpenConnection = openConnections.Any(x =>
                {
                    int openConnThickness = Mathf.RoundToInt(x.ConnectionLength / cellSize);
                    Vector2Int openConnectionTarget = x.location + x.Forward * openConnThickness;
                    return openConnectionTarget == rotatedConnectionLocation &&
                           x.Forward == -rotatedForward;
                });
                if (!matchesOpenConnection)
                {
                    if (worldGrid.ContainsKey(connectionTarget))
                    {
                        return false;
                    }
                }
            }

            // Build a set of all new connection world positions and their directions
            var newRoomConnections = room.Connections.Select(connection =>
            {
                Vector2Int rotatedLocation = RotatePoint(connection.location, Vector2Int.zero, roomPlacementLocation.Rotation) + roomPlacementLocation.Location;
                Vector2Int rotatedForward = RotatePoint(connection.Forward, Vector2Int.zero, roomPlacementLocation.Rotation);
                int connectionTileThickness = Mathf.RoundToInt(connection.ConnectionLength / cellSize);
                Vector2Int target = rotatedLocation + rotatedForward * connectionTileThickness;
                return (location: rotatedLocation, forward: rotatedForward, target: target);
            }).ToList();

            foreach (ConnectionBlueprint connection in openConnections)
            {
                int connectionTileThickness = Mathf.RoundToInt(connection.ConnectionLength / cellSize);
                Vector2Int target = connection.location + connection.Forward * connectionTileThickness;

                // If the target is inside the new room's bounding box
                if (target.x >= min.x && target.x < max.x && target.y >= min.y && target.y < max.y)
                {
                    // Allow if there is a new connection at this location facing the opposite direction
                    bool matchesNewConnection = newRoomConnections.Any(newConn =>
                        newConn.location == target && newConn.forward == -connection.Forward);

                    if (!matchesNewConnection)
                    {
                        return false;
                    }
                }
            }

            return true;
        }

        /// <summary>
        /// Counts the number of open connections that would exist after this room is placed.
        /// </summary>
        /// <param name="room"> The room we want to place.</param>
        /// <param name="location"> The location we want to place the room.</param>
        /// <returns> Returns an integer representing the numbner of open connections that would exist after placing this room.</returns>
        int GetOpenConnectionsAfterRoomPlacement(RoomBlueprint room, GridLocation location)
        {
            int changeInConnections = 0;

            // Build a list of the new room's connections' world locations and directions
            var newRoomConnections = room.Connections.Select(connection =>
            {
                Vector2Int rotatedLocation = RotatePoint(connection.location, Vector2Int.zero, location.Rotation) + location.Location;
                Vector2Int rotatedForward = RotatePoint(connection.Forward, Vector2Int.zero, location.Rotation);
                Vector2Int target = rotatedLocation + rotatedForward;
                return (location: rotatedLocation, forward: rotatedForward, target: target);
            }).ToList();

            // For each new connection, check if it connects to an existing open connection
            foreach (var newConn in newRoomConnections)
            {
                // Is there an open connection at this target, facing the opposite direction?
                bool connectsToExisting = openConnections.Any(openConn =>
                    (openConn.location + openConn.Forward) == newConn.location &&
                    openConn.Forward == -newConn.forward
                );

                // If not, and the target is not occupied, it's a new open connection
                if (!connectsToExisting && !worldGrid.ContainsKey(newConn.target))
                {
                    changeInConnections++;
                }
            }

            // For each existing open connection, check if it will be closed by the new room
            foreach (var openConn in openConnections)
            {
                Vector2Int openTarget = openConn.location + openConn.Forward;
                // Is there a new connection at this location, facing the opposite direction?
                bool closedByNewRoom = newRoomConnections.Any(newConn =>
                    newConn.location == openTarget &&
                    newConn.forward == -openConn.Forward
                );

                if (closedByNewRoom)
                {
                    changeInConnections--;
                }
            }

            return openConnections.Count + changeInConnections;
        }

        /// <summary>
        /// Finds all viable spots to place the room using the direction of the connection.
        /// </summary>
        /// <param name="room">The room we want to place.</param>
        /// <param name="connection"> The connection we want to build off of.</param>
        /// <returns>Where the room would have to be placed to build off of this connection.</returns>
        private List<GridLocation> FindRoomPlacementLocations(RoomBlueprint room, ConnectionBlueprint connection)
        {
            List<GridLocation> placementLocations = new List<GridLocation>();
            Quaternion[] rotations = { Quaternion.identity, Quaternion.Euler(0, 90, 0), Quaternion.Euler(0, 180, 0), Quaternion.Euler(0, 270, 0) };

            // Randomize the order of the rotations array
            rotations = rotations.OrderBy(x => randomNumberGenerator.Next()).ToArray();

            foreach (Quaternion rotation in rotations)
            {
                foreach (ConnectionBlueprint placedRoomConnection in room.Connections)
                {
                    // Rotate the connection's forward direction
                    Vector3 rotatedForward = rotation * new Vector3(placedRoomConnection.Forward.x, 0, placedRoomConnection.Forward.y);
                    Vector2Int rotatedForwardInt = new Vector2Int(Mathf.RoundToInt(rotatedForward.x), Mathf.RoundToInt(rotatedForward.z));

                    // If the connections are facing in opposite directions they can be linked up.
                    if (-1 * rotatedForwardInt == connection.Forward)
                    {
                        // Find the location in cells the room should be placed at if the given connections want to be lined up.
                        int connectionTileThickness = Mathf.RoundToInt(connection.ConnectionLength / cellSize);

                        // Calculate the location we want to place the room at in world grid coordinates.
                        Vector2Int targetLocation = connection.location + (connection.Forward * connectionTileThickness) - RotatePoint(placedRoomConnection.location, Vector2Int.zero, rotation);

                        GridLocation placementLocation = new GridLocation()
                        {
                            Location = targetLocation,
                            Rotation = rotation,
                        };

                        placementLocations.Add(placementLocation);
                    }
                }
            }

            return placementLocations;
        }

        /// <summary>
        /// Rotates a point around a given origin by a specified rotation.
        /// </summary>
        /// <param name="point">The point to rotate.</param>
        /// <param name="origin">The origin around which to rotate the point.</param>
        /// <param name="rotation">The rotation to apply to the point.</param>
        /// <returns>The rotated point as a Vector2Int.</returns>
        private Vector2Int RotatePoint(Vector2Int point, Vector2Int origin, Quaternion rotation)
        {
            // Find the relative position to the origin.
            Vector3 relaitvePosition = new Vector3(point.x - origin.x, 0, point.y - origin.y);
            // Rotate the point around the origin
            Vector3 rotatedRelativePosition = rotation * relaitvePosition;
            // Convert back to Vector2Int and translate back to the original position
            return new Vector2Int(Mathf.RoundToInt(rotatedRelativePosition.x + origin.x), Mathf.RoundToInt(rotatedRelativePosition.z + origin.y));
        }

        /// <summary>
        /// Gets the bounds of a room in world space at a given location with a given rotation.
        /// </summary>
        /// <param name="size"> The size of the room we want to rotate.</param>
        /// <param name="location"> The location we want to place the room at.</param>
        /// <param name="rotation"> The rotation we want to place the room at.</param>
        /// <param name="min"> The location of the bottom left corner of the room.</param>
        /// <param name="max"> The location of the top right corner of the room.</param>
        private void GetRotatedRoomBounds(Vector2Int size, Vector2Int location, Quaternion rotation, out Vector2Int min, out Vector2Int max)
        {
            // Get all four corners of the room in local space
            Vector2Int[] corners = new Vector2Int[]
            {
        new Vector2Int(0, 0),
        new Vector2Int(size.x, 0),
        new Vector2Int(0, size.y),
        new Vector2Int(size.x, size.y)
            };

            // Rotate and translate each corner
            Vector2Int[] worldCorners = corners
                .Select(corner => RotatePoint(corner, Vector2Int.zero, rotation) + location)
                .ToArray();

            // Find min and max
            int minX = worldCorners.Min(v => v.x);
            int minY = worldCorners.Min(v => v.y);
            int maxX = worldCorners.Max(v => v.x);
            int maxY = worldCorners.Max(v => v.y);

            min = new Vector2Int(minX, minY);
            max = new Vector2Int(maxX, maxY);
        }

        /// <summary>
        /// Represents a position and rotation of a room we want to place.
        /// Roughly analogous to a transform but we don't want to instantiate an entire game object to access one.
        /// </summary>
        private struct GridLocation
        {
            /// <summary>
            /// The rotation of the room to be placed.
            /// </summary>
            public Quaternion Rotation;

            /// <summary>
            /// The world coordinates of the room to be placed at.
            /// </summary>
            public Vector2Int Location;
        }
    }
}