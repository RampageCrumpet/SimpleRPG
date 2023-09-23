using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;
using LevelGeneration;
using System.Security.Cryptography;
using Codice.CM.Common;
using UnityEditor.MemoryProfiler;
using static UnityEditor.FilePathAttribute;
using System.Linq;
using Codice.CM.Client.Differences;

public class LevelGenerator 
{
    /// <summary>
    /// The connections that have not currently been 
    /// </summary>
    private List<Connection> openConnections = new List<Connection>();

    private List<Connection> closedConnections = new List<Connection>();

    /// <summary>
    /// A map for each tile to the world grid. Each room can span multiple tiles.
    /// </summary>
    private Room[,] worldGrid;

    /// <summary>
    /// A complete list of rooms we can draw from.
    /// </summary>
    private List<Room> roomBlueprints = new List<Room>();

    private List<Room> placedRooms = new List<Room>();

    private System.Random randomNumberGenerator;

    /// <summary>
    /// The size of each cell in unity units.
    /// </summary>
    private int cellSize;

    /// <summary>
    /// Randomly generate a world given a seed and a size.
    /// </summary>
    /// <param name="seed"> The seed we want to use for our random generator.</param>
    /// <param name="worldSize"> The worlds size in cells.</param>
    public LevelGenerator(int seed, Vector2Int worldSize, List<Room> roomBlueprints, int cellSize)
    {
        randomNumberGenerator = new System.Random(seed);
        worldGrid = new Room[worldSize.x, worldSize.y];
        this.cellSize = cellSize;
        this.roomBlueprints = roomBlueprints;
    }

    public void GenerateLevel(int minimumNumberOfRooms)
    {
        //Place a starting room to seed our dungeon.
        Room startingRoom = SelectRandomRoom(null);
        PlaceRoom(startingRoom, new Vector2Int(worldGrid.GetLength(0)/2, worldGrid.GetLength(1)/2));

        // Continue placing rooms while our room count hasn't been reached or we have open connections to fill.
        while (placedRooms.Count < minimumNumberOfRooms || openConnections.Count > 0)
        {
            // The connection we want to build off of.
            Connection openConnection = openConnections[randomNumberGenerator.Next(openConnections.Count)];

            Room newRoom = SelectRandomRoom(openConnection);

            foreach(Vector2Int location in FindRoomPlacementLocations(newRoom, openConnection))
            {
                int openConnectionsAfterRoomPlacement = GetOpenConnectionsAfterRoomPlacement(newRoom, location);

                // Try to place the room if we haven't placed enough rooms or if placing the room will reduce the total 
                if ((placedRooms.Count < minimumNumberOfRooms && openConnectionsAfterRoomPlacement != 0) || openConnectionsAfterRoomPlacement < openConnections.Count)
                {
                    if (ValidateRoomPlacement(newRoom, location))
                    {
                        PlaceRoom(newRoom, location);
                        break;
                    }
                }
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
        //TODO: Take into account the connection we're building off so we only pick potentially applicable rooms.
        if(connection == null)
        {
            return roomBlueprints[randomNumberGenerator.Next(roomBlueprints.Count)];
        }
        else
        {
            List<Room> filteredRoomBlueprints = roomBlueprints.Where(x => x.connections.Any(y => y.CanConnect(connection))).ToList();
            return filteredRoomBlueprints.ElementAt(randomNumberGenerator.Next(0, filteredRoomBlueprints.Count));
        }
    }

    /// <summary>
    /// Place a room on the level generation map.
    /// </summary>
    /// <param name="room"> The room we want to place.</param>
    /// <param name="location"> The location in cells where we want to place the room.</param>
    private void PlaceRoom(Room room, Vector2Int location)
    {
        Room placedRoom = GameObject.Instantiate(room.gameObject, new Vector3(location.x * cellSize, room.gameObject.transform.position.y, location.y * cellSize), room.gameObject.transform.rotation).GetComponent<Room>();

        // Fill out the rooms occupied spaces on the world grid.
        for (int x = 0; x < placedRoom.Size.x; x++)
        {
            for (int y = 0; y < placedRoom.Size.y; y++)
            {
                worldGrid[x + location.x, y + location.y] = placedRoom;
            }
        }

        // Remove the recently closed connections.
        for(int x = openConnections.Count - 1; x >= 0;  x--) 
        {
            Connection connection = openConnections[x];

            Vector2Int connectionTarget = connection.location + connection.Forward;
            if (worldGrid[connectionTarget.x, connectionTarget.y] != null)
            {
                closedConnections.Add(connection);
                openConnections.Remove(connection);
            }
        }

        // Add the new connections from the recently placed room.
        foreach (Connection connection in placedRoom.connections)
        {
            Vector2Int connectionTarget = connection.location + connection.Forward + location;
            if (worldGrid[connectionTarget.x, connectionTarget.y] == null)
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
            connection.location += location;
        }

        placedRooms.Add(placedRoom);
    }

    /// <summary>
    /// Validates that a room can acutally be placed at the given location.
    /// </summary>
    /// <param name="room"> The room we want to check to see if we can place.</param>
    /// <param name="location"> The location we want to check for valid placement.</param>
    /// <returns>True if the room can be placed here without issue, false is placing the room here would close off rooms or place the room outside of the map.</returns>
    bool ValidateRoomPlacement(Room room, Vector2Int location)
    {
        // If we're trying to place a room that clips outside of the world grids bounds it's an invalid placement.
        
        if (!IsWithinWorldBounds(location) || !IsWithinWorldBounds(location + room.Size))
        {
            return false;
        }

        // Ensure that none of the placement locations are already occupied.
        for (int x = 0; x < room.Size.x; x++)
        {
            for(int y = 0; y < room.Size.y; y++)
            {
                if (worldGrid[x + location.x, y + location.y] != null)
                {
                    return false;
                }
            }
        }

        // Ensure that no connection points outside of the world grid.
        foreach(Connection connection in room.connections)
        {
            if (!IsWithinWorldBounds(location + connection.location + connection.Forward))
            {
                return false;
            }
        }

        // Find the place in the world all of the connections in the room would be pointing at.
        List<Vector2Int> roomConnectionTargets = room.connections.Select(x => x.location + x.Forward + location).ToList();

        // Ensure that no new connection will be closed off by pointing at a wall.
        foreach (Vector2Int connectionTarget in roomConnectionTargets)
        {
            if (!openConnections.Any(x => x.location == connectionTarget) && worldGrid[connectionTarget.x, connectionTarget.y] != null)
            {
                return false;
            }
        }


        // Ensure that no existing connection will be closed off by hitting a wall.
        foreach (Connection connection in openConnections)
        {
            // Find our connections target cell relative to our room origin .
            Vector2Int roomRelativeTarget = connection.location + connection.Forward - location;

            // If the connection points into our room
            if(roomRelativeTarget.x >= 0 && roomRelativeTarget.x < room.Size.x && roomRelativeTarget.y >= 0 && roomRelativeTarget.y < room.Size.y)
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
    int GetOpenConnectionsAfterRoomPlacement(Room room, Vector2Int location)
    {
        //Count of the change in our total connections should this room be placed here.
        int changeInConnections = 0;

        // Find all of the new connections that would be open.
        foreach (Connection connection in room.connections)
        {
            // The location our connection is pointing at.
            Vector2Int connectionTargetLocation = new Vector2Int(
                connection.location.x + location.x + connection.Forward.x, 
                connection.location.y + location.y + connection.Forward.y);

            if (IsWithinWorldBounds(connectionTargetLocation))
            {
                // If the connection points at an open space in our world it wont be closed off.
                if (worldGrid[connectionTargetLocation.x, connectionTargetLocation.y] == null)
                {
                    changeInConnections++;
                }
            }
        }

        // Find all the existing connections that would be closed.
        foreach(Connection connection in openConnections)
        {
            // The location our connection is pointing at.
            Vector2Int connectionTargetLocation = connection.location + connection.Forward;

            // If the target location of this connection is inside the room the connection would be blocked off and must be closed.
            if (connectionTargetLocation.x >= location.x && connectionTargetLocation.x < location.x + room.Size.x)
            {
                if (connectionTargetLocation.y >= location.y && connectionTargetLocation.y < location.y + room.Size.y)
                {
                    changeInConnections--;
                }
            }
        }

        return openConnections.Count + changeInConnections;
    }

    /// <summary>
    /// Returns true if the given position is within the world bounds.
    /// </summary>
    /// <param name="position"> The position we want to check.</param>
    /// <returns> True if the given position is within the bounds of the world, false if it's not.</returns>
    private bool IsWithinWorldBounds(Vector2Int position)
    {
        if (position.x < 0 ||
            position.y < 0 ||
            position.x >= worldGrid.GetLength(0) ||
            position.y  >= worldGrid.GetLength(1))
        {
            return false;
        }

        return true;
    }
    
    /// <summary>
    /// Finds all viable spots to place the room using the direction of the connection.
    /// </summary>
    /// <param name="room">The room we want to place.</param>
    /// <param name="connection"> The connection we want to build off of.</param>
    /// <returns>Where the room would have to be placed to build off of this connection.</returns>
    private List<Vector2Int> FindRoomPlacementLocations(Room room, Connection connection)
    {
        List<Vector2Int> placementLocations = new List<Vector2Int>();
        foreach (Connection roomConnection in room.connections)
        {
            // If the connections are facing in opposite directions they can be linked up.
            if (-1* roomConnection.Forward == connection.Forward)
            {
                // Find the location the room should be placed at if the given connections want to be lined up.
                Vector2Int targetLocation = connection.location + connection.Forward - roomConnection.location;
                placementLocations.Add(targetLocation);
            }
        }

        return placementLocations;
    }
}