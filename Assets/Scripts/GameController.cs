using SimpleRPG.Gameplay;
using SimpleRPG.LevelGeneration;
using Unity.AI.Navigation;
using Unity.Netcode;
using UnityEngine;

namespace SimpleRPG
{
    /// <summary>
    /// Drives session start-up.
    /// Authority model: the server owns the world. It picks a random seed, generates the dungeon, spawns its network objects
    /// (chests, enemies) and builds the only navmesh (AI runs on the server). Clients rebuild the dungeon's geometry from the
    /// seed and receive everything else through Netcode spawns and replicated state.
    /// </summary>
    public class GameController : NetworkBehaviour
    {
        [field: SerializeField] public bool UseRandomSeed { get; set; } = true;
        [field: SerializeField] public int Seed { get; set; } = 42;
        /// <summary>
        /// The controller for the level generator we want to use.
        /// </summary>
        private LevelGeneratorController levelGeneratorController;

        /// <summary>
        /// The navmesh surface we want to use to use for pathfinding.
        /// </summary>
        private NavMeshSurface navMeshSurface;

        // References are resolved in Awake because OnNetworkSpawn can run before Start.
        private void Awake()
        {
            levelGeneratorController = this.GetComponentInChildren<LevelGeneratorController>();
            navMeshSurface = this.GetComponentInChildren<NavMeshSurface>();
        }

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();

            if (IsServer)
            {
                int seed = UseRandomSeed ? CreateSeed() : Seed;
                // One logged seed reproduces the layout and every loot and combat roll that follows.
                Debug.Log($"Session seed: {seed}");
                GameplayServices.Current.Random = new SeededRandom(seed);
                levelGeneratorController.GenerateLevelAcrossNetwork(seed);

                // Only the server runs AI, so it is the only machine that needs a navmesh.
                navMeshSurface.BuildNavMesh();
                var population = GetComponentInChildren<DungeonPopulation>();
                if (population != null)
                    population.Populate(levelGeneratorController.GeneratedRooms, levelGeneratorController.Roomset.cellSize, levelGeneratorController.GeneratedSeed);
            }
            else
            {
                levelGeneratorController.RequestLevelGenerationRpc();
            }
        }

        /// <summary>
        /// Creates a seed that differs between sessions.
        /// </summary>
        private static int CreateSeed()
        {
            return new System.Random().Next();
        }
    }
}
