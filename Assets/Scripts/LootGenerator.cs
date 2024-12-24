using NUnit.Framework;
using SimpleRPG.InventorySystem;
using System.Collections.Generic;
using UnityEngine;

namespace SimpleRPG.InventorySystem
{
    public class LootGenerator : MonoBehaviour
    {
        /// <summary>
        /// The inventory we're generating loot into.
        /// </summary>
        private Inventory inventory;



        // Start is called once before the first execution of Update after the MonoBehaviour is created
        void Start()
        {
            inventory = this.GetComponent<Inventory>();
        }

        // Update is called once per frame
        void Update()
        {
        
        }
    }
}
