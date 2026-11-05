using UnityEngine;

namespace SimpleRPG.InventorySystem
{
    /// <summary>
    /// How strong the loot of one area of the dungeon can be: the level its ordinary sources start at and the ceiling no source there can pass.
    /// The numbers are the designer's for each area, set where the area is configured; they are not the floor's number. A particular source
    /// (a boss, a plain chest) can be better or worse than the area's ordinary loot by an offset, but never past the ceiling. The level only decides
    /// which affix tiers an item may roll; it promises no rarity.
    /// </summary>
    [System.Serializable]
    public struct LootArea
    {
        [SerializeField, Min(ItemLevel.Minimum)] private int baseItemLevel;
        [SerializeField, Min(ItemLevel.Minimum)] private int ceilingItemLevel;

        public LootArea(int baseItemLevel, int ceilingItemLevel)
        {
            this.baseItemLevel = Mathf.Max(ItemLevel.Minimum, baseItemLevel);
            this.ceilingItemLevel = Mathf.Max(this.baseItemLevel, ceilingItemLevel);
        }

        /// <summary>The level of the area's ordinary loot.</summary>
        public int BaseItemLevel => Mathf.Max(ItemLevel.Minimum, baseItemLevel);

        /// <summary>The highest level any source in the area can give.</summary>
        public int CeilingItemLevel => Mathf.Max(BaseItemLevel, ceilingItemLevel);

        /// <summary>The area for loot with no area at all: the lowest level.</summary>
        public static LootArea Lowest => new LootArea(ItemLevel.Minimum, ItemLevel.Minimum);

        /// <summary>The level of the loot of a source that is <paramref name="sourceOffset"/> better (or worse, if negative) than the area's ordinary loot.</summary>
        public int LevelFor(int sourceOffset) => Mathf.Clamp(BaseItemLevel + sourceOffset, ItemLevel.Minimum, CeilingItemLevel);
    }
}
