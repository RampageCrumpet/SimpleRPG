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
                GameObject playerInstance = Instantiate(playerPrefab);
                playerInstance.GetComponent<NetworkObject>().SpawnWithOwnership(this.OwnerClientId, true);
            }

            if (IsOwner)
            {
                GameObject.FindGameObjectsWithTag("UI").Select(x => x.GetComponent<PlayerUIController>()).Single(x => x != null).CreateAbilityButtons(character);
            }
        }
    }
}