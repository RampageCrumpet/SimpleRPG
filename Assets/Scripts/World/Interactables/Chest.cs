using UnityEngine;
using SimpleRPG.InventorySystem;

namespace SimpleRPG.ObjectInteractions
{
    public class Chest : Interactable
    {
        Inventory Inventory;

        public override void FinishInteraction()
        {
            Debug.Log("Chest opened!");
        }

    }
}
