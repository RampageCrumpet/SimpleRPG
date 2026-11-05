using NUnit.Framework;
using SimpleRPG.InventorySystem;

namespace SimpleRPG.Tests.EditMode
{
    /// <summary>
    /// How strong an area's loot can be: ordinary loot at the base level, better or worse sources by an offset, nothing past the ceiling.
    /// </summary>
    public class LootAreaTests
    {
        [Test]
        public void OrdinaryLoot_IsAtTheAreasBaseLevel()
        {
            var area = new LootArea(5, 12);

            Assert.AreEqual(5, area.LevelFor(0));
        }

        [Test]
        public void ABetterSource_IsHigher_ButNeverPastTheCeiling()
        {
            var area = new LootArea(5, 12);

            Assert.AreEqual(8, area.LevelFor(3));
            Assert.AreEqual(12, area.LevelFor(7));
            Assert.AreEqual(12, area.LevelFor(100), "The ceiling is the upper boundary of the area.");
        }

        [Test]
        public void AWorseSource_IsLower_ButNeverBelowTheMinimum()
        {
            var area = new LootArea(5, 12);

            Assert.AreEqual(3, area.LevelFor(-2));
            Assert.AreEqual(ItemLevel.Minimum, area.LevelFor(-50));
        }

        [Test]
        public void AnAreaWithACeilingBelowItsBase_IsMadeConsistent()
        {
            var area = new LootArea(10, 4);

            Assert.AreEqual(10, area.BaseItemLevel);
            Assert.AreEqual(10, area.CeilingItemLevel);
            Assert.AreEqual(10, area.LevelFor(5));
        }

        [Test]
        public void NoAreaAtAll_IsTheLowestLevel_SoOnlyFirstTierAffixesRoll()
        {
            Assert.AreEqual(ItemLevel.Minimum, LootArea.Lowest.LevelFor(0));
            Assert.AreEqual(ItemLevel.Minimum, LootArea.Lowest.LevelFor(9), "Even a good source cannot pass the lowest area's ceiling.");
            Assert.AreEqual(ItemLevel.Minimum, default(LootArea).LevelFor(0), "An unset area behaves as the lowest.");
        }
    }
}
