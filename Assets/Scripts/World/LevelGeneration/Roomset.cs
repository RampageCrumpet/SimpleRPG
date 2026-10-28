using SimpleRPG.LevelGeneration;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace SimpleRPG
{
    [CreateAssetMenu(menuName = "World generation/Room set")]
    public class Roomset : ScriptableObject 
    {
        [SerializeField]
        [Tooltip("The collection of rooms we want to use for our world generation.")]
        public List<RoomBlueprint> RoomCollection;

        [SerializeField]
        [Tooltip("The size of each world cell in unity units.")]
        public float cellSize;

        [SerializeField]
        [Tooltip("The height of one floor in unity units. Only needed for roomsets with rooms or doors that span floors.")]
        public float floorHeight;
    }
}
