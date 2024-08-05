using UnityEngine;

namespace Inventory
{
    public enum EquipmentSlot
    {
        neck,
        head,
        chest,
        legs,
        feet,
        hands,
        mainHand,
        offhand,
        Consumable
    }

    /// <summary>
    /// A serializable base class containing information about items.
    /// </summary>
    [CreateAssetMenu(fileName = "Item", menuName = "Items/Item")]
    public class Item : ScriptableObject
    {
        [field: SerializeField]
        [Tooltip("The image for the item to be displayed in the users Inventory.")]
        public Sprite Icon { get; private set; }

        [field: SerializeField]
        [Tooltip("The name of the item to be displayed in game.")]
        public string ItemName { get; private set; }

        /// <summary>
        /// The total size of the item in the inventory grid.
        /// </summary>
        [field: SerializeField]
        [Tooltip("The total size of the item in the inventory grid.")]
        public Vector2Int ItemSize { private set; get; }
    }
}


