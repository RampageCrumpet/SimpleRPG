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
            if (NetworkManager.Singleton.StartHost()) gameObject.SetActive(false);
        });

        startClientButton.onClick.AddListener(() =>
        {
            if (NetworkManager.Singleton.StartClient()) gameObject.SetActive(false);
        });

        startDedicatedButton.onClick.AddListener(() =>
        {
            if (NetworkManager.Singleton.StartServer()) gameObject.SetActive(false);
        });
    }
}
