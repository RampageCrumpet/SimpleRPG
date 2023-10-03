using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

public class GenerateWorldTest : NetworkBehaviour
{
    [SerializeField]
    Roomset roomset;

    [SerializeField]
    Vector2Int worldSize;

    [SerializeField]
    int minimumNumberOfRooms;
    
    public override void OnNetworkSpawn()
    {
        if(this.IsHost || this.IsServer)
        {
            GenerateLevel();
        }
        base.OnNetworkSpawn();
    }


    public void GenerateLevel()
    {
        int seed = (int)Time.time;

        if (this.IsServer)
        {
            GenerateLevel(seed);
        }
        GenerateLevelClientRpc(seed);
    }

    /// <summary>
    /// Invokes the generate level code on all of the connected clients.
    /// </summary>
    /// <param name="seed"> The seed we want to send the clients to generate the level</param>
    [ClientRpc]
    private void GenerateLevelClientRpc(int seed)
    {
        GenerateLevel(seed);
    }

    /// <summary>
    /// Call the level generator to create a level.
    /// </summary>
    /// <param name="seed">The seed we want to use to generate a level.</param>
    private void GenerateLevel(int seed)
    {
        LevelGenerator levelGenerator = new LevelGenerator(seed, worldSize, roomset.RoomCollection, roomset.cellSize);
        levelGenerator.GenerateLevel(minimumNumberOfRooms, this.transform);
    }
}
