using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Data;
using System.Linq;
using UnityEngine;

namespace SimpleRPG.LevelGeneration
{
    /// <summary>
    /// This class holds all of the data needed to instantiate a room including it's size and list of connections.
    /// </summary>
    public class RoomBlueprint : MonoBehaviour
    {
        /// <summary>
        /// The size of the room in Unity units.
        /// </summary>
        public Vector2Int Size;

        /// <summary>
        /// Where the room's lowest corner sits relative to the prefab root, in cells (never positive).
        /// Rooms whose geometry extends below or left of the root (for example corridor stubs) have a negative offset.
        /// Connection locations and <see cref="Size"/> are measured from this corner, and the generator moves the instance
        /// by this offset so the corner lands on the room's tile position.
        /// </summary>
        public Vector2Int OriginOffset;

        /// <summary>
        /// Cells the room claims beyond its rectangle, such as alcoves that stick out of a wall, measured like door locations from the
        /// room's lowest corner. Written by the packer from the <see cref="RoomClaimedCell"/> markers in the room. May be negative or past <see cref="Size"/>.
        /// </summary>
        public List<Vector2Int> ExtraCells = new List<Vector2Int>();

        /// <summary>
        /// How likely this room is to be picked compared with the others in its roomset. Zero means never.
        /// </summary>
        [Tooltip("How likely this room is to be picked compared with the others in its roomset. Zero means never.")]
        [Min(0f)]
        public float SelectionWeight = 1f;

        /// <summary>
        /// The most times this room may appear in one dungeon. Zero means no limit.
        /// </summary>
        [Tooltip("The most times this room may appear in one dungeon. Zero means no limit.")]
        [Min(0)]
        public int MaxCount;

        /// <summary>
        /// The fewest times this room must appear in one dungeon. Zero means it need not appear.
        /// </summary>
        [Tooltip("The fewest times this room must appear in one dungeon. Zero means it need not appear.")]
        [Min(0)]
        public int MinCount;

        /// <summary>
        /// What this room is for. A Start room is where the dungeon begins and is never placed anywhere else.
        /// </summary>
        [Tooltip("What this room is for. A Start room is where the dungeon begins and is never placed anywhere else.")]
        public RoomRole Role;

        /// <summary>
        /// How many floors high the room is (a stairwell room is two). Its footprint is occupied on every one of them.
        /// </summary>
        [Tooltip("How many floors high the room is. A room with stairs between two floors is 2.")]
        [Min(1)]
        public int Floors = 1;

        /// <summary>
        /// A collection of all of the connection points this room has.
        /// </summary>
        [SerializeField]
        [Tooltip("A collection of all of the connection points this room has.")]
        private List<ConnectionBlueprint> connections = new List<ConnectionBlueprint>();

        /// <summary>
        /// A non modifiable collection of this <see cref="RoomBlueprint"/>'s connections.
        /// </summary>
        public IEnumerable<ConnectionBlueprint> Connections => connections.AsReadOnly();

        /// <summary>
        /// Replaces the stored connection list. Intended for the room packer, which writes the list the generator reads.
        /// </summary>
        /// <param name="newConnections"> The connections to store, in the order they should be read.</param>
        public void SetConnections(IEnumerable<ConnectionBlueprint> newConnections)
        {
            connections = newConnections.ToList();
        }
    }
}


