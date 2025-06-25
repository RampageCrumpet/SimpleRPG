using System;
using System.Collections.Generic;
using UnityEngine;

namespace SimpleRPG.InventorySystem.LootGeneration
{
    public class LootValue<T> : WeightedRandomEntry
    {
        public override IEnumerable<WeightedRandomEntry> GetResult()
        {
            throw new NotImplementedException();
        }
    }
}
