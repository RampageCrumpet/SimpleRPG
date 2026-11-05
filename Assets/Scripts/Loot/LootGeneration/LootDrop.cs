namespace SimpleRPG.InventorySystem.LootGeneration
{
    /// <summary>
    /// One thing a loot table produced and how much better (or worse) its source is than the area's ordinary loot: the sum of the offsets of the
    /// entries it came through, so a boss table that is +4 makes everything it drops +4.
    /// </summary>
    public readonly struct LootDrop
    {
        public LootDrop(WeightedRandomEntry entry, int levelOffset)
        {
            Entry = entry;
            LevelOffset = levelOffset;
        }

        public WeightedRandomEntry Entry { get; }

        public int LevelOffset { get; }
    }
}
