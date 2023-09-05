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

        // Start is called before the first frame update
        void Start()
        {
            SpawnCharacter();
        }

        /// <summary>
        /// Creates the character object across the network.
        /// </summary>
        private void SpawnCharacter()
        {
            if (IsServer)
            {
                // Ensure that only the server tries to spawn a character.
                GameObject playerInstance = Instantiate(playerPrefab);
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
                character = FindObjectsOfType<NetworkObject>().Single(x => x.NetworkObjectId == playerInstanceNetworkID).GetComponent<Character>();
                character.Initialize();
            }


            if (IsOwner)
            {
                // Attach the UI to the character.
                GameObject.FindGameObjectsWithTag("UI").Select(x => x.GetComponent<PlayerUIController>()).Single(x => x != null).CreateAbilityButtons(character.PersonalAbilities);
            }
        }
    }
}