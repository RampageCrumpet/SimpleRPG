using LevelGeneration;
using Unity.Netcode;
using UnityEngine;

namespace SimpleRPG.LevelGeneration
{
    /// <summary>
    /// Builds the dungeon on every machine from parameters chosen by the server.
    /// The server generates first, then tells connected clients; clients that join later ask the server for the parameters.
    /// </summary>
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
        /// The parameters the current level was generated with. Only valid once <see cref="IsGenerated"/> is true.
        /// </summary>
        private LevelGenerationParameters generatedParameters;

        /// <summary>
        /// True once this machine has generated its copy of the level.
        /// </summary>
        public bool IsGenerated { get; private set; }

        /// <summary>
        /// Generates the level on the server and tells every connected client to do the same. Server only.
        /// </summary>
        /// <param name="seed"> The random seed we want to use to generate our level. </param>
        public void GenerateLevelAcrossNetwork(int seed)
        {
            if (!IsServer)
            {
                Debug.LogError("Only the server can start level generation.");
                return;
            }

            var parameters = new LevelGenerationParameters { Seed = seed, MinimumNumberOfRooms = MinimumNumberOfRooms };

            // The server (including a host) generates directly rather than relying on receiving its own RPC.
            GenerateLevel(parameters);
            GenerateLevelRpc(parameters, RpcTarget.NotServer);
        }

        /// <summary>
        /// Called by a client that spawned after the level was generated, to receive the parameters it missed.
        /// </summary>
        [Rpc(SendTo.Server)]
        public void RequestLevelGenerationRpc(RpcParams rpcParams = default)
        {
            // If the server hasn't generated yet the client will receive the broadcast from GenerateLevelAcrossNetwork instead.
            if (!IsGenerated)
            {
                return;
            }

            GenerateLevelRpc(generatedParameters, RpcTarget.Single(rpcParams.Receive.SenderClientId, RpcTargetUse.Temp));
        }

        /// <summary>
        /// Generates the level on the receiving clients. Sent to all clients on first generation, or to a single late joiner on request.
        /// </summary>
        [Rpc(SendTo.SpecifiedInParams)]
        private void GenerateLevelRpc(LevelGenerationParameters parameters, RpcParams rpcParams = default)
        {
            GenerateLevel(parameters);
        }

        /// <summary>
        /// Call the level generator to create a level. Does nothing if this machine already has one, which also
        /// protects a client that receives both the broadcast and a reply to its own request.
        /// </summary>
        private void GenerateLevel(LevelGenerationParameters parameters)
        {
            if (IsGenerated)
            {
                return;
            }

            IsGenerated = true;
            generatedParameters = parameters;

            var levelGenerator = new DungeonGenerator(parameters.Seed, Roomset.RoomCollection, Roomset.cellSize);
            levelGenerator.RoomInstantiated = room => RoomNetworkObjects.Apply(room, IsServer);
            levelGenerator.GenerateLevel(parameters.MinimumNumberOfRooms, this.transform);
        }
    }
}
