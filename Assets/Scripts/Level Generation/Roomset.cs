using LevelGeneration;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "World generation/Room set")]
public class Roomset : ScriptableObject 
{
    [SerializeField]
    [Tooltip("The collection of rooms we want to use for our world generation.")]
    public List<Room> RoomCollection;

    [SerializeField]
    [Tooltip("The size of each world cell in unity units.")]
    public int cellSize;
}
