using SimpleRPG;
using SimpleRPG.UI;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Unity.Netcode;
using UnityEngine;

namespace SimpleRPG
{
    public class Player : NetworkBehaviour
    {
        /// <summary>
        /// The prefab we want to use to spawn our player character.
        /// </summary>
        [SerializeField]
        [Tooltip("The prefab we want to use to spawn our player character.")]
        private GameObject playerPrefab;

        /// <summary>
        /// The character our player is controlling.
        /// </summary>
        [HideInInspector]
        [Tooltip("Our player's character")]
        public Character character;

        /// <summary>
        /// The spawned character, replicated so clients that join later receive it too. Written once by the server.
        /// </summary>
        private readonly NetworkVariable<NetworkObjectReference> characterReference = new NetworkVariable<NetworkObjectReference>();

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();
            if (IsServer) StartCoroutine(SpawnWhenDungeonReady());
            else StartCoroutine(BindCharacterWhenAvailable());
        }

        /// <summary>
        /// Clients wait for the replicated reference and for the character object it points at, which may arrive after the player object.
        /// </summary>
        private IEnumerator BindCharacterWhenAvailable()
        {
            while (IsSpawned && character == null)
            {
                if (characterReference.Value.TryGet(out NetworkObject characterObject, NetworkManager))
                {
                    character = characterObject.GetComponent<Character>();
                    character.Initialize();
                    yield break;
                }

                yield return null;
            }
        }

        private IEnumerator SpawnWhenDungeonReady()
        {
            var population = UnityEngine.Object.FindAnyObjectByType<SimpleRPG.LevelGeneration.DungeonPopulation>();
            while (population != null && !population.IsReady && !population.HasFailed) yield return null;
            if (population != null && population.HasFailed)
                Debug.LogError($"Spawning player {OwnerClientId} at the fallback position because dungeon population failed: {population.FailureReason}", this);
            if (IsSpawned) SpawnCharacter();
        }

        /// <summary>
        /// Creates the character object across the network.
        /// </summary>
        private void SpawnCharacter()
        {
            // Ensure that only the server tries to spawn a character.
            if (IsServer)
            {
                var population = UnityEngine.Object.FindAnyObjectByType<SimpleRPG.LevelGeneration.DungeonPopulation>();
                Vector3 spawnPosition = population != null && population.IsReady ? population.PlayerSpawnPosition + Vector3.up * 0.15f : new Vector3(0, 5, 0);
                GameObject playerInstance = Instantiate(playerPrefab, spawnPosition, playerPrefab.transform.rotation);
                playerInstance.GetComponent<NetworkObject>().SpawnWithOwnership(this.OwnerClientId, true);

                // Ensure that the server has the connection betweeen the character and the client.
                character = playerInstance.GetComponent<Character>();
                character.Initialize();

                characterReference.Value = new NetworkObjectReference(playerInstance);
            }
        }
    }
}
