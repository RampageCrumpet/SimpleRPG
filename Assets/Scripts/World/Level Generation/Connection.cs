using UnityEngine;

namespace LevelGeneration
{
    /// <summary>
    /// This class represents a connection between two <see cref="Room"/>'s.
    /// </summary>
    public class Connection
    {
        /// <summary>
        /// The room this connection is a part of.
        /// </summary>
        private Room room;

        /// <summary>
        /// The location in tile space of this <see cref="Connection"/> relative to our parent.
        /// </summary>
        public Vector2Int LocalTilePosition;

        private Vector2Int forward;

        /// <summary>
        /// Finds this connections Foward in global tile space.
        /// </summary>
        public Vector2Int Forward
        {
            get
            {
                Vector3 forwardVector3 = new Vector3(forward.x, 0, forward.y);
                forwardVector3 = room.Rotation * forwardVector3;
                return new Vector2Int(Mathf.RoundToInt(forwardVector3.x), Mathf.RoundToInt(forwardVector3.z));
            }
        }

        /// <summary>
        ///  How many tiles long our connection is.
        /// </summary>
        public float ConnectionLength { get; private set; }

        public float ConnectionWidth { get; private set; }

        public Connection(ConnectionBlueprint connectionBlueprint, Room room)
        {
            ConnectionLength = connectionBlueprint.ConnectionLength;
            ConnectionWidth = connectionBlueprint.ConnectionWidth;

            forward = connectionBlueprint.Forward;
            LocalTilePosition = connectionBlueprint.location;
            this.room = room;
        }

        /// <summary>
        /// The location in tile space of this <see cref="Connection"/> relative to the origin of tile space.
        /// </summary>
        public Vector2Int GlobalTilePosition
        {
            get
            {
                // Get the room's rotation and position (in tile space)
                Quaternion roomRotation = room.Rotation;
                Vector2Int roomTilePosition = room.Position;

                // Rotate the connection's local tile position
                Vector3 rotatedLocal = roomRotation * new Vector3(LocalTilePosition.x, 0, LocalTilePosition.y);

                // Add the room's tile position
                Vector2Int globalTile = roomTilePosition + new Vector2Int(
                    Mathf.RoundToInt(rotatedLocal.x),
                    Mathf.RoundToInt(rotatedLocal.z)
                );

                return globalTile;
            }
        }



        /// <summary>
        /// Returns true if a connection is facing in the opposite direction as another connection.
        /// </summary>
        /// <param name="otherConnection"> The connection we want to see if we can connect to.</param>
        /// <returns> True if the connections are facing opposite directions, false otherwise.</returns>
        public bool CanConnect(Connection otherConnection)
        {
            return Forward == -otherConnection.Forward;
        }
    }
}
