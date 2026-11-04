namespace SimpleRPG.InventorySystem
{
    /// <summary>
    /// An item's level is how strong the loot source that made it was. It is decided where an item enters the world (not by the item) and passed
    /// to the roller, which only offers affixes whose tier the level allows. It is not stored on the item: what the item rolled is.
    /// </summary>
    public static class ItemLevel
    {
        /// <summary>The lowest level; only the first tier of an affix family can roll at it.</summary>
        public const int Minimum = 1;
    }
}
