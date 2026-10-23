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

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();
            if (IsServer) StartCoroutine(SpawnWhenDungeonReady());
        }

        private IEnumerator SpawnWhenDungeonReady()
        {
            var population = UnityEngine.Object.FindAnyObjectByType<SimpleRPG.LevelGeneration.DungeonPopulation>();
            while (population != null && !population.IsReady) yield return null;
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
                Vector3 spawnPosition = population != null ? population.PlayerSpawnPosition + Vector3.up * 0.15f : new Vector3(0, 5, 0);
                GameObject playerInstance = Instantiate(playerPrefab, spawnPosition, playerPrefab.transform.rotation);
                playerInstance.GetComponent<NetworkObject>().SpawnWithOwnership(this.OwnerClientId, true);

                // Ensure that the server has the connection betweeen the character and the client.
                character = playerInstance.GetComponent<Character>();
                character.Initialize();

                SetupCharacterAcrossNetwork_ClientRPC(playerInstance.GetComponent<NetworkObject>().NetworkObjectId);
            }
        }

        /// <summary>
        /// Setups the connection between the character and client across the network.
        /// </summary>
        [ClientRpc]
        private void SetupCharacterAcrossNetwork_ClientRPC(ulong playerInstanceNetworkID)
        {
            // Find and Initialize the character on the client. If the client is also the server the character may already be initialized.
            if(character == null)
            {
                character = FindObjectsByType<NetworkObject>().Single(x => x.NetworkObjectId == playerInstanceNetworkID).GetComponent<Character>();
                character.Initialize();
            }
        }
    }
}
