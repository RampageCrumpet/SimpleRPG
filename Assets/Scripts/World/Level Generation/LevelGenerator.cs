using System.Collections.Generic;
using UnityEngine;
using LevelGeneration;
using System.Linq;
using UnityEngine.Networking.PlayerConnection;
using static UnityEditor.FilePathAttribute;
using UnityEditor.MemoryProfiler;
using JetBrains.Annotations;

public class LevelGenerator
{
    /// <summary>
    /// The seed we want to use for generating our level.
    /// </summary>
    public int Seed {get; private set;}

    /// <summary>
    /// The connections that have not currently been 
    /// </summary>
    private List<Connection> openConnections = new List<Connection>();

    private List<Connection> closedConnections = new List<Connection>();

    /// <summary>
    /// A map for each tile to the world grid. Each room can span multiple tiles.
    /// </summary>
    private Dictionary<Vector2Int, Room> worldGrid;

    /// <summary>
    /// A complete list of rooms we can draw from.
    /// </summary>
    private List<Room> roomBlueprints = new List<Room>();

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
    public LevelGenerator(int seed, List<Room> roomBlueprints, float cellSize)
    {
        Seed = seed;
        randomNumberGenerator = new System.Random(Seed);
        worldGrid = new Dictionary<Vector2Int, Room>();
        this.cellSize = cellSize;
        this.roomBlueprints = roomBlueprints;
    }

    /// <summary>
    /// Generates a level.
    /// </summary>
    /// <param name="minimumNumberOfRooms"> The minimum number of rooms this level will contain.</param>
    /// <param name="parentTransform"> The parent transform we want to attach the newly generated level to.</param>
    public void GenerateLevel(int minimumNumberOfRooms, Transform parentTransform)
    {
        //Place a starting room to seed our dungeon.
        Room startingRoom = SelectRandomRoom(null);
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
            Connection openConnection = openConnections[randomNumberGenerator.Next(openConnections.Count - 1)];

            Room newRoom = SelectRandomRoom(openConnection);

            roomPlacementsAttempted++;

            foreach(GridLocation location in FindRoomPlacementLocations(newRoom, openConnection))
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
            if(roomPlacementsAttempted >= minimumNumberOfRooms*10)
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
    private Room SelectRandomRoom(Connection connection)
    {
        if(connection == null)
        {
            return roomBlueprints[randomNumberGenerator.Next(roomBlueprints.Count)];
        }
        else
        {
            // Filter to only rooms that have connections facing the correct directions.
            List<Room> filteredRoomBlueprints = roomBlueprints; //.Where(x => x.connections.Any(y => y.CanConnect(connection))).ToList();

            if(filteredRoomBlueprints.Count == 0)
            {
                Debug.LogError("No connection connects to " + connection.gameObject.name + " at " + (connection.location + connection.Forward) + ".");
                // Clear the open connections to ensure that we don't continue generating.
                openConnections.Clear();
            }

            return filteredRoomBlueprints.ElementAt(randomNumberGenerator.Next(0, filteredRoomBlueprints.Count));
        }
    }

    /// <summary>
    /// Place a room on the level generation map.
    /// </summary>
    /// <param name="room"> The room we want to place.</param>
    /// <param name="rotation" The rotation we want our room to be placed at.</param>
    /// <param name="location"> The location in cells where we want to place the room.</param>
    private void PlaceRoom(Room room, GridLocation placementInformation, Transform worldParent)
    {
        Vector3 worldPlacementLocation = new Vector3(placementInformation.Location.x * cellSize, room.gameObject.transform.position.y, placementInformation.Location.y * cellSize);
        Room placedRoom = GameObject.Instantiate(room.gameObject, worldPlacementLocation, placementInformation.Rotation, worldParent).GetComponent<Room>();

        Vector2Int rotatedSize = RotateSize(room.Size, placementInformation.Rotation);

        // Fill out the rooms occupied spaces on the world grid.
        for (int x = 0; x < rotatedSize.x; x++)
        {
            for (int y = 0; y < rotatedSize.y; y++)
            {
                Vector2Int placementLocation = new Vector2Int(x + placementInformation.Location.x, y + placementInformation.Location.y);
                worldGrid.Add(placementLocation, room);
            }
        }

        // Fill out the spaces occupied by the room connection if they're not already full.
        //foreach (Connection connection in placedRoom.connections)
        //{
        //    Vector2Int connectionLocation = connection.location + placementInformation.Location + connection.Forward * (int)(connection.ConnectionThickness / cellSize);

        //    if(!worldGrid.ContainsKey(connectionLocation))
        //    {
        //        worldGrid.Add(connectionLocation, room);
        //    }
        //    else
        //    {
        //        connection.gameObject.SetActive(false);
        //    }
        //}

        // Remove the recently closed connections.
        for (int x = openConnections.Count - 1; x >= 0;  x--) 
        {

            Connection connection = openConnections[x];
            int connectionTileThickness = Mathf.CeilToInt(connection.ConnectionThickness / cellSize);

            Vector2Int connectionTarget = connection.location + (connection.Forward * (1 + connectionTileThickness));
            if (worldGrid.ContainsKey(connectionTarget))
            {
                closedConnections.Add(connection);
                openConnections.Remove(connection);
            }
        }

        // Add the new connections from the recently placed room.
        foreach (Connection connection in placedRoom.connections)
        {
            Vector2Int connectionTarget = connection.location + connection.Forward + placementInformation.Location;
            if (!worldGrid.ContainsKey(connectionTarget))
            {
                openConnections.Add(connection);
            }
            else
            {
                closedConnections.Add(connection);
            }
        }

        // Update the connections on the room to understand their new position.
        foreach(Connection connection in placedRoom.connections)
        {
            connection.location += placementInformation.Location;
        }

        placedRooms.Add(placedRoom);
    }

    /// <summary>
    /// Validates that a room can acutally be placed at the given location.
    /// </summary>
    /// <param name="room"> The room we want to check to see if we can place.</param>
    /// <param name="roomPlacementLocation"> The location we want to check for valid placement.</param>
    /// <returns>True if the room can be placed here without issue, false is placing the room here would close off rooms or place the room outside of the map.</returns>
    bool ValidateRoomPlacement(Room room, GridLocation roomPlacementLocation)
    {
        // Rotate the room's size based on the provided rotation
        Vector2Int rotatedSize = RotateSize(room.Size, roomPlacementLocation.Rotation);

        // Ensure that none of the placement locations are already occupied.
        for (int x = 0; x < rotatedSize.x; x++)
        {
            for(int y = 0; y < rotatedSize.y; y++)
            {
                Vector2Int placementLocation = new Vector2Int(x + roomPlacementLocation.Location.x, y + roomPlacementLocation.Location.y);
                if (worldGrid.ContainsKey(placementLocation))
                {
                    return false;
                }
            }
        }

        // Ensure that no new connection will be closed off by pointing at a wall.
        foreach (Connection connection in room.connections)
        {
            // Rotate the connection's position and forward direction around the rooms origin.
            Vector2Int rotatedConnectionLocation = RotatePoint(connection.location, Vector2Int.zero, roomPlacementLocation.Rotation);
            Vector2Int rotatedForward = RotatePoint(connection.Forward, Vector2Int.zero, roomPlacementLocation.Rotation);

            // Calculate the number of tiles away we'd need to check to see if we hit another room using our given connection thickness.
            int connectionTileThickness = Mathf.RoundToInt(connection.ConnectionThickness / cellSize);
            Vector2Int rotatedThickenedForward = rotatedForward * (connectionTileThickness + 1);

            // Find the location in the world our connection is pointing at.
            Vector2Int connectionTarget = roomPlacementLocation.Location + rotatedConnectionLocation + rotatedThickenedForward;

            // If the target position has no connection pointing out of it in the opposite direction as our connection AND it's occupied the position must be filled by a wall.
            if (!openConnections.Any(x => x.location == connectionTarget && x.Forward * -1 == rotatedForward))
            {
                if (worldGrid.ContainsKey(connectionTarget))
                {
                    return false;
                }
            }
        }

        // Find the place in the world all of the connections in the room would be pointing at.
        IEnumerable<Vector2Int> roomConnectionTargets = room.connections.Select(x => x.location + x.Forward + roomPlacementLocation.Location);

        // Ensure that no existing connection will be closed off by hitting a wall.
        foreach (Connection connection in openConnections)
        {
            // Calculate the number of tiles away we'd need to check to see if we hit another room using our given connection thickness.
            int connectionTileThickness = Mathf.CeilToInt(connection.ConnectionThickness / cellSize);

            // Find our connections target cell relative to our room origin .
            Vector2Int roomRelativeTarget =roomPlacementLocation.Location + connection.location + (connection.Forward * (connectionTileThickness + 1));

            // If the connection points into our room
            if (roomRelativeTarget.x >= 0 && roomRelativeTarget.x < rotatedSize.x && roomRelativeTarget.y >= 0 && roomRelativeTarget.y < rotatedSize.y)
            {
                if (!roomConnectionTargets.Any(x => x == connection.location))
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
    int GetOpenConnectionsAfterRoomPlacement(Room room, GridLocation location)
    {
        //Count of the change in our total connections should this room be placed here.
        int changeInConnections = 0;

        // Find all of the new connections that would be open.
        foreach (Connection connection in room.connections)
        {
            // Rotate the connection's forward direction
            Vector3 rotatedForward = location.Rotation * new Vector3(connection.Forward.x, 0, connection.Forward.y);
            Vector2Int rotatedForwardInt = new Vector2Int(Mathf.RoundToInt(rotatedForward.x), Mathf.RoundToInt(rotatedForward.z));

            // The location our connection is pointing at.
            Vector2Int connectionTargetLocation = new Vector2Int(
                connection.location.x + location.Location.x + rotatedForwardInt.x,
                connection.location.y + location.Location.y + rotatedForwardInt.y);

            // If the connection points at an open space in our world it won't be closed off.
            if (!worldGrid.ContainsKey(connectionTargetLocation))
            {
                changeInConnections++;
            }
        }

        // Find all the existing connections that would be closed.
        foreach (Connection connection in openConnections)
        {
            // The location our connection is pointing at.
            Vector2Int connectionTargetLocation = connection.location + connection.Forward;

            // Rotate the room's bounds to match the current rotation
            Vector2Int rotatedRoomMin = RotatePoint(Vector2Int.zero, Vector2Int.zero, location.Rotation) + location.Location;
            Vector2Int rotatedRoomMax = RotatePoint(room.Size - Vector2Int.one, Vector2Int.zero, location.Rotation) + location.Location;

            // If the target location of this connection is inside the room the connection would be blocked off and must be closed.
            if (connectionTargetLocation.x >= rotatedRoomMin.x && connectionTargetLocation.x <= rotatedRoomMax.x &&
                connectionTargetLocation.y >= rotatedRoomMin.y && connectionTargetLocation.y <= rotatedRoomMax.y)
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
    private List<GridLocation> FindRoomPlacementLocations(Room room, Connection connection)
    {
        List<GridLocation> placementLocations = new List<GridLocation>();
        //Quaternion[] rotations = { Quaternion.identity, Quaternion.Euler(0, 90, 0), Quaternion.Euler(0, 180, 0), Quaternion.Euler(0, 270, 0) };
        Quaternion[] rotations = { Quaternion.identity}; // Just for testing purposes. //TODO: Delete me

        // Randomize the order of the rotations array
        rotations = rotations.OrderBy(x => randomNumberGenerator.Next()).ToArray();

        foreach (Quaternion rotation in rotations)
        {
            foreach (Connection placedRoomConnection in room.connections)
            {
                // Rotate the connection's forward direction
                Vector3 rotatedForward = rotation * new Vector3(placedRoomConnection.Forward.x, 0, placedRoomConnection.Forward.y);
                Vector2Int rotatedForwardInt = new Vector2Int(Mathf.RoundToInt(rotatedForward.x), Mathf.RoundToInt(rotatedForward.z));


                // If the connections are facing in opposite directions they can be linked up.
                if (-1 * rotatedForwardInt == connection.Forward)
                {
                    // Find the location in cells the room should be placed at if the given connections want to be lined up.
                    int connectionTileThickness = Mathf.RoundToInt(connection.ConnectionThickness / cellSize);
                    Vector2Int targetLocation = connection.location + (connection.Forward * (connectionTileThickness + 1)) - RotatePoint(placedRoomConnection.location, Vector2Int.zero, rotation);

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
    /// Rotates the size of a room based on the provided rotation.
    /// </summary>
    /// <param name="size">The original size of the room.</param>
    /// <param name="rotation">The rotation to apply.</param>
    /// <returns>The rotated size as a Vector2Int.</returns>
    private Vector2Int RotateSize(Vector2Int size, Quaternion rotation)
    {
        Vector3 rotatedSize = rotation * new Vector3(size.x, 0, size.y);
        return new Vector2Int(Mathf.Abs(Mathf.RoundToInt(rotatedSize.x)), Mathf.Abs(Mathf.RoundToInt(rotatedSize.z)));
    }

    /// <summary>
    /// Represents a position and rotation of a room we want to place.
    /// Roughly analogous to a transform but we don't want to instantiate an entire game object to access one.
    /// </summary>
    private struct GridLocation
    {
        public Quaternion Rotation;
        public Vector2Int Location;
    }
}