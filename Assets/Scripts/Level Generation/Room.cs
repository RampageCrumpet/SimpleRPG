using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace LevelGeneration
{
    public class Room : MonoBehaviour
    {
        /// <summary>
        /// The size of the room in Unity units.
        /// </summary>
        public Vector2Int Size;

        public List<Connection> connections = new List<Connection>();
    }
}


