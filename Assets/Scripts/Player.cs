using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Unity.Netcode;
using UnityEngine;

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
    [SerializeField]
    [Tooltip("Our player's character")]
    public Character character;

    // Start is called before the first frame update
    void Start()
    {
        SpawnCharacter();
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    private void SpawnCharacter()
    {
        if(IsServer)
        {
            GameObject playerInstance = Instantiate(playerPrefab);
            playerInstance.GetComponent<NetworkObject>().SpawnWithOwnership(this.OwnerClientId, true);
            character = playerInstance.GetComponent<Character>();
        }

        if(IsOwner)
        {
            GameObject.FindGameObjectsWithTag("UI").Select(x => x.GetComponent<PlayerUIController>()).Where(x => x != null).First().CreateAbilityButtons(character);
        }
    }
}
