using System.Collections.Generic;
using System.Linq;
using LevelGeneration;
using NUnit.Framework;
using UnityEditor;

namespace SimpleRPG.Tests.EditMode
{
    /// <summary>
    /// The loop and branching dials: they must change the shape of the dungeon in the right direction without ever breaking validity.
    /// </summary>
    public class LayoutStyleTests
    {
        private const string ModularDungeonPath = "Assets/Prefabs/World/TileSets/ModularDungeon/ModularDungeon.asset";
        private const int Seeds = 30;
        private const int RoomCount = 20;

        private static List<RoomDefinition> RealRooms() =>
            RoomDefinitionBaker.Bake(AssetDatabase.LoadAssetAtPath<Roomset>(ModularDungeonPath));

        [Test]
        public void LoopRatioZero_GivesTreesWithNoLoops()
        {
            var failures = new List<string>();

            foreach (List<RoomDefinition> definitions in new[] { RealRooms(), TestRooms.MixedSet() })
            {
                for (int seed = 0; seed < Seeds; seed++)
                {
                    var generator = new LayoutGenerator(seed, definitions) { Style = new LayoutStyle { LoopRatio = 0f } };
                    GenerationResult result = generator.Generate(RoomCount);
                    int loops = Loops(result.Layout);

                    if (!result.Success || loops != 0)
                    {
                        failures.Add($"Seed {seed}: {loops} loops, success {result.Success}. {result}");
                    }
                }
            }

            Assert.That(failures, Is.Empty, string.Join("\n", failures.Take(20)));
        }

        [Test]
        public void HigherLoopRatio_GivesMoreLoops_AndStaysValid()
        {
            List<RoomDefinition> definitions = TestRooms.MixedSet();
            var failures = new List<string>();
            double zero = AverageLoops(definitions, 0f, failures);
            double natural = AverageLoops(definitions, LayoutStyle.Natural, failures);
            double full = AverageLoops(definitions, 1f, failures);

            Assert.That(failures, Is.Empty, string.Join("\n", failures.Take(20)));
            Assert.That(zero, Is.EqualTo(0d));
            Assert.That(natural, Is.GreaterThanOrEqualTo(zero));
            Assert.That(full, Is.GreaterThan(natural), $"Average loops: 0 -> {zero}, 0.5 -> {natural}, 1 -> {full}.");
        }

        [Test]
        public void HigherLoopRatio_GivesMoreLoops_OnTheRealRoomset()
        {
            List<RoomDefinition> definitions = RealRooms();
            var failures = new List<string>();
            double natural = AverageLoops(definitions, LayoutStyle.Natural, failures);
            double full = AverageLoops(definitions, 1f, failures);

            Assert.That(failures, Is.Empty, string.Join("\n", failures.Take(20)));
            Assert.That(full, Is.GreaterThan(natural), $"Average loops: 0.5 -> {natural}, 1 -> {full}.");
        }

        [Test]
        public void MoreBranching_GivesMoreJunctions()
        {
            List<RoomDefinition> definitions = RealRooms();
            double winding = AverageJunctionShare(definitions, 0f);
            double bushy = AverageJunctionShare(definitions, 1f);

            Assert.That(bushy, Is.GreaterThan(winding), $"Junction share: 0 -> {winding}, 1 -> {bushy}.");
        }

        [Test]
        public void NaturalStyle_GivesTheSameLayoutAsNoStyle([NUnit.Framework.Range(0, 9)] int seed)
        {
            List<RoomDefinition> definitions = RealRooms();

            Layout plain = new LayoutGenerator(seed, definitions).Generate(12).Layout;
            Layout natural = new LayoutGenerator(seed, definitions) { Style = new LayoutStyle() }.Generate(12).Layout;

            Assert.That(natural.Rooms.Select(room => room.ToString()), Is.EqualTo(plain.Rooms.Select(room => room.ToString())));
        }

        [Test]
        public void Style_ClampsItsDials()
        {
            var style = new LayoutStyle { LoopRatio = 3f, Branching = -2f };

            Assert.That(style.LoopRatio, Is.EqualTo(1f));
            Assert.That(style.Branching, Is.EqualTo(0f));
        }

        private static double AverageLoops(List<RoomDefinition> definitions, float loopRatio, List<string> failures)
        {
            int total = 0;

            for (int seed = 0; seed < Seeds; seed++)
            {
                var generator = new LayoutGenerator(seed, definitions) { Style = new LayoutStyle { LoopRatio = loopRatio } };
                GenerationResult result = generator.Generate(RoomCount);

                if (!result.Success)
                {
                    failures.Add($"Ratio {loopRatio} seed {seed}: {result}");
                    continue;
                }

                total += Loops(result.Layout);
            }

            return (double)total / Seeds;
        }

        private static double AverageJunctionShare(List<RoomDefinition> definitions, float branching)
        {
            double total = 0;

            for (int seed = 0; seed < Seeds; seed++)
            {
                var generator = new LayoutGenerator(seed, definitions) { Style = new LayoutStyle { Branching = branching } };
                Layout layout = generator.Generate(RoomCount).Layout;
                total += (double)layout.Rooms.Count(room => room.Definition.Doors.Count >= 3) / layout.Rooms.Count;
            }

            return total / Seeds;
        }

        /// <summary>
        /// The number of independent circuits in the dungeon: joined doors, less the rooms needed to span it.
        /// </summary>
        private static int Loops(Layout layout)
        {
            var sealedDoors = new HashSet<PlacedDoor>(layout.SealedDoors);
            List<PlacedDoor> doors = layout.Rooms.SelectMany(room => room.Doors).Where(door => !sealedDoors.Contains(door)).ToList();
            int joins = doors.Count(door => doors.Any(other => other != door && door.Mates(other))) / 2;

            return joins - (layout.Rooms.Count - 1);
        }
    }
}
