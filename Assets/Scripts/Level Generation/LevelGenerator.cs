using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;
using LevelGeneration;
using System.Security.Cryptography;
using Codice.CM.Common;
using UnityEditor.MemoryProfiler;
using static UnityEditor.FilePathAttribute;

public class LevelGenerator 
{
    /// <summary>
    /// The connections that have not currently been 
    /// </summary>
    private List<Connection> openConnections = new List<Connection>();

    private List<Connection> closedConnection = new List<Connection>();

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

    public LevelGenerator(int seed, Vector2Int worldSize)
    {
        randomNumberGenerator = new System.Random(seed);
        worldGrid = new Room[worldSize.x, worldSize.y];
    }

    void GenerateLevel(int minimumNumberOfRooms)
    {
        //Place a starting room to seed our dungeon.
        Room startingRoom = SelectRandomRoom();
        PlaceRoom(startingRoom, new Vector2Int());

        // Continue placing rooms while our room count hasn't been reached.
        while (placedRooms.Count < minimumNumberOfRooms && openConnections.Count > 0)
        {
            Room newRoom = SelectRandomRoom();

            // The connection we want to build off of.
            Connection openConnection = openConnections[randomNumberGenerator.Next(openConnections.Count)];

            foreach(Vector2Int location in FindRoomPlacementLocations(newRoom, openConnection))
            {
                int openConnectionsAfterRoomPlacement = GetOpenConnectionsAfterRoomPlacement(newRoom, location);

                // Try to place the room if we haven't placed enough rooms or if placing the room will reduce the total 
                if ((placedRooms.Count < minimumNumberOfRooms && openConnectionsAfterRoomPlacement != 0) || openConnectionsAfterRoomPlacement < openConnections.Count)
                {
                    ValidateRoomPlacement(newRoom, location);
                    PlaceRoom(newRoom, location);
                    break;
                }
            }
        }
    }

    /// <summary>
    /// Gets a new random room to place in the world.
    /// </summary>
    /// <returns></returns>
    private Room SelectRandomRoom()
    {
        return roomBlueprints[randomNumberGenerator.Next(roomBlueprints.Count)];
    }

    void PlaceRoom(Room room, Vector2Int location)
    {
        for (int x = 0; x < room.Size.x; x++)
        {
            for (int y = 0; y < room.Size.y; y++)
            {
                worldGrid[x + location.x, y + location.y] = room;
            }
        }
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
        return true;
    }

    /// <summary>
    /// Counts the number of open connections that would exist after this room is placed.
    /// </summary>
    /// <param name="room"> The room we want to place.</param>
    /// <param name="location"> The location we want to place the room.</param>
    /// <returns> Returns an integer representing the numbner of open connections that would exist after placing this room.</returns>
    private int GetOpenConnectionsAfterRoomPlacement(Room room, Vector2Int location)
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
                if (worldGrid[connectionTargetLocation.x, connectionTargetLocation.y] != null)
                {
                    changeInConnections++;
                }
            }
        }

        // Find all the existing connections that would be closed.
        foreach(Connection connection in openConnections)
        {
            // The location our connection is pointing at.
            Vector2Int connectionTargetLocation = new Vector2Int(
               connection.location.x + location.x + connection.Forward.x,
               connection.location.y + location.y + connection.Forward.y);

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
    /// <param name="position"></param>
    /// <returns></returns>
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
            if (-1*connection.Forward == connection.Forward)
            {
                // Find the location the room should be placed at if the given connections want to be lined up.
                Vector2Int taregetLocation = connection.location + connection.Forward - roomConnection.location;
                placementLocations.Add(taregetLocation);
            }
        }

        return placementLocations;
    }
}
