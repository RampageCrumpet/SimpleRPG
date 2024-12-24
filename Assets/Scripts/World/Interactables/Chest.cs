using UnityEngine;
using SimpleRPG.InventorySystem;
using System.Linq;
using SimpleRPG.UI;

namespace SimpleRPG.ObjectInteractions
{
    [RequireComponent(typeof(Inventory))]
    public class Chest : Interactable
    {
        /// <summary>
        /// The inventory of the chest.
        /// </summary>
        Inventory inventory;

        /// <summary>
        /// The UI for the loot menu.
        /// </summary>
        LootMenuUI lootMenuUI;

        new public void Start()
        {
            base.Start();
            inventory = this.GetComponent<Inventory>();

            lootMenuUI = GameObject.FindGameObjectsWithTag("UI").Select(x => x.GetComponent<LootMenuUI>()).Single(x => x != null);


        }

        /// <inheritdoc cref="Interactable.FinishInteraction(Character)"/>
        public override void FinishInteraction(Interactor interactor)
        {
            lootMenuUI.SetLootMenuUIDisplayStatus(false);
        }

        public override void ActivateInteraction(Interactor interactor)
        {
            lootMenuUI.DisplayLootMenu(this.inventory);
        }
    }
}
