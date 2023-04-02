using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// This class manages a UI used to determine which state we want to start our connection as.
/// </summary>
public class NetworkManagerUI : MonoBehaviour
{
    /// <summary>
    /// The button we want to use to start as a client and connect to the server.
    /// </summary>
    [SerializeField]
    [Tooltip("The button we want to use to start as a client.")]
    private Button startClientButton;

    /// <summary>
    /// The button we want to use to start as a server.
    /// </summary>
    [SerializeField]
    [Tooltip("The button we want to use to start as a server able to play the game.")]
    private Button startServerButton;

    /// <summary>
    /// The button we want to use to start as a client and connect to the server.
    /// </summary>
    [SerializeField]
    [Tooltip("The button we want to use to start as a dedicated server.")]
    private Button startDedicatedButton;

    private void Awake()
    {
        startServerButton.onClick.AddListener(() =>
        {
            NetworkManager.Singleton.StartServer();
        });

        startClientButton.onClick.AddListener(() =>
        {
            NetworkManager.Singleton.StartClient();
        });

        startDedicatedButton.onClick.AddListener(() =>
        {
            NetworkManager.Singleton.StartHost();
        });
    }

    // Start is called before the first frame update
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
