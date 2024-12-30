using UnityEngine;
using SimpleRPG.InventorySystem;
using System.Linq;
using SimpleRPG.UI;
using Unity.Netcode;
using SimpleRPG.InventorySystem.LootGeneration;
using System.Collections.Generic;

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

        [Header("Loot Settings")]

        //TODO: Unity has issues with serializing interfaces, so we need to use the concrete type here. If you find a good workaround replace this.
        [SerializeField, SerializeReference]
        [Tooltip("The LootTable for this chest.")]
        private LootTable lootTable;

        new public void Start()
        {
            base.Start();
            inventory = this.GetComponent<Inventory>();

            lootMenuUI = GameObject.FindGameObjectsWithTag("UI").Select(x => x.GetComponent<LootMenuUI>()).Single(x => x != null);

            if(!this.IsSpawned)
            {
                this.GetComponent<NetworkObject>().Spawn();
            }

            if (this.IsServer || this.IsHost)
            {
                GenerateRandomInventory();
            }
        }

        /// <inheritdoc/>
        public override void FinishInteraction(Interactor interactor)
        {
            lootMenuUI.SetLootMenuUIDisplayStatus(false);
        }

        /// <inheritdoc/>
        public override void ActivateInteraction(Interactor interactor)
        {
            lootMenuUI.DisplayLootMenu(this.inventory);
        }

        /// <summary>
        /// Populates the chest with random loot.
        /// </summary>
        private void GenerateRandomInventory()
        {
            if (lootTable != null)
            {
                var loot = lootTable.GetResult();
                foreach (LootableItem item in loot)
                {
                    inventory.AddItemInstance(item.Item);
                }
            }
        }

    }
}
