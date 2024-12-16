using UnityEngine;
using SimpleRPG.InventorySystem;

namespace SimpleRPG.ObjectInteractions
{
    public class Chest : Interactable
    {
        Inventory Inventory;

        protected override void FinishInteraction()
        {
            Debug.Log("Chest opened!");
        }

    }
}
