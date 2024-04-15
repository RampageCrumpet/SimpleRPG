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
    public int MinimumNumberOfRooms { get; set; }

    /// <summary>
    /// The level generator we're using to generate our level.
    /// </summary>
    private LevelGenerator levelGenerator;

    /// <summary>
    /// Call the level generator across the network.
    /// </summary>
    /// <param name="seed"> The random seed we want to use to generate our level. </param>
    public void GenerateLevelAcrossNetwork(int seed)
    {
        // Generate the level if this is a headless server.
        if (this.IsServer && !this.IsHost)
        {
            GenerateLevel(seed);
        }
        // Call the RPC that will generate this level on every client including non headless servers.
        GenerateLevelClientRpc(seed);
    }


    [ServerRpc(RequireOwnership = false)]
    public void RequestLevelGenerationServerRpc(ServerRpcParams serverRpcParams = default)
    {
        // Find the sending clients ID and prepare to send the message back to them.
        ClientRpcParams clientRpcParams = new ClientRpcParams()
        {
            Send = new ClientRpcSendParams()
            {
                TargetClientIds = new ulong[] { serverRpcParams.Receive.SenderClientId }
            }
        };
        GenerateLevelClientRpc(levelGenerator.Seed, clientRpcParams);
    }

    /// <summary>
    /// Invokes the generate level code on all of the connected clients.
    /// </summary>
    /// <param name="seed"> The seed we want to send the clients to generate the level</param>
    /// <param name="clientRpcParams"> The clients we want to send the command to. Defaults to all clients. </param>
    [ClientRpc]
    private void GenerateLevelClientRpc(int seed, ClientRpcParams clientRpcParams = default)
    {
        GenerateLevel(seed);
    }

    /// <summary>
    /// Call the level generator to create a level.
    /// </summary>
    /// <param name="seed">The seed we want to use to generate a level.</param>
    private void GenerateLevel(int seed)
    {
        levelGenerator = new LevelGenerator(seed, Roomset.RoomCollection, Roomset.cellSize);
        levelGenerator.GenerateLevel(MinimumNumberOfRooms, this.transform);
    }
}
