using UnityEngine;
using SimpleRPG.InventorySystem;
using System.Linq;
using SimpleRPG.UI;
using SimpleRPG.InventorySystem.LootGeneration;
using SimpleRPG.Audio;

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

        public override void Start()
        {
            base.Start();
            inventory = this.GetComponent<Inventory>();
        }

        /// <inheritdoc/>
        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();

            // Only the server owns the contents; clients receive them through the inventory's replicated state.
            if (this.IsServer)
            {
                inventory = this.GetComponent<Inventory>();
                inventory.SharedAccessRange = InteractionRange + ServerAccessSlack;
                GenerateRandomInventory();
            }
        }

        /// <inheritdoc/>
        public override void FinishInteraction(Interactor interactor)
        {
            GetLootMenuUI()?.SetLootMenuUIDisplayStatus(false);
            InteractableAudio.TryPlay(this, InteractionCue.Close);
        }

        /// <inheritdoc/>
        public override void ActivateInteraction(Interactor interactor)
        {
            GetLootMenuUI()?.DisplayLootMenu(this.inventory);
            InteractableAudio.TryPlay(this, InteractionCue.Open);
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
                // Only lootable items can go in a chest; other entry types and items without an Item are skipped.
                foreach (LootableItem lootable in lootTable.GetResult(GameplayRandomSource.Current).OfType<LootableItem>())
                {
                    if (lootable.Item == null)
                    {
                        Debug.LogWarning($"Loot entry '{lootable.name}' in {lootTable.name} has no item assigned; skipping it.", this);
                        continue;
                    }

                    if (!inventory.AddItemInstance(lootable.Item))
                    {
                        Debug.LogWarning($"'{lootable.Item.name}' from {lootTable.name} could not be placed in {name}.", this);
                    }
                }
            }
        }

    }
}
