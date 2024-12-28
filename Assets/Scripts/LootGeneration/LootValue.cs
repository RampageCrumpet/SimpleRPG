using System;
using System.Collections.Generic;
using UnityEngine;

namespace SimpleRPG.InventorySystem.LootGeneration
{
    public class LootValue<T> : LootableObject
    {
        public override IEnumerable<LootableObject> GetResult()
        {
            throw new NotImplementedException();
        }
    }
}
