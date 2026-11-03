using SimpleRPG.Gameplay;
using SimpleRPG.Networking;
using System.Collections;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace SimpleRPG.UI
{
    /// <summary>
    /// Ends the session and returns the player to the main menu. The network session is shut down (a client leaving a delve leaves its character
    /// waiting, as for any dropped connection; a host leaving ends it for everyone), what the session left behind is cleared, the cursor is
    /// freed and the scene is loaded afresh, which brings the menu back.
    /// </summary>
    public sealed class MainMenuReturn : MonoBehaviour
    {
        /// <summary>True from the moment a return begins until the menu has loaded, so it cannot start twice.</summary>
        public static bool IsReturning { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Reset() => IsReturning = false;

        /// <summary>Starts the return. Does nothing if one is already under way.</summary>
        public static void Begin()
        {
            if (IsReturning)
            {
                return;
            }

            IsReturning = true;

            // The runner outlives the scene it is started in, because loading the menu destroys everything else.
            var host = new GameObject("MainMenuReturn");
            DontDestroyOnLoad(host);
            MainMenuReturn runner = host.AddComponent<MainMenuReturn>();
            runner.StartCoroutine(runner.Run());
        }

        private IEnumerator Run()
        {
            // Nothing of this session's menus or local player may carry into the next: a menu still marked open would hold the new player's controls back.
            LocalPlayer.Reset();
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;

            NetworkManager manager = NetworkManager.Singleton;
            if (manager != null && (manager.IsListening || manager.ShutdownInProgress))
            {
                if (manager.IsListening)
                {
                    manager.Shutdown();
                }

                while (manager != null && (manager.IsListening || manager.ShutdownInProgress))
                {
                    yield return null;
                }
            }

            if (manager != null)
            {
                PlayerSessions.For(manager).Clear();
            }

            // A new session starts from new gameplay state: combatants, item instance ids and item state do not carry over.
            GameplayServices.Reset();

            Scene active = SceneManager.GetActiveScene();
            AsyncOperation load = active.buildIndex >= 0 ? SceneManager.LoadSceneAsync(active.buildIndex) : SceneManager.LoadSceneAsync(active.name);
            while (load != null && !load.isDone)
            {
                yield return null;
            }

            IsReturning = false;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            Destroy(gameObject);
        }
    }
}
