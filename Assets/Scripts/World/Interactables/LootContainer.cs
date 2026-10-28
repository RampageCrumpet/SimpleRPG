using UnityEngine;
using SimpleRPG.InventorySystem;
using System.Linq;
using SimpleRPG.UI;
using SimpleRPG.InventorySystem.LootGeneration;
using SimpleRPG.Audio;

namespace SimpleRPG.ObjectInteractions
{
    /// <summary>
    /// Something the player can open to take items from. The server rolls its contents from a loot table and owns them; clients see the
    /// contents through the replicated <see cref="Inventory"/>. A subclass decides when the contents are rolled (a chest when it spawns, a
    /// corpse when it dies) by calling <see cref="RollLoot"/>.
    /// </summary>
    [RequireComponent(typeof(Inventory))]
    public abstract class LootContainer : Interactable
    {
        /// <summary>
        /// How far beyond <see cref="Interactable.InteractionRange"/> the server still lets a client move items,
        /// to absorb the difference between the client's and the server's view of the player's position.
        /// </summary>
        private const float ServerAccessSlack = 2f;

        /// <summary>
        /// The inventory holding the contents.
        /// </summary>
        protected Inventory Inventory { get; private set; }

        /// <summary>
        /// The UI for the loot menu. Looked up on first use because a dedicated server has no UI.
        /// </summary>
        LootMenuUI lootMenuUI;

        [Header("Loot Settings")]

        [SerializeField]
        [Tooltip("The loot table the contents are rolled from.")]
        private WeightedRandomTable lootTable;

        public override void Start()
        {
            base.Start();
            Inventory = this.GetComponent<Inventory>();
        }

        /// <inheritdoc/>
        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();

            // Only the server owns the contents; clients receive them through the inventory's replicated state.
            if (this.IsServer)
            {
                Inventory = this.GetComponent<Inventory>();
                Inventory.SharedAccessRange = InteractionRange + ServerAccessSlack;
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
            GetLootMenuUI()?.DisplayLootMenu(this.Inventory);
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
        /// Rolls the loot table into the inventory. Server only.
        /// </summary>
        protected void RollLoot()
        {
            if (lootTable != null)
            {
                // Only lootable items can go in a container; other entry types and items without an Item are skipped.
                foreach (LootableItem lootable in lootTable.GetResult(GameplayRandomSource.Current).OfType<LootableItem>())
                {
                    if (lootable.Item == null)
                    {
                        Debug.LogWarning($"Loot entry '{lootable.name}' in {lootTable.name} has no item assigned; skipping it.", this);
                        continue;
                    }

                    if (!Inventory.AddItemInstance(lootable.Item))
                    {
                        Debug.LogWarning($"'{lootable.Item.name}' from {lootTable.name} could not be placed in {name}.", this);
                    }
                }
            }
        }
    }
}
