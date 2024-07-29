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
    [CreateAssetMenu(fileName = "Item", menuName = "Scriptable Objects/Item")]
    public class Item : ScriptableObject
    {
        /// <summary>
        /// The total size of the item in the inventory grid.
        /// </summary>
        [SerializeField]
        [Tooltip("The total size of the item in the inventory grid.")]
        public Vector2Int itemSize { private set; get; }
    }
}


