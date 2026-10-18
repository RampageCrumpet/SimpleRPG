using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Data;
using System.Linq;
using UnityEngine;

namespace LevelGeneration
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
        /// A collection of all of the connection points this room has.
        /// </summary>
        [SerializeField]
        [Tooltip("A collection of all of the connection points this room has.")]
        private List<ConnectionBlueprint> connections = new List<ConnectionBlueprint>();

        /// <summary>
        /// A non modifiable collection of this <see cref="RoomBlueprint"/>'s connections.
        /// </summary>
        public IEnumerable<ConnectionBlueprint> Connections { get => connections.AsReadOnly(); private set { connections = value.ToList(); } }
    }
}


