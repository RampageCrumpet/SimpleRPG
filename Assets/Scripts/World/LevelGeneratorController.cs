using LevelGeneration;
using System.Collections.Generic;
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
        /// The most rooms we allow in our dungeon. Zero lets the generator choose from the minimum.
        /// </summary>
        [field: SerializeField]
        [Tooltip("The most rooms we allow. Zero lets the generator choose from the minimum.")]
        public int MaximumNumberOfRooms { get; set; }

        /// <summary>
        /// How many floors the dungeon may span. One gives a flat dungeon.
        /// </summary>
        [field: SerializeField]
        [Tooltip("How many floors the dungeon may span. The roomset needs vertical rooms for more than one to be used.")]
        public int FloorCount { get; set; } = 1;

        /// <summary>
        /// The parameters the current level was generated with. Only valid once <see cref="IsGenerated"/> is true.
        /// </summary>
        private LevelGenerationParameters generatedParameters;

        /// <summary>
        /// True while a level is being built, so a second request that arrives meanwhile is ignored.
        /// </summary>
        private bool isGenerating;

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

            var parameters = new LevelGenerationParameters
            {
                Seed = seed,
                MinimumNumberOfRooms = MinimumNumberOfRooms,
                MaximumNumberOfRooms = MaximumNumberOfRooms,
                FloorCount = Mathf.Max(1, FloorCount),
            };

            // The server (including a host) generates directly rather than relying on receiving its own RPC.
            // Generating fills in the fingerprints, so clients are sent the finished parameters.
            if (GenerateLevel(parameters, out LevelGenerationParameters finished))
            {
                GenerateLevelRpc(finished, RpcTarget.NotServer);
            }
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
            GenerateLevel(parameters, out _);
        }

        /// <summary>
        /// Call the level generator to create a level. Does nothing if this machine already has one, which also
        /// protects a client that receives both the broadcast and a reply to its own request.
        /// On the server the returned parameters carry the fingerprints clients check; a client refuses to build a level that differs from the server's.
        /// </summary>
        /// <returns> True if the level was built here.</returns>
        private bool GenerateLevel(LevelGenerationParameters parameters, out LevelGenerationParameters finished)
        {
            finished = parameters;

            if (IsGenerated || isGenerating)
            {
                return false;
            }

            isGenerating = true;
            try
            {
                List<RoomDefinition> definitions = RoomDefinitionBaker.Bake(Roomset);

                // A client with different room data would build a different dungeon from the same seed.
                int roomsetFingerprint = LayoutFingerprint.OfRoomset(definitions);
                if (!IsServer && LayoutFingerprint.Check(parameters.RoomsetFingerprint, roomsetFingerprint) is string roomsetMismatch)
                {
                    Debug.LogError($"Not generating the dungeon: this client's roomset differs from the server's. {roomsetMismatch}");
                    return false;
                }

                // The layout is worked out from the seed alone, so every machine reaches the same one; only then is anything instantiated.
                var layoutGenerator = new LayoutGenerator(parameters.Seed, definitions);
                GenerationResult result = layoutGenerator.Generate(parameters.MinimumNumberOfRooms, parameters.MaximumNumberOfRooms, parameters.FloorCount);

                int layoutChecksum = LayoutFingerprint.OfLayout(result.Layout);
                if (!IsServer && LayoutFingerprint.Check(parameters.LayoutChecksum, layoutChecksum) is string layoutMismatch)
                {
                    Debug.LogError($"Not generating the dungeon: this client reached a different layout than the server. {layoutMismatch}");
                    return false;
                }

                if (IsServer)
                {
                    parameters.RoomsetFingerprint = roomsetFingerprint;
                    parameters.LayoutChecksum = layoutChecksum;
                }

                foreach (string rejection in result.RejectedAttempts)
                {
                    Debug.LogWarning($"Dungeon seed {parameters.Seed}, {rejection}");
                }

                if (!result.Success)
                {
                    Debug.LogError(result.ToString());
                }

                var spawner = new LevelSpawner(Roomset.RoomCollection, Roomset.cellSize, Roomset.floorHeight);
                spawner.RoomInstantiated = room => RoomNetworkObjects.Apply(room, IsServer);
                spawner.Spawn(result.Layout, this.transform);

                generatedParameters = parameters;
                finished = parameters;
                IsGenerated = true;
                return true;
            }
            finally
            {
                isGenerating = false;
            }
        }
    }
}
