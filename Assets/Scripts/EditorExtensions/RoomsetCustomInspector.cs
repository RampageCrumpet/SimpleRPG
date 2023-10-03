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
                connection.location = new Vector2Int(Mathf.FloorToInt(connection.gameObject.transform.position.x / ((Roomset)target).cellSize), Mathf.FloorToInt((int)connection.transform.position.z) / ((Roomset)target).cellSize);
            }
        }

        private Vector2Int CalculateRoomSize(Room room)
        {
            var objectsInRoom = room.gameObject.GetComponentsInChildren<Transform>();

            Vector2Int originLocation = new Vector2Int(Mathf.RoundToInt(objectsInRoom.Min(x => x.position.x)), Mathf.RoundToInt(objectsInRoom.Min(y => y.position.z)));
            Vector2Int farCornerLocation = new Vector2Int(Mathf.RoundToInt(objectsInRoom.Max(x => x.position.x)), Mathf.RoundToInt(objectsInRoom.Max(y => y.position.z)));

            // Find the distance between the corners and divide it by the cell size to get the rooms size in cells.
            // Because the origin is in the middle of the tile we lose 1 tile's width in each direction and have to add that back.
            return (farCornerLocation - originLocation + new Vector2Int(1, 1)) / ((Roomset)target).cellSize;
        }
    }
    #endif
}
