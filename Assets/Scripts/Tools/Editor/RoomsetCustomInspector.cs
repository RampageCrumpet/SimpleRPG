using LevelGeneration;
using System.Linq;
using UnityEngine;
using System.Reflection;

namespace UnityEditor.EditorTools
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
                foreach (RoomBlueprint room in roomset.RoomCollection)
                {
                    PackageRoom(room, roomset.cellSize);
                }
            }

        }

        /// <summary>
        /// Packages a room up for world generation by setting the room's origin offset, connection positions and the room size.
        /// </summary>
        public static void PackageRoom(RoomBlueprint room, float cellSize)
        {
            room.gameObject.transform.position = Vector3.zero;

            // The lowest corner of the room's geometry, in cells relative to the prefab root. Everything is measured from here.
            Vector2Int originOffset = CalculateOriginOffset(room, cellSize);
            room.OriginOffset = originOffset;

            PackageConnections(room, originOffset, cellSize);
            room.Size = CalculateRoomSize(room, originOffset, cellSize);

            EditorUtility.SetDirty(room);
            PrefabUtility.SavePrefabAsset(room.gameObject);
        }

        /// <summary>
        /// Package up all of the connections by setting their position and replacing the given rooms connection list with them.
        /// </summary>
        /// <param name="room"> The room we want to set connections for.</param>
        /// <param name="originOffset"> The room's origin offset in cells; connection locations are relative to the room's lowest corner.</param>
        private static void PackageConnections(RoomBlueprint room, Vector2Int originOffset, float cellSize)
        {
            PropertyInfo connectionsProperty = typeof(RoomBlueprint).GetProperty(nameof(room.Connections));
            connectionsProperty.SetValue(room, room.gameObject.GetComponentsInChildren<ConnectionBlueprint>().Where(connection => connection.SealingObject != null).ToList());

            foreach (ConnectionBlueprint connection in room.Connections)
            {
                // Calculate the x/y position of the connection. The Z direction in the 3d worldspace maps to the Y direction in the 2d room space.
                int xPos = Mathf.RoundToInt(connection.transform.position.x / cellSize) - originOffset.x;
                int yPos = Mathf.RoundToInt(connection.transform.position.z / cellSize) - originOffset.y;

                connection.location = new Vector2Int(xPos, yPos);
                EditorUtility.SetDirty(connection);
            }
        }

        /// <summary>
        /// Finds the lowest corner of the room in cells relative to the prefab root.
        /// A child pivot less than a full cell below the root is treated as part of the first cell rather than an extra one,
        /// because pieces such as wall trims sit slightly off the cell edge.
        /// </summary>
        private static Vector2Int CalculateOriginOffset(RoomBlueprint room, float cellSize)
        {
            var objectsInRoom = room.gameObject.GetComponentsInChildren<Transform>();

            float minX = objectsInRoom.Min(roomObject => roomObject.position.x);
            float minZ = objectsInRoom.Min(roomObject => roomObject.position.z);

            return new Vector2Int(
                Mathf.Min(0, Mathf.CeilToInt(minX / cellSize)),
                Mathf.Min(0, Mathf.CeilToInt(minZ / cellSize)));
        }

        /// <summary>
        /// Calculates the size of the room and returns it.
        /// </summary>
        /// <param name="room"> The room who's size we want to calculate.</param>
        /// <returns> The width and height of the room.</returns>
        private static Vector2Int CalculateRoomSize(RoomBlueprint room, Vector2Int originOffset, float cellSize)
        {
            var objectsInRoom = room.gameObject.GetComponentsInChildren<Transform>();

            float maxX = objectsInRoom.Max(roomObject => roomObject.position.x);
            float maxZ = objectsInRoom.Max(roomObject => roomObject.position.z);

            // Find the distance from the lowest corner to the far pivots and divide it by the cell size to get the room's size in cells.
            return new Vector2Int(
                Mathf.RoundToInt((maxX - originOffset.x * cellSize) / cellSize),
                Mathf.RoundToInt((maxZ - originOffset.y * cellSize) / cellSize));
        }
    }
    #endif
}
