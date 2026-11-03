using SimpleRPG.Delve;
using Unity.Netcode;
using UnityEngine;

namespace SimpleRPG.UI
{
    /// <summary>
    /// What a player sees of the delve: how far the extraction point is and how the countdown stands, and when the delve ends, how it went
    /// and a way out. It only reads the delve's replicated state; it owns none of it. When the delve ends it takes the cursor (through the same
    /// reference-counted lock the death screen uses, so the two never fight over it) so the button can be clicked.
    /// <para>
    /// A plain on-screen placeholder, created by code so it needs no scene or prefab, until the real HUD is made. The extraction marker is a
    /// flat disc made the same way.
    /// </para>
    /// </summary>
    public sealed class DelveHud : MonoBehaviour
    {
        private PlayerUIController playerUI;
        private bool holdingCursor;
        private GameObject marker;
        private Vector3 markedPosition;
        private float markedRadius;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            if (Application.isBatchMode || FindAnyObjectByType<DelveHud>() != null)
            {
                return;
            }

            var host = new GameObject("DelveHud");
            host.AddComponent<DelveHud>();
            DontDestroyOnLoad(host);
        }

        private DelveController Controller => DelveController.Find(NetworkManager.Singleton);

        private void Update()
        {
            // Leaving for the menu frees the cursor itself; releasing the lock now would hide it again just as the menu loads.
            if (MainMenuReturn.IsReturning)
            {
                holdingCursor = false;
                playerUI = null;
                DestroyMarker();
                return;
            }

            DelveController delve = Controller;
            bool ended = delve != null && delve.Phase != DelvePhase.Delving;
            HoldCursor(ended);
            ShowMarker(delve);
        }

        private void OnDestroy()
        {
            HoldCursor(false);
            DestroyMarker();
        }

        // Takes the cursor while the end-of-delve panel is up, and gives it back when it is not.
        private void HoldCursor(bool wanted)
        {
            if (wanted == holdingCursor)
            {
                return;
            }

            if (wanted)
            {
                playerUI = playerUI != null ? playerUI : FindAnyObjectByType<PlayerUIController>();
                if (playerUI == null)
                {
                    // No local player to hand the cursor to yet; try again next frame.
                    return;
                }

                playerUI.LockRotationAndShowMouse(this);
                holdingCursor = true;
            }
            else
            {
                if (playerUI != null)
                {
                    playerUI.UnlockRotationAndHideMouse(this);
                }

                holdingCursor = false;
            }
        }

        // A flat disc on the floor where the party gets out. A stand-in for real art, made here so it needs no asset.
        private void ShowMarker(DelveController delve)
        {
            if (delve == null || !delve.TryGetExtraction(out Vector3 position))
            {
                DestroyMarker();
                return;
            }

            if (marker != null && position == markedPosition && Mathf.Approximately(markedRadius, delve.ExtractionRadius))
            {
                return;
            }

            DestroyMarker();
            marker = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            marker.name = "ExtractionMarker (placeholder)";
            Destroy(marker.GetComponent<Collider>());
            marker.transform.position = position + Vector3.up * 0.03f;
            marker.transform.localScale = new Vector3(delve.ExtractionRadius * 2f, 0.03f, delve.ExtractionRadius * 2f);
            Renderer renderer = marker.GetComponent<Renderer>();
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.material.color = new Color(0.2f, 0.9f, 0.5f);
            markedPosition = position;
            markedRadius = delve.ExtractionRadius;
        }

        private void DestroyMarker()
        {
            if (marker != null)
            {
                Destroy(marker);
                marker = null;
            }
        }

        private void OnGUI()
        {
            NetworkManager manager = NetworkManager.Singleton;
            DelveController delve = Controller;
            if (delve == null || manager == null || !manager.IsClient || MainMenuReturn.IsReturning)
            {
                return;
            }

            const float width = 360f;
            GUILayout.BeginArea(new Rect((Screen.width - width) * 0.5f, 12f, width, 140f));

            if (delve.Phase != DelvePhase.Delving)
            {
                string title = delve.Phase == DelvePhase.Extracted ? "EXTRACTED" : "WIPED";
                GUILayout.Box($"{title}\n{delve.SurvivorCount} survived, {delve.DeadCount} died, {delve.DurationSeconds:0}s");
                if (GUILayout.Button("Exit dungeon"))
                {
                    MainMenuReturn.Begin();
                }
            }
            else if (delve.TryGetExtraction(out Vector3 extraction))
            {
                if (delve.ExtractionProgress > 0f)
                {
                    GUILayout.Box($"Extracting... {delve.ExtractionProgress * 100f:0}% (the whole party must stay)");
                }
                else
                {
                    Character local = PlayerCharacters.Of(manager.LocalClient);
                    GUILayout.Box(local != null ? $"{Vector3.Distance(local.transform.position, extraction):0}m to extraction" : "extraction");
                }
            }

            GUILayout.EndArea();
        }
    }
}
