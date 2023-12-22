using System.Collections;
using System.Collections.Generic;
using Unity.AI.Navigation;
using Unity.Netcode;
using UnityEngine;

public class GameController : NetworkBehaviour
{
    /// <summary>
    /// The controller for the level generator we want to use.
    /// </summary>
    private LevelGeneratorController levelGeneratorController;

    /// <summary>
    /// The navmesh surface we want to use to use for pathfinding.
    /// </summary>
    private NavMeshSurface navMeshSurface;

    public override void OnNetworkSpawn()
    {
        if (this.IsHost || this.IsServer)
        {
            levelGeneratorController.GenerateLevelAcrossNetwork((int)Time.time);
            navMeshSurface.BuildNavMesh();
        }
        else
        {
            levelGeneratorController.RequestLevelGenerationServerRpc();
            UpdateNavmeshServerRpc();
        }
        base.OnNetworkSpawn();
    }

    // Start is called before the first frame update
    void Start()
    {
        levelGeneratorController = this.GetComponentInChildren<LevelGeneratorController>();
        navMeshSurface = this.GetComponentInChildren<NavMeshSurface>();
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    [ServerRpc]
    private void UpdateNavmeshServerRpc()
    {
        navMeshSurface.BuildNavMesh();
    }
}
