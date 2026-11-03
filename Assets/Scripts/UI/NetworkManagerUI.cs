using System.Collections;
using System.Collections.Generic;
using SimpleRPG.Networking;
using SimpleRPG.UI;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;

namespace SimpleRPG
{
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

        /// <summary>
        /// The most players, a host included, a session started from here takes. A dungeon that suits few players can still be capped
        /// low while the rest of the game is built to take more.
        /// </summary>
        [SerializeField]
        [Min(0)]
        [Tooltip("The most players, a host included, a session started from here accepts. 0 means no limit.")]
        private int maxPlayers = 4;

        private void Awake()
        {
            startServerButton.onClick.AddListener(() =>
            {
                SessionHandshake.InstallOnServer(NetworkManager.Singleton, maxPlayers);
                if (NetworkManager.Singleton.StartHost()) gameObject.SetActive(false);
            });

            startClientButton.onClick.AddListener(() =>
            {
                SessionHandshake.PrepareClient(NetworkManager.Singleton);

                // A client that loses its connection, or is refused (a full server, another build), has nothing to play: back to the menu.
                NetworkManager.Singleton.OnClientDisconnectCallback += HandleClientDisconnect;
                if (NetworkManager.Singleton.StartClient()) gameObject.SetActive(false);
                else NetworkManager.Singleton.OnClientDisconnectCallback -= HandleClientDisconnect;
            });

            startDedicatedButton.onClick.AddListener(() =>
            {
                SessionHandshake.InstallOnServer(NetworkManager.Singleton, maxPlayers);
                if (NetworkManager.Singleton.StartServer()) gameObject.SetActive(false);
            });
        }

        private void HandleClientDisconnect(ulong clientId)
        {
            NetworkManager manager = NetworkManager.Singleton;
            if (manager == null || manager.IsServer || clientId != manager.LocalClientId)
            {
                return;
            }

            Debug.Log("Disconnected from the server" + (string.IsNullOrEmpty(manager.DisconnectReason) ? "." : ": " + manager.DisconnectReason));
            manager.OnClientDisconnectCallback -= HandleClientDisconnect;
            MainMenuReturn.Begin();
        }

        private void OnDestroy()
        {
            if (NetworkManager.Singleton != null)
            {
                NetworkManager.Singleton.OnClientDisconnectCallback -= HandleClientDisconnect;
            }
        }
    }
}
