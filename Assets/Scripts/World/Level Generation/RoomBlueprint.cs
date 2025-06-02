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


