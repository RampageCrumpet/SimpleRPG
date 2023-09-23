using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class GenerateWorldTest : MonoBehaviour
{
    [SerializeField]
    Roomset roomset;

    [SerializeField]
    Vector2Int worldSize;

    [SerializeField]
    int minimumNumberOfRooms;
    
    // Start is called before the first frame update
    void Start()
    {
        LevelGenerator levelGenerator = new LevelGenerator((int)Time.time, worldSize, roomset.RoomCollection, roomset.cellSize);
        levelGenerator.GenerateLevel(minimumNumberOfRooms);
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
