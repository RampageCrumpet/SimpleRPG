using System.Collections.Generic;
using System.Linq;
using SimpleRPG.LevelGeneration;
using NUnit.Framework;
using SimpleRPG.EditorTools;
using UnityEngine;

namespace SimpleRPG.Tests.EditMode
{
    /// <summary>
    /// Checks the project-wide roomset gate that runs before every build.
    /// </summary>
    public class RoomsetBuildGateTests
    {
        private readonly List<Object> created = new List<Object>();

        [TearDown]
        public void TearDown()
        {
            foreach (Object asset in created)
            {
                Object.DestroyImmediate(asset);
            }
            created.Clear();
        }

        [Test]
        public void EveryShippedRoomset_PassesTheBuildGate()
        {
            List<Roomset> roomsets = RoomsetTools.FindShippedRoomsets();

            List<string> problems = RoomsetTools.ValidateForBuild(roomsets);

            Assert.That(problems, Is.Empty, $"{problems.Count} problem(s), first few:\n" + string.Join("\n", problems.Take(5)));
        }

        [Test]
        public void TheDungeonTheGameUses_IsCoveredByTheBuildGate()
        {
            // If this fails, no enabled build scene reaches the roomset, so the gate would check nothing that matters.
            Assert.That(RoomsetTools.FindShippedRoomsets().Select(roomset => roomset.name), Does.Contain("ModularDungeon"));
        }

        [Test]
        public void RoomsetWithoutRooms_FailsTheBuildGate()
        {
            Roomset empty = ScriptableObject.CreateInstance<Roomset>();
            empty.name = "EmptyRoomset";
            empty.cellSize = 5f;
            empty.RoomCollection = new List<RoomBlueprint>();
            created.Add(empty);

            List<string> problems = RoomsetTools.ValidateForBuild(new[] { empty });

            Assert.That(problems, Has.Count.EqualTo(1));
            StringAssert.Contains("EmptyRoomset", problems.Single());
            StringAssert.Contains(RoomValidator.RuleRoomsetEmpty, problems.Single());
        }

        [Test]
        public void RoomsetPrefabLookup_FindsTheRoomsetOfARoom()
        {
            Roomset modular = RoomsetTools.FindRoomsets().First(roomset => roomset.name == "ModularDungeon");
            RoomBlueprint room = modular.RoomCollection.First(r => r != null);

            Assert.AreSame(modular, RoomsetTools.FindRoomsetContaining(room));
        }
    }
}
