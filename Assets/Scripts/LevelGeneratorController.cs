using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

public class LevelGeneratorController : NetworkBehaviour
{
    [field: SerializeField]
    [Tooltip("The set of rooms we want to spawn")]
    /// <summary>
    /// The <see cref="Roomset"/> that contains the rooms we want to spawn.
    /// </summary>
    public Roomset Roomset { get; set; }

    /// <summary>
    /// The minimum number of rooms we want to spawn in our dungeon.
    /// </summary>
    [field: SerializeField]
    [Tooltip("The number of rooms we want to spawn.")]
    int MinimumNumberOfRooms { get; set; }

    public void GenerateLevel()
    {
        int seed = (int)Time.time;

        // Generate the level if this is a headless server.
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
        // On
        if(this.IsClient)
        {
            GenerateLevel(seed);
        }
    }

    /// <summary>
    /// Call the level generator to create a level.
    /// </summary>
    /// <param name="seed">The seed we want to use to generate a level.</param>
    private void GenerateLevel(int seed)
    {
        LevelGenerator levelGenerator = new LevelGenerator(seed, Roomset.RoomCollection, Roomset.cellSize);
        levelGenerator.GenerateLevel(MinimumNumberOfRooms, this.transform);
    }
}
