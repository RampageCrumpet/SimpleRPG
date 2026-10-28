using System.Collections.Generic;
using System.Linq;
using SimpleRPG.LevelGeneration;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace SimpleRPG.Tests.EditMode
{
    /// <summary>
    /// Generates dungeons and checks every layout with <see cref="LayoutValidator"/>, both from hand-built rooms and from the real roomset.
    /// Layouts are pure data, so hundreds of seeds run in moments; one test then builds a layout in the scene to check the spawner.
    /// </summary>
    public class LayoutGeneratorTests
    {
        private const string ModularDungeonPath = "Assets/Prefabs/World/TileSets/ModularDungeon/ModularDungeon.asset";
        private static readonly int[] RoomCounts = { 8, 20 };
        private static readonly int[] FloorCounts = { 1, 3 };
        private const int SeedsPerConfiguration = 300;

        private readonly List<GameObject> spawned = new List<GameObject>();

        [TearDown]
        public void TearDown()
        {
            foreach (GameObject spawnedObject in spawned)
            {
                if (spawnedObject != null)
                {
                    Object.DestroyImmediate(spawnedObject);
                }
            }
            spawned.Clear();
        }

        [Test]
        public void HandBuiltRooms_AlwaysGiveValidLayouts([ValueSource(nameof(RoomCounts))] int roomCount)
        {
            AssertSweepIsValid(TestRooms.MixedSet(), roomCount);
        }

        [Test]
        public void ModularDungeon_AlwaysGivesValidLayouts([ValueSource(nameof(RoomCounts))] int roomCount, [ValueSource(nameof(FloorCounts))] int floorCount)
        {
            AssertSweepIsValid(RoomDefinitionBaker.Bake(LoadModularDungeon()), roomCount, floorCount);
        }

        [Test]
        public void SameSeed_GivesTheSameLayout([NUnit.Framework.Range(0, 9)] int seed)
        {
            List<RoomDefinition> definitions = RoomDefinitionBaker.Bake(LoadModularDungeon());

            List<string> first = Describe(new LayoutGenerator(seed, definitions).Generate(12).Layout);
            List<string> second = Describe(new LayoutGenerator(seed, definitions).Generate(12).Layout);

            Assert.That(second, Is.EqualTo(first));
        }

        [Test]
        public void GeneratorThatCannotFinish_RetriesDeterministicallyThenReportsFailure()
        {
            // A single one-door room can never grow past one room, so every attempt runs out of doors.
            var definitions = new List<RoomDefinition> { TestRooms.DeadEnd() };

            GenerationResult first = new LayoutGenerator(7, definitions) { MaxAttempts = 3 }.Generate(3);
            GenerationResult second = new LayoutGenerator(7, definitions) { MaxAttempts = 3 }.Generate(3);

            Assert.IsFalse(first.Success);
            Assert.That(first.RejectedAttempts, Has.Count.EqualTo(3), "Every attempt should have been rejected and reported.");
            Assert.That(first.Layout.SealedDoors, Has.Count.EqualTo(1), "The only door should have been sealed.");
            Assert.That(second.RejectedAttempts, Is.EqualTo(first.RejectedAttempts));
            Assert.That(Describe(second.Layout), Is.EqualTo(Describe(first.Layout)));
        }

        [Test]
        public void LayoutThatNeedsFewerRoomsThanAvailable_StillValid()
        {
            GenerationResult result = new LayoutGenerator(1, TestRooms.MixedSet()).Generate(1);

            Assert.IsTrue(result.Success, result.ToString());
            Assert.That(LayoutValidator.Validate(result.Layout), Is.Empty);
        }

        [Test]
        public void MaximumNumberOfRooms_IsNeverExceeded_AndLeftoverDoorsAreSealed()
        {
            List<RoomDefinition> definitions = TestRooms.MixedSet();

            for (int seed = 0; seed < 100; seed++)
            {
                GenerationResult result = new LayoutGenerator(seed, definitions).Generate(4, 6);

                Assert.IsTrue(result.Success, result.ToString());
                Assert.That(result.Layout.Rooms.Count, Is.InRange(4, 6), $"Seed {seed}");
                Assert.That(LayoutValidator.Validate(result.Layout), Is.Empty, $"Seed {seed}");
            }
        }

        [Test]
        public void MaximumEqualToMinimum_GivesAnExactRoomCount([ValueSource(nameof(RoomCounts))] int roomCount, [ValueSource(nameof(FloorCounts))] int floorCount)
        {
            List<RoomDefinition> definitions = RoomDefinitionBaker.Bake(LoadModularDungeon());
            var failures = new List<string>();

            for (int seed = 0; seed < 100; seed++)
            {
                GenerationResult result = new LayoutGenerator(seed, definitions).Generate(roomCount, roomCount, floorCount);

                if (!result.Success || result.Layout.Rooms.Count != roomCount || LayoutValidator.Validate(result.Layout).Count > 0)
                {
                    failures.Add($"Seed {seed}: {result.Layout.Rooms.Count} rooms, success {result.Success}");
                }
            }

            Assert.That(failures, Is.Empty, string.Join("\n", failures.Take(20)));
        }

        [Test]
        public void GeneratorWithoutRooms_IsRejected()
        {
            Assert.Throws<System.ArgumentException>(() => new LayoutGenerator(1, new List<RoomDefinition>()));
        }

        [Test]
        public void SpawnedLevel_MatchesTheLayoutInTheScene()
        {
            Roomset roomset = LoadModularDungeon();
            GenerationResult result = new LayoutGenerator(3, RoomDefinitionBaker.Bake(roomset)).Generate(8);
            Layout layout = result.Layout;

            var parent = new GameObject("DungeonParent");
            spawned.Add(parent);
            var instantiated = new List<GameObject>();
            var spawner = new LevelSpawner(roomset.RoomCollection, roomset.cellSize) { RoomInstantiated = instantiated.Add };

            IReadOnlyList<RoomBlueprint> instances = spawner.Spawn(layout, parent.transform);
            spawned.AddRange(instantiated);

            Assert.That(instances, Has.Count.EqualTo(layout.Rooms.Count));
            Assert.That(instantiated.All(room => room.transform.parent == parent.transform), "Every room should be parented to the transform passed in.");

            // Sealed doors have their sealing object switched on; every other door's stays off.
            var sealedDoors = new HashSet<PlacedDoor>(layout.SealedDoors);
            foreach (PlacedRoom room in layout.Rooms)
            {
                List<ConnectionBlueprint> connections = instances[room.Id].Connections.ToList();
                foreach (PlacedDoor door in room.Doors)
                {
                    Assert.AreEqual(sealedDoors.Contains(door), connections[door.Index].SealingObject.activeSelf, $"sealing state of {door}");
                }
            }

            // Doors the layout joins must meet in the world: one door's far end is where the other stands, and they face each other.
            float cellSize = roomset.cellSize;
            List<PlacedDoor> unsealed = layout.Rooms.SelectMany(room => room.Doors).Where(door => !sealedDoors.Contains(door)).ToList();
            foreach (PlacedDoor door in unsealed)
            {
                PlacedDoor mate = unsealed.Single(other => door.Mates(other));
                ConnectionBlueprint doorObject = instances[door.Room.Id].Connections.ElementAt(door.Index);
                ConnectionBlueprint mateObject = instances[mate.Room.Id].Connections.ElementAt(mate.Index);

                Vector3 farEnd = doorObject.transform.position + doorObject.transform.forward * door.LengthCells * cellSize;
                Assert.Less(Vector3.Distance(farEnd, mateObject.transform.position), cellSize * 0.6f, $"{door} should reach {mate} in the world");
                Assert.Less(Vector3.Dot(doorObject.transform.forward, mateObject.transform.forward), -0.99f, $"{door} and {mate} should face each other");
            }
        }

        private static void AssertSweepIsValid(IReadOnlyList<RoomDefinition> definitions, int roomCount, int floorCount = 1)
        {
            var failures = new List<string>();

            for (int seed = 0; seed < SeedsPerConfiguration; seed++)
            {
                GenerationResult result = new LayoutGenerator(seed, definitions).Generate(roomCount, 0, floorCount);

                if (!result.Success)
                {
                    failures.Add($"Seed {seed}: {result}");
                    continue;
                }

                if (result.Layout.Rooms.Count < roomCount)
                {
                    failures.Add($"Seed {seed}: placed {result.Layout.Rooms.Count} of {roomCount} rooms.");
                }

                if (result.Layout.Rooms.Count > LayoutGenerator.DefaultMaximumRooms(roomCount))
                {
                    failures.Add($"Seed {seed}: placed {result.Layout.Rooms.Count} rooms for a minimum of {roomCount}.");
                }

                foreach (LayoutViolation violation in LayoutValidator.Validate(result.Layout))
                {
                    failures.Add($"Seed {seed}: {violation}");
                }
            }

            Assert.That(failures, Is.Empty, string.Join("\n", failures.Take(20)));
        }

        private static List<string> Describe(Layout layout)
        {
            return layout.Rooms
                .Select(room => room.ToString())
                .Concat(layout.SealedDoors.Select(door => $"sealed {door}"))
                .ToList();
        }

        private static Roomset LoadModularDungeon()
        {
            Roomset roomset = AssetDatabase.LoadAssetAtPath<Roomset>(ModularDungeonPath);
            Assert.IsNotNull(roomset, $"Could not load {ModularDungeonPath}.");
            return roomset;
        }
    }
}
