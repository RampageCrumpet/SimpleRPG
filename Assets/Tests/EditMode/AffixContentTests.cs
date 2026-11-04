using NUnit.Framework;
using SimpleRPG.Abilities;
using SimpleRPG.InventorySystem;
using System.Linq;
using UnityEditor;

namespace SimpleRPG.Tests.EditMode
{
    /// <summary>
    /// The shipped affix content: every affix is authored correctly and registered, and no pool a player can roll from holds an affix that does
    /// nothing or could outgrow the room an item has.
    /// </summary>
    public class AffixContentTests
    {
        [Test]
        public void EveryAffixPool_IsFitToShip()
        {
            var problems = AssetDatabase.FindAssets("t:AffixPool")
                .Select(g => AssetDatabase.LoadAssetAtPath<AffixPool>(AssetDatabase.GUIDToAssetPath(g)))
                .SelectMany(pool => pool.Problems())
                .ToList();

            Assert.IsEmpty(problems, string.Join("\n", problems));
        }

        [Test]
        public void EveryAffixDefinition_IsAuthoredCorrectly_AndRegistered()
        {
            foreach (string guid in AssetDatabase.FindAssets("t:AffixDefinition"))
            {
                var affix = AssetDatabase.LoadAssetAtPath<AffixDefinition>(AssetDatabase.GUIDToAssetPath(guid));
                Assert.IsNull(affix.Problem(), affix.name);
                Assert.IsTrue(DefinitionRegistry.Instance != null && DefinitionRegistry.Instance.TryGetShortId(affix.Id, out _), affix.name + " is not in the DefinitionRegistry; rebuild it (SimpleRPG > Definitions).");
            }
        }
    }
}
