using System;
using System.Collections.Generic;
using UnityEngine;

namespace SimpleRPG.InventorySystem.LootGeneration
{
    /// <summary>
    /// This class is used to represent the chance that no item will drop.
    /// </summary>
    [Serializable]
    public class NullLoot : LootableObject
    {
        /// <inheritdoc/>
        public override IEnumerable<LootableObject> GetResult()
        {
            // Return an empty list of lootable objects so nothing is added to the parent.
            return new List<LootableObject>();
        }
    }
}
