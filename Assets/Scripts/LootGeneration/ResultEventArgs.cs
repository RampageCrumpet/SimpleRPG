using System.Collections.Generic;

namespace SimpleRPG.InventorySystem.LootGeneration
{
    public class ResultEventArgs : System.EventArgs
    {
        public ResultEventArgs(IEnumerable<WeightedRandomEntry> lootResult)
        {
            LootResult = lootResult;
        }
        public IEnumerable<WeightedRandomEntry> LootResult { get; }
    }
}