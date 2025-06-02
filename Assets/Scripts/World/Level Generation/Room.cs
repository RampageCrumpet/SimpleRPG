using NUnit.Framework;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace LevelGeneration
{
    /// <summary>
    /// This class 
    /// </summary>
    public class Room
    {
        private Quaternion rotation;

        /// <summary>
        /// The location in tile space of this <see cref="Room"/> in tile space..
        /// </summary>
        public Vector2Int Position;

        /// <summary>
        /// The rooms rotation.
        /// </summary>
        public Quaternion Rotation 
        { 
            get
            {
                return rotation;
            }

            set
            {
                rotation = value;

                // Always use the original blueprint size for rotation calculations
                Vector3 size3 = new Vector3(RoomBlueprint.Size.x, 0, RoomBlueprint.Size.y);
                Vector3 rotated = rotation * size3;


                // The new size in tiles is the absolute value of the rotated axes, rounded to int
                Size = new Vector2Int(
                    Mathf.RoundToInt(Mathf.Abs(rotated.x)),
                    Mathf.RoundToInt(Mathf.Abs(rotated.z))
                );
            }
        }


        /// <summary>
        /// The size of the room in tiles.
        /// </summary>
        public Vector2Int Size { get; private set; } 

        /// <summary>
        /// The RoomOrigin is the smallest X/Y position contained within the room.
        /// </summary>
        public Vector2Int RoomOrigin 
        {
            get
            {
                // Always use the original blueprint size for rotation calculations
                Vector3 originalRoomSize = new Vector3(RoomBlueprint.Size.x, 0, RoomBlueprint.Size.y);
                Vector3 rotatedSize = rotation * originalRoomSize;

                return new Vector2Int(Mathf.RoundToInt(Mathf.Min(rotatedSize.x, 0)),Mathf.RoundToInt(Mathf.Min(rotatedSize.z, 0)));
            }
        }
        public RoomBlueprint RoomBlueprint { get; private set; }

        public List<Connection> Connections { get; private set; }


        public Room(RoomBlueprint roomBlueprint)
        {
            RoomBlueprint = roomBlueprint;

            this.rotation = roomBlueprint.transform.rotation;
            this.Position = Vector2Int.zero;

            Size = roomBlueprint.Size;

            Connections = new List<Connection>();
            foreach (ConnectionBlueprint connectionBlueprint in roomBlueprint.Connections)
            {
                Connection connection = new Connection(connectionBlueprint, this);
                Connections.Add(connection);
            }
        }
    }
}
