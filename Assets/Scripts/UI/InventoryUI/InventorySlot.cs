using UnityEngine;

namespace SimpleRPG.UI
{
    /// <summary>
    /// Represents an individual UI slot in the inventory.
    /// </summary>
    public class InventorySlot : MonoBehaviour
    {
        /// <summary>
        /// This inventory slots position within it's owning inventory.
        /// </summary>
        public Vector2Int position;
    }
}
