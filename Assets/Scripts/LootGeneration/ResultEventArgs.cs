using System.Collections.Generic;

namespace SimpleRPG.InventorySystem.LootGeneration
{
    public class ResultEventArgs : System.EventArgs
    {
        public ResultEventArgs(IEnumerable<LootableObject> lootResult)
        {
            LootResult = lootResult;
        }
        public IEnumerable<LootableObject> LootResult { get; }
    }
}