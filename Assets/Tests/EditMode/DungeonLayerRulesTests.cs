using NUnit.Framework;
using SimpleRPG.Editor;

namespace SimpleRPG.Tests.EditMode
{
    /// <summary>
    /// Which dungeon pieces are walls a blade bounces off, which are ground, and which are left alone.
    /// </summary>
    public class DungeonLayerRulesTests
    {
        [TestCase("Wall")]
        [TestCase("Wall_Corner_Interior")]
        [TestCase("half_wall")]
        [TestCase("Pillar_Beveled")]
        [TestCase("Metal_Bar_Door")]
        [TestCase("Metal_Bar_Wall_001")]
        [TestCase("Arch_Door_Portal")]
        [TestCase("Square_Door_Portal")]
        [TestCase("Door_Surround")]
        [TestCase("Railing_Segment")]
        [TestCase("Arched_Ceiling_T")]
        [TestCase("Flat_Ceiling_High")]
        [TestCase("ceiling_corner")]
        public void SolidStructure_GoesOnTheWorldLayer(string prefab)
        {
            Assert.AreEqual(DungeonLayerRules.World, DungeonLayerRules.LayerFor(prefab));
        }

        [TestCase("Floor_Stone")]
        [TestCase("Floor_Dirt")]
        [TestCase("Floor")]
        [TestCase("Stairs")]
        public void SurfacesToStandOn_GoOnTheWalkableLayer(string prefab)
        {
            Assert.AreEqual(DungeonLayerRules.Walkable, DungeonLayerRules.LayerFor(prefab));
        }

        [TestCase("Baseboard")]
        [TestCase("Baseboard_Plug")]
        [TestCase("Torch_Lit")]
        [TestCase("Torch")]
        [TestCase("")]
        [TestCase(null)]
        public void EverythingElse_IsLeftAlone(string prefab)
        {
            Assert.IsNull(DungeonLayerRules.LayerFor(prefab));
        }

        [Test]
        public void TheRuleIgnoresCase()
        {
            Assert.AreEqual(DungeonLayerRules.World, DungeonLayerRules.LayerFor("WALL"));
            Assert.AreEqual(DungeonLayerRules.Walkable, DungeonLayerRules.LayerFor("FLOOR_STONE"));
        }
    }
}
