using SimpleRPG;
using Unity.Netcode;
using UnityEngine;

namespace SimpleRPG.InventorySystem
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
    public class Item : NetworkSerializableScriptableObject
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

        protected override void DeserializeData<T>(BufferSerializer<T> serializer)
        {
            var itemName = string.Empty;
            serializer.SerializeValue(ref itemName);
            ItemName = itemName;

            var iconName = string.Empty;
            serializer.SerializeValue(ref iconName);
            Icon = Resources.Load<Sprite>(iconName);

            var itemSize = new Vector2Int();
            serializer.SerializeValue(ref itemSize);
            ItemSize = itemSize;
        }

        protected override void SerializeData<T>(BufferSerializer<T> serializer)
        {
            var itemName = ItemName;
            serializer.SerializeValue(ref itemName);

            // Serialize the icon's name instead of the icon itself so we don't have to send images across the network.
            string iconName = Icon != null ? Icon.name : string.Empty;
            serializer.SerializeValue(ref iconName);

            var itemSize = ItemSize;
            serializer.SerializeValue(ref itemSize);
        }
    }
}


