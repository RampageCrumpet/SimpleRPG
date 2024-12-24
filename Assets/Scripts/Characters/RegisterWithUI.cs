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
                GameObject.FindGameObjectsWithTag("UI").Select(x => x.GetComponent<AbilityButtonControllerUI>()).Single(x => x != null).CreateAbilityButtons(character.PersonalAbilities);

                Interactor interactor = GetComponent<Interactor>();
                GameObject.FindGameObjectsWithTag("UI").Select(x => x.GetComponent<InteractionUI>()).Single(x => x != null).SetInteractor(interactor);

                Inventory inventory = GetComponent<Inventory>();
                GameObject.FindGameObjectsWithTag("UI").Select(x => x.GetComponent<LootMenuUI>()).Single(x => x != null).PlayerInventoryUI.SetInventory(inventory);

                FirstPersonCharacterController firstPersonCharacterController = GetComponent<FirstPersonCharacterController>();
                GameObject.FindGameObjectsWithTag("UI").Select(x => x.GetComponent<PlayerUIController>()).Single(x => x != null).SetCharacterController(firstPersonCharacterController);
            }
        }

    }
}
