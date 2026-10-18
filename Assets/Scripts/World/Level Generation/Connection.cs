using UnityEngine;

namespace LevelGeneration
{
    /// <summary>
    /// This class represents a connection between two <see cref="Room"/>'s.
    /// </summary>
    public class Connection
    {

        /// <summary>
        /// The location in tile space of this <see cref="Connection"/> relative to our parent.
        /// </summary>
        public Vector2Int LocalTilePosition;

        /// <summary>
        /// The <see cref="ConnectionBlueprint"> this connection represents.
        /// </summary>
        public ConnectionBlueprint ConnectionBlueprint;

        /// <summary>
        /// The room this connection is a part of.
        /// </summary>
        private Room room;

        /// <summary>
        /// The direction this connection is facing in tile space.
        /// </summary>
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
        ///  How many unity units long our connection is.
        /// </summary>
        [Tooltip("The length of the connection in unity units away from the room it exists on.")]
        public float ConnectionLength { get; private set; }

        /// <summary>
        /// How many unity units wide our connection is.
        /// </summary>
        [Tooltip("The width of the connection in unity units.")]
        public float ConnectionWidth { get; private set; }

        public Connection(ConnectionBlueprint connectionBlueprint, Room room)
        {
            this.ConnectionBlueprint = connectionBlueprint;

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

        /// <summary>
        /// Finds the cell a connection opens onto, <paramref name="lengthInCells"/> cells away.
        /// A connection's position is a grid point (a cell corner), so the cell beyond that point is the one on its forward side;
        /// for connections facing negative x or y that is the cell below/left of the point, not the cell indexed by it.
        /// </summary>
        /// <param name="lengthInCells"> How many cells away the target point is.</param>
        public Vector2Int FacingCell(int lengthInCells)
        {
            Vector2Int forwardDirection = Forward;
            Vector2Int target = GlobalTilePosition + (forwardDirection * lengthInCells);

            return new Vector2Int(
                forwardDirection.x < 0 ? target.x - 1 : target.x,
                forwardDirection.y < 0 ? target.y - 1 : target.y);
        }

        public void Seal()
        {
            ConnectionBlueprint.Seal();
        }
    }
}
