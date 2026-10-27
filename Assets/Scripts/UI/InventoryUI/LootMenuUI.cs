using SimpleRPG.Controls;
using SimpleRPG.InventorySystem;
using UnityEngine;

namespace SimpleRPG.UI
{
    /// <summary>
    /// The inventory screens. One screen root holds the stat sheet, the player's panel (paper doll and inventory) and the interacted object's panel,
    /// and this class arranges them for the open state:
    /// <list type="bullet">
    /// <item><description><see cref="ScreenMode.Inventory"/>: stat sheet on the left, the player's panel wide on the right.</description></item>
    /// <item><description><see cref="ScreenMode.Interactable"/>: the player's panel on the left, the object's inventory on the right, with a paper doll only if the object has equipment.</description></item>
    /// </list>
    /// The panels are the same objects in both, moved between the two layouts; inventory and equipment state live in their own components.
    /// </summary>
    public class LootMenuUI : MonoBehaviour
    {
        /// <summary>
        /// Which screen is open.
        /// </summary>
        public enum ScreenMode
        {
            Closed,
            Inventory,
            Interactable,
        }

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

        [SerializeField]
        [Tooltip("The root of every inventory panel. Hidden while no screen is open.")]
        private GameObject screenRoot;

        [SerializeField]
        [Tooltip("The stat sheet panel. Shown only on the inventory screen.")]
        private GameObject statPanel;

        [SerializeField]
        [Tooltip("The player's panel: paper doll and inventory.")]
        private RectTransform playerPanel;

        [SerializeField]
        [Tooltip("The interacted object's panel. Shown only on the interactable screen.")]
        private GameObject targetPanel;

        [SerializeField]
        [Tooltip("The player's paper doll.")]
        private PaperDollUI playerPaperDoll;

        [SerializeField]
        [Tooltip("The character preview on the player's paper doll.")]
        private CharacterPreviewUI playerPreview;

        [SerializeField]
        [Tooltip("The stat sheet.")]
        private StatSheetUI statSheet;

        [SerializeField]
        [Tooltip("The interacted object's paper doll. Shown only when the object has equipment.")]
        private PaperDollUI targetPaperDoll;

        [SerializeField]
        [Tooltip("The interacted object's inventory region, moved up when there is no paper doll above it.")]
        private RectTransform targetInventoryRegion;

        [Header("Layout, in 1920x1080 reference units from the top left")]
        [SerializeField] private Rect playerPanelInventoryRect = new Rect(700f, 100f, 1180f, 940f);
        [SerializeField] private Rect playerPanelInteractableRect = new Rect(40f, 100f, 900f, 940f);
        [SerializeField] private float targetInventoryYWithDoll = 352f;
        [SerializeField] private float targetInventoryYWithoutDoll = 68f;

        /// <summary>
        /// The <see cref="PlayerUIController"/> in charge of displaying the player's UI.
        /// </summary>
        private PlayerUIController playerUIController;

        /// <summary>
        /// The screen that is open now.
        /// </summary>
        public ScreenMode Mode { get; private set; }

        /// <summary>
        /// True if the interacted object's paper doll is showing.
        /// </summary>
        public bool TargetPaperDollVisible => targetPaperDoll != null && targetPaperDoll.gameObject.activeSelf;

        public void Start()
        {
            playerUIController = this.gameObject.GetComponentInParent<PlayerUIController>();
            Apply(ScreenMode.Closed);
        }

        /// <summary>
        /// Shows the local character's equipment, inventory, preview and stats on the screens.
        /// </summary>
        public void BindPlayer(Character character)
        {
            PlayerInventoryUI.SetInventory(character.GetComponent<Inventory>());
            playerPaperDoll.Bind(character.GetComponent<Equipment>());
            if (playerPreview != null)
            {
                playerPreview.Bind(character);
            }

            if (statSheet != null)
            {
                statSheet.Bind(character);
            }
        }

        private void Update()
        {
            if (playerUIController == null || !PlayerInventoryUI.HasInventory) return;

            // The dead handle no items: close whatever is open and ignore the inventory keys.
            if (!playerUIController.IsLocalCharacterAlive)
            {
                if (Mode != ScreenMode.Closed)
                {
                    SetLootMenuUIDisplayStatus(false);
                }

                return;
            }

            if (GameInput.Inventory.WasPressedThisFrame())
            {
                if (Mode == ScreenMode.Closed) Open(ScreenMode.Inventory);
                else Close();
            }
            else if (GameInput.Cancel.WasPressedThisFrame() && Mode != ScreenMode.Closed)
            {
                Close();
            }
        }

        /// <summary>
        /// Display the loot menu UI.
        /// </summary>
        /// <param name="lootInventory"> The inventory to be displayed.</param>
        public void DisplayLootMenu(Inventory lootInventory)
        {
            LootInventoryUI.SetInventory(lootInventory);

            // Entities with equipment, such as a corpse, show it; a chest does not reserve room for it.
            bool hasEquipment = lootInventory.TryGetComponent(out Equipment equipment);
            targetPaperDoll.Bind(hasEquipment ? equipment : null);
            Open(ScreenMode.Interactable);
        }

        /// <summary>
        /// Show's or hides the loot menu UI.
        /// </summary>
        /// <param name="displayStatus"> True if we want the UI to be visible, false if we want the UI to be hidden</param>
        public void SetLootMenuUIDisplayStatus(bool displayStatus)
        {
            if (displayStatus) Open(ScreenMode.Interactable);
            else Close();
        }

        /// <summary>
        /// Opens a screen, replacing the one that is open.
        /// </summary>
        public void Open(ScreenMode mode)
        {
            bool wasClosed = Mode == ScreenMode.Closed;
            Apply(mode);

            // The camera is locked once however many screens open in a row.
            if (wasClosed && playerUIController != null) playerUIController.LockRotationAndShowMouse(this);
        }

        /// <summary>
        /// Closes whichever screen is open. The inventory and equipment underneath are untouched.
        /// </summary>
        public void Close()
        {
            Apply(ScreenMode.Closed);
            if (playerUIController != null) playerUIController.UnlockRotationAndHideMouse(this);
        }

        private void Apply(ScreenMode mode)
        {
            Mode = mode;
            bool open = mode != ScreenMode.Closed;
            bool interactable = mode == ScreenMode.Interactable;
            screenRoot.SetActive(open);
            statPanel.SetActive(mode == ScreenMode.Inventory);
            targetPanel.SetActive(interactable);

            if (!open)
            {
                return;
            }

            Place(playerPanel, interactable ? playerPanelInteractableRect : playerPanelInventoryRect);

            if (interactable)
            {
                bool hasDoll = targetPaperDoll.Equipment != null;
                targetPaperDoll.gameObject.SetActive(hasDoll);
                targetInventoryRegion.anchoredPosition = new Vector2(targetInventoryRegion.anchoredPosition.x, -(hasDoll ? targetInventoryYWithDoll : targetInventoryYWithoutDoll));
            }
        }

        /// <summary>
        /// Positions a panel from reference-resolution coordinates: x and y from the top left of the screen layout, y down.
        /// </summary>
        private static void Place(RectTransform panel, Rect rect)
        {
            panel.anchorMin = panel.anchorMax = new Vector2(0f, 1f);
            panel.pivot = new Vector2(0f, 1f);
            panel.anchoredPosition = new Vector2(rect.x, -rect.y);
            panel.sizeDelta = rect.size;
        }
    }
}
