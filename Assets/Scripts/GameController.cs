using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

public class GameController : NetworkBehaviour
{
    /// <summary>
    /// The controller for the level generator we want to use.
    /// </summary>
    private LevelGeneratorController levelGeneratorController;

    public override void OnNetworkSpawn()
    {
        if (this.IsHost || this.IsServer)
        {
            levelGeneratorController.GenerateLevelAcrossNetwork((int)Time.time);
        }
        else
        {
            levelGeneratorController.RequestLevelGenerationServerRpc();
        }
        base.OnNetworkSpawn();
    }

    // Start is called before the first frame update
    void Start()
    {
        levelGeneratorController = this.GetComponentInChildren<LevelGeneratorController>();
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
