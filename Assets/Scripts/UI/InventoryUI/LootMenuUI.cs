using SimpleRPG.InventorySystem;
using UnityEngine;

namespace SimpleRPG.UI
{
    public class LootMenuUI : MonoBehaviour
    {
        /// <summary>
        /// The players inventory UI.
        /// </summary>
        [field: SerializeField]
        [Tooltip("The players inventory UI.")]
        public InventoryUI PlayerInventoryUI { get; private set; }

        /// <summary>
        /// The <see cref="InventoryUI"/> representing the object we're looting.
        /// </summary>
        [field: SerializeField]
        [Tooltip("The loot inventory UI.")]
        public InventoryUI LootInventoryUI { get; private set; }

        /// <summary>
        /// The <see cref="PlayerUIController"/> in charge of displaying the player's UI.
        /// </summary>
        private PlayerUIController playerUIController;

        public void Start()
        {
            playerUIController = this.gameObject.GetComponentInParent<PlayerUIController>();
        }

        /// <summary>
        /// Display the loot menu UI.
        /// </summary>
        /// <param name="lootInventory"> The inventory to be displayed.</param>
        public void DisplayLootMenu(Inventory lootInventory)
        {
            LootInventoryUI.SetInventory(lootInventory);
            SetLootMenuUIDisplayStatus(true);
        }

        /// <summary>
        /// Show's or hides the loot menu UI.
        /// </summary>
        /// <param name="displayStatus"> True if we want the UI to be visible, false if we want the UI to be hidden</param>
        public void SetLootMenuUIDisplayStatus(bool displayStatus)
        {
            PlayerInventoryUI.gameObject.SetActive(displayStatus);
            LootInventoryUI.gameObject.SetActive(displayStatus);

            // If we're displaying the loot menu, lock the camera rotation and show the mouse, when we're done release it.
            if (displayStatus)
            {
                playerUIController.LockRotationAndShowMouse(this);
            }
            else
            {
                playerUIController.UnlockRotationAndHideMouse(this);
            }
        }
    }
}
