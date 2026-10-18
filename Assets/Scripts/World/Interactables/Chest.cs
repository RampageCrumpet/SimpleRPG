using UnityEngine;
using SimpleRPG.InventorySystem;
using System.Linq;
using SimpleRPG.UI;
using SimpleRPG.InventorySystem.LootGeneration;

namespace SimpleRPG.ObjectInteractions
{
    /// <summary>
    /// A container the player can loot. The server spawns the chest (see <c>RoomNetworkObjects</c>), rolls its contents
    /// and owns them; clients see the contents through the chest's replicated <see cref="Inventory"/>.
    /// </summary>
    [RequireComponent(typeof(Inventory))]
    public class Chest : Interactable
    {
        /// <summary>
        /// How far beyond <see cref="Interactable.InteractionRange"/> the server still lets a client move items,
        /// to absorb the difference between the client's and the server's view of the player's position.
        /// </summary>
        private const float ServerAccessSlack = 2f;

        /// <summary>
        /// The inventory of the chest.
        /// </summary>
        Inventory inventory;

        /// <summary>
        /// The UI for the loot menu. Looked up on first use because a dedicated server has no UI.
        /// </summary>
        LootMenuUI lootMenuUI;

        [Header("Loot Settings")]

        //TODO: Unity has issues with serializing interfaces, so we need to use the concrete type here. If you find a good workaround replace this.
        [SerializeField, SerializeReference]
        [Tooltip("The LootTable for this chest.")]
        private WeightedRandomTable lootTable;

        new public void Start()
        {
            base.Start();
            inventory = this.GetComponent<Inventory>();

            // Only the server owns the contents; clients receive them through the inventory's replicated state.
            if (this.IsSpawned && this.IsServer)
            {
                inventory.SharedAccessRange = InteractionRange + ServerAccessSlack;
                GenerateRandomInventory();
            }
        }

        /// <inheritdoc/>
        public override void FinishInteraction(Interactor interactor)
        {
            GetLootMenuUI()?.SetLootMenuUIDisplayStatus(false);
        }

        /// <inheritdoc/>
        public override void ActivateInteraction(Interactor interactor)
        {
            GetLootMenuUI()?.DisplayLootMenu(this.inventory);
        }

        private LootMenuUI GetLootMenuUI()
        {
            if (lootMenuUI == null)
            {
                lootMenuUI = GameObject.FindGameObjectsWithTag("UI").Select(x => x.GetComponent<LootMenuUI>()).SingleOrDefault(x => x != null);
            }

            return lootMenuUI;
        }

        /// <summary>
        /// Populates the chest with random loot. Server only.
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
