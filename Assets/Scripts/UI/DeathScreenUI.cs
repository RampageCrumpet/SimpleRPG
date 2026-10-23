using SimpleRPG.Controls;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SimpleRPG.UI
{
    /// <summary>
    /// Tells the local player they have died and lets them start their next life. The panel shows while the character is dead, holds the
    /// cursor free and the camera still, and asks the server to respawn the character when the player chooses to.
    /// </summary>
    public class DeathScreenUI : MonoBehaviour
    {
        /// <summary>
        /// The panel shown while the character is dead.
        /// </summary>
        [SerializeField]
        [Tooltip("The panel shown while the character is dead.")]
        private GameObject panel;

        /// <summary>
        /// The button that asks for a respawn.
        /// </summary>
        [SerializeField]
        [Tooltip("The button that asks for a respawn.")]
        private Button respawnButton;

        /// <summary>
        /// The text on the respawn button, which names the key that does the same.
        /// </summary>
        [SerializeField]
        [Tooltip("The text on the respawn button.")]
        private TMP_Text respawnLabel;

        private Character character;

        private PlayerUIController playerUI;

        /// <summary>
        /// True while the death panel is up.
        /// </summary>
        public bool IsShowing => panel != null && panel.activeSelf;

        /// <summary>
        /// Points the screen at the locally owned character, replacing the previous one, and shows the panel if that character is dead.
        /// </summary>
        public void SetCharacter(Character newCharacter, PlayerUIController controller)
        {
            Hide();
            Detach();

            character = newCharacter;
            playerUI = controller;

            if (character != null)
            {
                character.OnDeath += Show;
                character.OnRevive += Hide;
                if (!character.IsAlive) Show();
            }
        }

        private void Awake()
        {
            if (panel != null) panel.SetActive(false);
            if (respawnButton != null) respawnButton.onClick.AddListener(Respawn);
        }

        private void Update()
        {
            if (IsShowing && GameInput.Interact.WasPressedThisFrame())
            {
                Respawn();
            }
        }

        private void OnDestroy()
        {
            Detach();
            if (respawnButton != null) respawnButton.onClick.RemoveListener(Respawn);
        }

        /// <summary>
        /// Asks for the character's next life. The panel stays until the server has revived the character.
        /// </summary>
        public void Respawn()
        {
            if (character != null) character.RequestRespawn();
        }

        private void Show()
        {
            if (IsShowing) return;

            if (respawnLabel != null) respawnLabel.text = $"Respawn ({GameInput.BindingDisplay(GameInput.Interact)})";
            panel.SetActive(true);
            playerUI?.LockRotationAndShowMouse(this);
        }

        private void Hide()
        {
            if (!IsShowing) return;

            panel.SetActive(false);
            playerUI?.UnlockRotationAndHideMouse(this);
        }

        private void Detach()
        {
            if (character == null) return;

            character.OnDeath -= Show;
            character.OnRevive -= Hide;
            character = null;
        }
    }
}
