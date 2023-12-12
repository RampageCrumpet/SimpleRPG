using LevelGeneration;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Unity.VisualScripting;
using UnityEditor;
using UnityEngine;

namespace UnityEditor
{
    #if UNITY_EDITOR
    [CustomEditor(typeof(Roomset))]
    public class RoomsetCustomInspector : Editor
    {
        public override void OnInspectorGUI()
        {
            base.OnInspectorGUI();

            if (GUILayout.Button("Package Rooms"))
            {
                Roomset roomset = (Roomset)target;
                foreach (Room room in roomset.RoomCollection)
                {
                    this.PackageRoom(room);
                }
            }

        }

        /// <summary>
        /// Packages a room up for world generation by setting connection positions and the room size.
        /// </summary>
        private void PackageRoom(Room room)
        {
            PackageConnections(room);

            //Package the room
            room.gameObject.transform.position = Vector3.zero;
            room.Size = CalculateRoomSize(room);

            PrefabUtility.SavePrefabAsset(room.gameObject);
        }

        /// <summary>
        /// Package up all of the connections by setting their position and replacing the given rooms connection list with them.
        /// </summary>
        /// <param name="room"> The room we want to set connections for.</param>
        private void PackageConnections(Room room)
        {
            room.connections = room.gameObject.GetComponentsInChildren<Connection>().ToList();

            foreach (Connection connection in room.connections)
            {
                if (connection.transform.position.x < 0 || connection.transform.position.z < 0)
                {
                    Debug.LogError("Room " + room.gameObject.name + " has a connection in a negative position. This isn't currently supported.");
                }

                // Calculate the x/y position of the connection. The Z direction in the 3d worldspace maps to the Y direction in the 2d room space.
                int xPos = Mathf.FloorToInt(connection.transform.position.x / ((Roomset)target).cellSize) - (1 - Mathf.CeilToInt((connection.transform.position.x % ((Roomset)target).cellSize / ((Roomset)target).cellSize)));
                int yPos = Mathf.FloorToInt(connection.transform.position.z / ((Roomset)target).cellSize) - (1 - Mathf.CeilToInt((connection.transform.position.z % ((Roomset)target).cellSize / ((Roomset)target).cellSize)));

                // Ensure connections at the bottom of the room are placed inside the room.
                if(connection.transform.position.x == 0)
                {
                    xPos += 1;
                }
                if(connection.transform.position.z == 0)
                {
                    yPos += 1;
                }

                connection.location = new Vector2Int(xPos, yPos);
            }
        }

        private Vector2Int CalculateRoomSize(Room room)
        {
            var objectsInRoom = room.gameObject.GetComponentsInChildren<Transform>();

            if(objectsInRoom.Any(roomObject => roomObject.position.x < 0 || roomObject.position.y < 0))
            {
                Debug.LogWarning(room.name + " contains objects with negative position values that are not currently supported by the level generator.");
            }

            Vector2Int originLocation = new Vector2Int(Mathf.RoundToInt(objectsInRoom.Min(x => x.position.x)), Mathf.RoundToInt(objectsInRoom.Min(y => y.position.z)));
            Vector2Int farCornerLocation = new Vector2Int(Mathf.RoundToInt(objectsInRoom.Max(x => x.position.x)), Mathf.RoundToInt(objectsInRoom.Max(y => y.position.z)));

            // Find the distance between the corners and divide it by the cell size to get the rooms size in cells.
            // Because the origin is in the middle of the tile we lose 1 tile's width in each direction and have to add that back.
            return (farCornerLocation - originLocation + new Vector2Int(1, 1)) / ((Roomset)target).cellSize;
        }
    }
    #endif
}
