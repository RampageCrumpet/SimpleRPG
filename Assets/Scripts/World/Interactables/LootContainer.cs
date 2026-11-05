using UnityEngine;
using SimpleRPG.InventorySystem;
using System;
using System.Linq;
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
        /// Raised, on the machine whose player opened a container, with the inventory to show. The loot menu listens; with no menu (a dedicated
        /// server) nothing happens, and a container never has to find or know about the UI.
        /// </summary>
        public static event Action<Inventory> OpenRequested;

        /// <summary>
        /// Raised, on the machine whose player finished with a container, so the loot menu can close.
        /// </summary>
        public static event Action CloseRequested;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetEvents()
        {
            OpenRequested = null;
            CloseRequested = null;
        }

        [Header("Loot Settings")]

        [SerializeField]
        [Tooltip("The loot table the contents are rolled from.")]
        private WeightedRandomTable lootTable;

        [SerializeField]
        [Tooltip("How many item levels better (or worse, if negative) than the area's ordinary loot this container's contents are. The area's ceiling still holds.")]
        private int itemLevelOffset;

        /// <summary>
        /// How strong the loot where this container is can be. Set by the server where the container is placed; the lowest level otherwise.
        /// </summary>
        public LootArea Area { get; set; } = LootArea.Lowest;

        /// <summary>How much better (or worse) than the area's ordinary loot this container is, on top of what its table and entries add.</summary>
        public int ItemLevelOffset { get => itemLevelOffset; set => itemLevelOffset = value; }

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

        private const byte OpenMessage = 0;
        private const byte CloseMessage = 1;

        private bool rolled;

        /// <summary>
        /// True for a container that fills itself the first time it is opened, not when it spawns, so nothing about its contents is fixed until
        /// someone looks. A corpse and a loot bag fill at their own moments instead.
        /// </summary>
        protected virtual bool RollsWhenOpened => false;

        /// <summary>Lets the container fill itself again the next time it is opened, for a body whose character died another life.</summary>
        protected void ResetRoll() => rolled = false;

        /// <inheritdoc/>
        public override void ActivateInteraction(Interactor interactor)
        {
            // The server decides what is inside, the first time anyone looks.
            if (RollsWhenOpened && !rolled && (!IsSpawned || IsServer))
            {
                RollLoot();
            }

            NotifyInteractor(interactor, OpenMessage);
        }

        /// <inheritdoc/>
        public override void FinishInteraction(Interactor interactor)
        {
            NotifyInteractor(interactor, CloseMessage);
        }

        /// <summary>The opening player's machine shows the loot menu and plays the sound; the menu closes the same way.</summary>
        protected override void OnInteractorMessage(byte message)
        {
            if (message == OpenMessage)
            {
                OpenRequested?.Invoke(this.Inventory);
                InteractableAudio.TryPlay(this, InteractionCue.Open);
            }
            else if (message == CloseMessage)
            {
                CloseRequested?.Invoke();
                InteractableAudio.TryPlay(this, InteractionCue.Close);
            }
        }

        /// <summary>
        /// Rolls the loot table into the inventory. Server only.
        /// </summary>
        protected void RollLoot()
        {
            rolled = true;
            if (lootTable != null)
            {
                // Only lootable items can go in a container; other entry types and items without an Item are skipped.
                foreach (LootDrop drop in lootTable.GetDrops(GameplayRandomSource.Current))
                {
                    if (!(drop.Entry is LootableItem lootable))
                    {
                        continue;
                    }

                    if (lootable.Item == null)
                    {
                        Debug.LogWarning($"Loot entry '{lootable.name}' in {lootTable.name} has no item assigned; skipping it.", this);
                        continue;
                    }

                    if (!Inventory.AddItemInstance(lootable.Item, Area.LevelFor(itemLevelOffset + drop.LevelOffset)))
                    {
                        Debug.LogWarning($"'{lootable.Item.name}' from {lootTable.name} could not be placed in {name}.", this);
                    }
                }
            }
        }
    }
}
