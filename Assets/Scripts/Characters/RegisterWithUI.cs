using SimpleRPG.InventorySystem;
using SimpleRPG.UI;
using System.Globalization;
using System.Linq;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.TextCore.Text;

namespace SimpleRPG.UI
{
    /// <summary>
    /// This class is in charge of attaching the locally owned character to the UI.
    /// </summary>
    [RequireComponent(typeof(Character))]
    [RequireComponent(typeof(Interactor))]
    public class RegisterWithUI : NetworkBehaviour
    {
        public override void OnNetworkSpawn()
        {
            // Attach the character to the UI
            AttachToUI();
        }

        /// <summary>
        /// Gives the UI references to the locally owned character.
        /// </summary>
        private void AttachToUI()
        {
            // Ensure that only the owner of the character registers with the UI.
            if (IsOwner)
            {
                Character character = GetComponent<Character>();
                GameObject.FindGameObjectsWithTag("UI").Select(x => x.GetComponent<AbilityButtonControllerUI>()).Single(x => x != null).CreateAbilityButtons(character.AbilitySlots);

                GameObject.FindGameObjectsWithTag("UI").Select(x => x.GetComponentInChildren<HealthBarUI>(true)).Single(x => x != null).SetCharacter(character);

                // The UI controller must know the character's controls before the death screen binds: a character that joined already dead shows the screen at once.
                PlayerUIController playerUI = GameObject.FindGameObjectsWithTag("UI").Select(x => x.GetComponent<PlayerUIController>()).Single(x => x != null);
                playerUI.SetCharacter(character);
                playerUI.SetCharacterController(GetComponent<FirstPersonCharacterController>());
                playerUI.GetComponentInChildren<DeathScreenUI>(true)?.SetCharacter(character, playerUI);

                Interactor interactor = GetComponent<Interactor>();
                GameObject.FindGameObjectsWithTag("UI").Select(x => x.GetComponent<InteractionUI>()).Single(x => x != null).SetInteractor(interactor);

                // The inventory screens show the character's inventory, equipment, preview and stats.
                GameObject.FindGameObjectsWithTag("UI").Select(x => x.GetComponent<LootMenuUI>()).Single(x => x != null).BindPlayer(character);

                GameObject.FindGameObjectsWithTag("UI").Select(x => x.GetComponentInChildren<BuffBarUI>(true)).Single(x => x != null).SetCharacter(character);

            }
        }

    }
}
