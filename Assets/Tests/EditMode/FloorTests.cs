using System.Collections.Generic;
using System.Linq;
using SimpleRPG.LevelGeneration;
using NUnit.Framework;
using UnityEngine;

namespace SimpleRPG.Tests.EditMode
{
    /// <summary>
    /// Multi-floor dungeons: rooms with a height, doors on a floor of their room, vertical doors that join across floors,
    /// occupancy and passages in three dimensions, and the rules the validator and roomset checks add for them.
    /// </summary>
    public class FloorTests
    {
        private static readonly int Ladder = ConnectionType.IdFor("Ladder");
        private const int SeedCount = 300;

        private readonly List<Object> created = new List<Object>();

        [TearDown]
        public void TearDown()
        {
            foreach (Object item in created)
            {
                Object.DestroyImmediate(item);
            }
            created.Clear();
        }

        // ---- Occupancy and mating ----

        [Test]
        public void TallRoom_OccupiesEveryFloorItSpans()
        {
            PlacedRoom tall = new PlacedRoom(0, Stairwell(0), Vector2Int.zero, 0, 2);

            Assert.AreEqual(2, tall.Floor);
            Assert.AreEqual(4, tall.TopFloor);
            Assert.AreEqual(72, tall.Cells3D().Count());
            Assert.IsTrue(tall.Contains(new Vector3Int(1, 1, 3)));
            Assert.IsFalse(tall.Contains(new Vector3Int(1, 1, 4)));
            Assert.IsFalse(tall.Contains(new Vector3Int(1, 1, 1)));
        }

        [Test]
        public void DoorsStandOnTheFloorOfTheirLevel()
        {
            PlacedRoom tall = new PlacedRoom(0, Stairwell(0), Vector2Int.zero, 0, 2);

            Assert.AreEqual(2, tall.Doors[0].Floor, "The west door is on the room's lowest floor.");
            Assert.AreEqual(3, tall.Doors[1].Floor, "The east door is a floor higher.");
        }

        [Test]
        public void LevelDoors_OnlyMateOnTheSameFloor()
        {
            PlacedRoom west = new PlacedRoom(0, Plain("West", East(0)), Vector2Int.zero, 0, 0);
            PlacedRoom sameFloor = new PlacedRoom(1, Plain("Same", West(0)), new Vector2Int(6, 0), 0, 0);
            PlacedRoom otherFloor = new PlacedRoom(2, Plain("Other", West(0)), new Vector2Int(6, 0), 0, 1);

            Assert.IsTrue(west.Doors[0].Mates(sameFloor.Doors[0]));
            Assert.IsFalse(west.Doors[0].Mates(otherFloor.Doors[0]));
        }

        [Test]
        public void VerticalDoors_MateOnlyWithTheOppositeClimbOnTheRightFloor()
        {
            PlacedRoom bottom = new PlacedRoom(0, Plain("Bottom", LadderEast(0, 1)), Vector2Int.zero, 0, 0);
            PlacedRoom top = new PlacedRoom(1, Plain("Top", LadderWest(0, -1)), new Vector2Int(6, 0), 0, 1);
            PlacedRoom wrongFloor = new PlacedRoom(2, Plain("Wrong", LadderWest(0, -1)), new Vector2Int(6, 0), 0, 2);
            PlacedRoom sameClimb = new PlacedRoom(3, Plain("Same", LadderWest(0, 1)), new Vector2Int(6, 0), 0, 1);

            Assert.IsTrue(bottom.Doors[0].Mates(top.Doors[0]));
            Assert.IsFalse(bottom.Doors[0].Mates(wrongFloor.Doors[0]));
            Assert.IsFalse(bottom.Doors[0].Mates(sameClimb.Doors[0]));
        }

        [Test]
        public void PassageOfAVerticalDoor_CoversEveryFloorBetween()
        {
            PlacedRoom bottom = new PlacedRoom(0, Plain("Bottom", new DoorDefinition(0, new Vector2Int(4, 3), Side.East, 4, 2, Ladder, 0, 3)),
                Vector2Int.zero, 0, 1);

            Assert.That(bottom.Doors[0].PassageFloors(), Is.EqualTo(new[] { 1, 2, 3, 4 }));
        }

        // ---- Validator ----

        [Test]
        public void RoomsOnDifferentFloors_MayShareCells_ButNotOnTheSameFloor()
        {
            PlacedRoom lower = new PlacedRoom(0, Plain("Lower"), Vector2Int.zero, 0, 0);
            PlacedRoom upper = new PlacedRoom(1, Plain("Upper"), Vector2Int.zero, 0, 1);
            PlacedRoom clash = new PlacedRoom(2, Plain("Clash"), Vector2Int.zero, 0, 1);

            Assert.That(LayoutValidator.Validate(Sealed(2, lower, upper)).Select(v => v.Rule), Does.Not.Contain(LayoutValidator.RuleRoomOverlap));
            AssertRule(LayoutValidator.Validate(Sealed(2, lower, upper, clash)), LayoutValidator.RuleRoomOverlap);
        }

        [Test]
        public void TallRoom_ClashesWithARoomOnAnyFloorItSpans()
        {
            PlacedRoom tall = new PlacedRoom(0, Stairwell(0), Vector2Int.zero, 0, 0);
            PlacedRoom upper = new PlacedRoom(1, Plain("Upper"), Vector2Int.zero, 0, 1);

            AssertRule(LayoutValidator.Validate(Sealed(2, tall, upper)), LayoutValidator.RuleRoomOverlap);
        }

        [Test]
        public void RoomOutsideTheDungeonsFloors_IsReported()
        {
            PlacedRoom high = new PlacedRoom(0, Plain("High"), Vector2Int.zero, 0, 2);
            PlacedRoom low = new PlacedRoom(1, Plain("Low"), new Vector2Int(20, 0), 0, -1);

            AssertRule(LayoutValidator.Validate(Sealed(2, high)), LayoutValidator.RuleRoomOutsideFloors);
            AssertRule(LayoutValidator.Validate(Sealed(3, new PlacedRoom(0, Plain("Low"), Vector2Int.zero, 0, -1))), LayoutValidator.RuleRoomOutsideFloors);
            Assert.That(LayoutValidator.Validate(Sealed(3, high)).Select(v => v.Rule), Does.Not.Contain(LayoutValidator.RuleRoomOutsideFloors));
            Assert.IsNotNull(low);
        }

        [Test]
        public void VerticalJoin_ThatLinesUp_IsValid_AndOnTheWrongFloor_IsReported()
        {
            PlacedRoom bottom = new PlacedRoom(0, Plain("Bottom", LadderEast(0, 1)), Vector2Int.zero, 0, 0);
            PlacedRoom top = new PlacedRoom(1, Plain("Top", LadderWest(0, -1)), new Vector2Int(6, 0), 0, 1);
            PlacedRoom tooHigh = new PlacedRoom(2, Plain("TooHigh", LadderWest(0, -1)), new Vector2Int(6, 0), 0, 2);

            Assert.That(LayoutValidator.Validate(new Layout(0, 0, new[] { bottom, top }, new PlacedDoor[0], 2)), Is.Empty);
            AssertRule(LayoutValidator.Validate(new Layout(0, 0, new[] { bottom, tooHigh }, new PlacedDoor[0], 3)), LayoutValidator.RuleVerticalMismatch);
        }

        [Test]
        public void RoomBetweenTwoVerticallyJoinedDoors_BlocksThePassage()
        {
            PlacedRoom bottom = new PlacedRoom(0, Plain("Bottom", LadderEast(0, 1, 8)), Vector2Int.zero, 0, 0);
            PlacedRoom top = new PlacedRoom(1, Plain("Top", LadderWest(0, -1, 8)), new Vector2Int(10, 0), 0, 1);
            PlacedRoom blocker = new PlacedRoom(2, Plain("Blocker"), new Vector2Int(4, 0), 0, 1);

            Assert.That(LayoutValidator.Validate(new Layout(0, 0, new[] { bottom, top }, new PlacedDoor[0], 2)), Is.Empty);
            AssertRule(LayoutValidator.Validate(new Layout(0, 0, new[] { bottom, top, blocker }, new PlacedDoor[0], 2)), LayoutValidator.RulePassageBlocked);
        }

        [Test]
        public void VerticalDoorWithAStandardType_IsReported()
        {
            PlacedRoom odd = new PlacedRoom(0, Plain("Odd", new DoorDefinition(0, new Vector2Int(4, 3), Side.East, 4, 2, ConnectionType.StandardId, 0, 1)),
                Vector2Int.zero, 0, 0);

            AssertRule(LayoutValidator.Validate(Sealed(2, odd)), LayoutValidator.RuleVerticalDoorInvalid);
        }

        [Test]
        public void DoorOutsideItsRoomsFloors_IsReported()
        {
            PlacedRoom odd = new PlacedRoom(0, Plain("Odd", new DoorDefinition(0, new Vector2Int(4, 3), Side.East, 4, 2, ConnectionType.StandardId, 1, 0)),
                Vector2Int.zero, 0, 0);

            AssertRule(LayoutValidator.Validate(Sealed(2, odd)), LayoutValidator.RuleVerticalDoorInvalid);
        }

        // ---- Generation ----

        [Test]
        public void FlatGeneration_NeverUsesTallRooms()
        {
            List<RoomDefinition> set = MultiFloorSet();

            for (int seed = 0; seed < 100; seed++)
            {
                GenerationResult result = new LayoutGenerator(seed, set).Generate(12);
                Assert.IsTrue(result.Success, $"Seed {seed}: {result}");
                Assert.That(result.Layout.Rooms.Select(room => room.Floor), Has.All.EqualTo(0), $"Seed {seed}");
                Assert.That(result.Layout.Rooms.Select(room => room.Definition.Name), Has.None.EqualTo("Stairwell"), $"Seed {seed}");
            }
        }

        [TestCase(2)]
        [TestCase(3)]
        [TestCase(6)]
        public void MultiFloorGeneration_AlwaysGivesValidLayouts_ThatReachSeveralFloors(int floorCount)
        {
            List<RoomDefinition> set = MultiFloorSet();
            var failures = new List<string>();
            int seedsThatClimbed = 0;
            int mostFloorsUsed = 0;

            for (int seed = 0; seed < SeedCount; seed++)
            {
                GenerationResult result = new LayoutGenerator(seed, set).Generate(14, 0, floorCount);
                if (!result.Success)
                {
                    failures.Add($"Seed {seed}: {result}");
                    continue;
                }

                failures.AddRange(LayoutValidator.Validate(result.Layout).Select(v => $"Seed {seed}: {v}"));

                int floorsUsed = result.Layout.Rooms.SelectMany(room => Enumerable.Range(room.Floor, room.Definition.Height)).Distinct().Count();
                if (floorsUsed > 1)
                {
                    seedsThatClimbed++;
                }
                mostFloorsUsed = Mathf.Max(mostFloorsUsed, floorsUsed);

                Assert.AreEqual(0, result.Layout.Rooms[0].Floor, $"Seed {seed}: the dungeon starts on the bottom floor.");
            }

            Assert.That(failures, Is.Empty, string.Join("\n", failures.Take(10)));
            Assert.That(seedsThatClimbed, Is.GreaterThan(SeedCount / 4), "Stairs and ladders must actually be used.");
            Assert.That(mostFloorsUsed, Is.GreaterThanOrEqualTo(Mathf.Min(floorCount, 3)));
        }

        [Test]
        public void MultiFloorGeneration_IsDeterministic_AndTheFloorCountChangesTheFingerprint()
        {
            List<RoomDefinition> set = MultiFloorSet();

            for (int seed = 0; seed < 20; seed++)
            {
                Layout first = new LayoutGenerator(seed, set).Generate(14, 0, 3).Layout;
                Layout again = new LayoutGenerator(seed, set).Generate(14, 0, 3).Layout;
                Layout other = new LayoutGenerator(seed, set).Generate(14, 0, 4).Layout;

                Assert.AreEqual(LayoutFingerprint.OfLayout(first), LayoutFingerprint.OfLayout(again), $"Seed {seed}");
                Assert.AreNotEqual(LayoutFingerprint.OfLayout(first), LayoutFingerprint.OfLayout(other), $"Seed {seed}");
            }
        }

        [Test]
        public void LayoutFingerprint_SeesTheFloorARoomIsOn()
        {
            PlacedRoom low = new PlacedRoom(0, Plain("Room"), Vector2Int.zero, 0, 0);
            PlacedRoom high = new PlacedRoom(0, Plain("Room"), Vector2Int.zero, 0, 1);

            Assert.AreNotEqual(LayoutFingerprint.OfLayout(Sealed(2, low)), LayoutFingerprint.OfLayout(Sealed(2, high)));
        }

        [Test]
        public void RoomsetFingerprint_SeesHeightsLevelsAndClimbs()
        {
            int baseline = LayoutFingerprint.OfRoomset(new List<RoomDefinition> { Plain("A", West(0)) });
            RoomDefinition taller = new RoomDefinition(0, "A", new Vector2Int(6, 6), Vector2Int.zero, new[] { West(0) }, height: 2);
            RoomDefinition raised = new RoomDefinition(0, "A", new Vector2Int(6, 6), Vector2Int.zero,
                new[] { new DoorDefinition(0, new Vector2Int(2, 3), Side.West, 4, 2, ConnectionType.StandardId, 1, 0) }, height: 2);
            RoomDefinition climbing = Plain("A", LadderWest(0, 1));

            Assert.AreNotEqual(baseline, LayoutFingerprint.OfRoomset(new List<RoomDefinition> { taller }));
            Assert.AreNotEqual(LayoutFingerprint.OfRoomset(new List<RoomDefinition> { taller }), LayoutFingerprint.OfRoomset(new List<RoomDefinition> { raised }));
            Assert.AreNotEqual(baseline, LayoutFingerprint.OfRoomset(new List<RoomDefinition> { climbing }));
        }

        [Test]
        public void Selector_OnlyOffersRoomsWithADoorThatClimbsTheOtherWay()
        {
            List<RoomDefinition> set = MultiFloorSet();
            PlacedDoor target = new PlacedRoom(0, Plain("Anchor", LadderEast(0, 1)), Vector2Int.zero, 0, 0).Doors[0];
            var context = new SelectionContext(1, 10, 1, new Dictionary<RoomDefinition, int>(), target);
            var selector = new WeightedRoomSelector();
            var random = new System.Random(4);

            IEnumerable<RoomDefinition> picks = Enumerable.Range(0, 200).Select(_ => selector.Select(random, set, context));

            Assert.That(picks, Has.All.Matches<RoomDefinition>(room => room.Doors.Any(door => door.TypeId == Ladder && door.FloorDelta == -1)));
        }

        // ---- Authoring ----

        [Test]
        public void Baker_CarriesFloorsLevelAndClimbIntoTheDefinition()
        {
            ConnectionType ladderType = NewType("Ladder", 1);
            RoomBlueprint room = NewRoom();
            room.Size = new Vector2Int(6, 6);
            room.Floors = 3;
            NewDoor(room, ladderType, level: 2, climbsUp: true);
            NewDoor(room, ladderType, level: 0, climbsUp: false);
            NewDoor(room, null, level: 1, climbsUp: true);

            RoomDefinition baked = RoomDefinitionBaker.Bake(room, 0, 5f);

            Assert.AreEqual(3, baked.Height);
            Assert.AreEqual((2, 1), (baked.Doors[0].Level, baked.Doors[0].FloorDelta));
            Assert.AreEqual((0, -1), (baked.Doors[1].Level, baked.Doors[1].FloorDelta));
            Assert.AreEqual((1, 0), (baked.Doors[2].Level, baked.Doors[2].FloorDelta), "A standard door never climbs.");
        }

        [Test]
        public void RoomValidator_ReportsADoorLevelBeyondTheRoomsFloors()
        {
            RoomBlueprint room = NewRoom();
            room.Floors = 2;
            NewDoor(room, null, level: 2, climbsUp: true);

            Assert.That(RoomValidator.Validate(room, 5f).Select(e => e.Rule), Does.Contain(RoomValidator.RuleDoorLevel));
        }

        [Test]
        public void Roomset_WithTallRooms_NeedsAFloorHeight()
        {
            RoomBlueprint room = NewRoom();
            room.Floors = 2;
            NewDoor(room, null, level: 1, climbsUp: true);
            Roomset roomset = NewRoomset(room);

            Assert.That(RoomValidator.ValidateRoomset(roomset).Select(e => e.Rule), Does.Contain(RoomValidator.RuleRoomsetFloorHeight));

            roomset.floorHeight = 5f;
            Assert.That(RoomValidator.ValidateRoomset(roomset).Select(e => e.Rule), Does.Not.Contain(RoomValidator.RuleRoomsetFloorHeight));
        }

        [Test]
        public void Roomset_WhereAVerticalTypeOnlyClimbsOneWay_IsReported()
        {
            ConnectionType ladderType = NewType("Ladder", 1);
            RoomBlueprint onlyUp = NewRoom();
            NewDoor(onlyUp, ladderType, level: 0, climbsUp: true);
            Roomset lopsided = NewRoomset(onlyUp);

            RoomBlueprint down = NewRoom();
            NewDoor(down, ladderType, level: 0, climbsUp: false);
            Roomset paired = NewRoomset(onlyUp, down);

            Assert.That(RoomValidator.CheckRoomsetComposition(lopsided).Select(e => e.Rule), Does.Contain(RoomValidator.RuleRoomsetVerticalUnpaired));
            Assert.That(RoomValidator.CheckRoomsetComposition(paired).Select(e => e.Rule), Does.Not.Contain(RoomValidator.RuleRoomsetVerticalUnpaired));
        }

        [Test]
        public void Spawner_StandsRoomsAtTheirFloorsHeight()
        {
            RoomBlueprint prefab = NewRoom();
            prefab.Size = new Vector2Int(6, 6);

            PlacedRoom low = new PlacedRoom(0, Plain("Low", West(0)), Vector2Int.zero, 0, 0);
            PlacedRoom high = new PlacedRoom(1, Plain("High", West(0)), new Vector2Int(10, 0), 0, 2);
            var spawner = new LevelSpawner(new[] { prefab }, 5f, 7f);
            var parent = new GameObject("Parent");
            created.Add(parent);

            IReadOnlyList<RoomBlueprint> instances = spawner.Spawn(new Layout(0, 0, new[] { low, high }, new PlacedDoor[0], 3), parent.transform);

            Assert.AreEqual(0f, instances[0].transform.position.y, 0.001f);
            Assert.AreEqual(14f, instances[1].transform.position.y, 0.001f);
        }

        // ---- Real prefabs ----

        private const string ModularDungeonPath = "Assets/Prefabs/World/TileSets/ModularDungeon/ModularDungeon.asset";

        [Test]
        public void RealRoomset_HasAStairwellSpanningTwoFloors_AndNoProblems()
        {
            Roomset roomset = UnityEditor.AssetDatabase.LoadAssetAtPath<Roomset>(ModularDungeonPath);
            RoomBlueprint stairwell = roomset.RoomCollection.Single(room => room.name == "Room_Stairwell");

            Assert.AreEqual(2, stairwell.Floors);
            Assert.AreEqual(8f, roomset.floorHeight);
            Assert.That(stairwell.Connections.Select(door => door.Level), Is.EquivalentTo(new[] { 0, 1 }));
            Assert.That(RoomValidator.ValidateRoomset(roomset), Is.Empty);
        }

        [Test]
        public void Stairwell_CanBeWalkedFromItsLowerDoorToItsUpperDoor()
        {
            RoomBlueprint prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<Roomset>(ModularDungeonPath).RoomCollection.Single(room => room.name == "Room_Stairwell");
            RoomBlueprint room = (RoomBlueprint)UnityEditor.PrefabUtility.InstantiatePrefab(prefab);
            room.transform.position = new Vector3(0, 0, 1000);
            created.Add(room.gameObject);
            var surfaceObject = new GameObject("StairwellNavMesh");
            created.Add(surfaceObject);
            var surface = surfaceObject.AddComponent<Unity.AI.Navigation.NavMeshSurface>();
            surface.collectObjects = Unity.AI.Navigation.CollectObjects.All;
            surface.useGeometry = UnityEngine.AI.NavMeshCollectGeometry.RenderMeshes;

            try
            {
                surface.BuildNavMesh();
                ConnectionBlueprint lower = room.Connections.Single(door => door.Level == 0);
                ConnectionBlueprint upper = room.Connections.Single(door => door.Level == 1);

                Assert.IsTrue(UnityEngine.AI.NavMesh.SamplePosition(lower.transform.position + lower.transform.forward * 2f, out UnityEngine.AI.NavMeshHit start, 1.2f, -1), "Floor outside the lower door");
                Assert.IsTrue(UnityEngine.AI.NavMesh.SamplePosition(upper.transform.position + upper.transform.forward * 2f, out UnityEngine.AI.NavMeshHit end, 1.2f, -1), "Floor outside the upper door");

                var path = new UnityEngine.AI.NavMeshPath();
                UnityEngine.AI.NavMesh.CalculatePath(start.position, end.position, -1, path);

                Assert.AreEqual(UnityEngine.AI.NavMeshPathStatus.PathComplete, path.status);
                Assert.That(end.position.y - start.position.y, Is.GreaterThan(7.2f), "The stairs should climb a whole floor.");
            }
            finally
            {
                UnityEngine.AI.NavMesh.RemoveAllNavMeshData();
            }
        }

        [TestCase(8)]
        [TestCase(20)]
        public void RealRoomset_MultiFloor_AlwaysGivesValidLayouts_AndUsesTheStairwell(int roomCount)
        {
            List<RoomDefinition> definitions = RoomDefinitionBaker.Bake(UnityEditor.AssetDatabase.LoadAssetAtPath<Roomset>(ModularDungeonPath));
            var failures = new List<string>();
            int seedsWithStairs = 0;

            for (int seed = 0; seed < SeedCount; seed++)
            {
                GenerationResult result = new LayoutGenerator(seed, definitions).Generate(roomCount, 0, 3);
                if (!result.Success)
                {
                    failures.Add($"Seed {seed}: {result}");
                    continue;
                }

                failures.AddRange(LayoutValidator.Validate(result.Layout).Select(v => $"Seed {seed}: {v}"));
                if (result.Layout.Rooms.Any(room => room.Definition.Name == "Room_Stairwell"))
                {
                    seedsWithStairs++;
                }
            }

            Assert.That(failures, Is.Empty, string.Join("\n", failures.Take(10)));
            Assert.That(seedsWithStairs, Is.GreaterThan(SeedCount / 4), "The stairwell must actually be used.");
        }

        [Test]
        public void SpawnedStairwellLevel_JoinsDoorsInThreeDimensions()
        {
            Roomset roomset = UnityEditor.AssetDatabase.LoadAssetAtPath<Roomset>(ModularDungeonPath);
            List<RoomDefinition> definitions = RoomDefinitionBaker.Bake(roomset);
            Layout layout = Enumerable.Range(0, 200)
                .Select(seed => new LayoutGenerator(seed, definitions).Generate(10, 0, 3).Layout)
                .First(candidate => candidate.Rooms.Any(room => room.Definition.Name == "Room_Stairwell"));

            var parent = new GameObject("StairwellDungeon");
            created.Add(parent);
            var spawner = new LevelSpawner(roomset.RoomCollection, roomset.cellSize, roomset.floorHeight);
            IReadOnlyList<RoomBlueprint> instances = spawner.Spawn(layout, parent.transform);

            var sealedDoors = new HashSet<PlacedDoor>(layout.SealedDoors);
            List<PlacedDoor> unsealed = layout.Rooms.SelectMany(room => room.Doors).Where(door => !sealedDoors.Contains(door)).ToList();
            foreach (PlacedDoor door in unsealed)
            {
                PlacedDoor mate = unsealed.Single(other => door.Mates(other));
                ConnectionBlueprint doorObject = instances[door.Room.Id].Connections.ElementAt(door.Index);
                ConnectionBlueprint mateObject = instances[mate.Room.Id].Connections.ElementAt(mate.Index);

                Vector3 farEnd = doorObject.transform.position + doorObject.transform.forward * door.LengthCells * roomset.cellSize;
                Assert.Less(Vector3.Distance(farEnd, mateObject.transform.position), roomset.cellSize * 0.6f, $"{door} should reach {mate} in the world");
            }
        }

        // ---- Helpers ----

        private static DoorDefinition West(int index) => new DoorDefinition(index, new Vector2Int(2, 3), Side.West, 4);
        private static DoorDefinition East(int index) => new DoorDefinition(index, new Vector2Int(4, 3), Side.East, 4);
        private static DoorDefinition North(int index) => new DoorDefinition(index, new Vector2Int(3, 4), Side.North, 4);

        private static DoorDefinition LadderWest(int index, int climb, int length = 4) => new DoorDefinition(index, new Vector2Int(2, 3), Side.West, length, 2, Ladder, 0, climb);
        private static DoorDefinition LadderEast(int index, int climb, int length = 4) => new DoorDefinition(index, new Vector2Int(4, 3), Side.East, length, 2, Ladder, 0, climb);

        private static RoomDefinition Plain(string name, params DoorDefinition[] doors)
        {
            return new RoomDefinition(0, name, new Vector2Int(6, 6), Vector2Int.zero, doors.ToList());
        }

        /// <summary>
        /// Two floors high: the west door is on the lower floor and the east door on the upper, so a corridor walks up through it.
        /// </summary>
        private static RoomDefinition Stairwell(int index)
        {
            var doors = new[]
            {
                West(0),
                new DoorDefinition(1, new Vector2Int(4, 3), Side.East, 4, 2, ConnectionType.StandardId, 1, 0),
            };
            return new RoomDefinition(index, "Stairwell", new Vector2Int(6, 6), Vector2Int.zero, doors, height: 2);
        }

        /// <summary>
        /// Flat rooms, a stairwell that climbs a floor, and ladder rooms that join across floors.
        /// </summary>
        private static List<RoomDefinition> MultiFloorSet()
        {
            RoomDefinition Room(int index, string name, params DoorDefinition[] doors) =>
                new RoomDefinition(index, name, new Vector2Int(6, 6), Vector2Int.zero, doors.ToList());

            return new List<RoomDefinition>
            {
                Room(0, "DeadEnd", West(0)),
                Room(1, "Corridor", West(0), East(1)),
                Room(2, "Corner", West(0), North(1)),
                Room(3, "Crossroads", West(0), East(1), North(2), new DoorDefinition(3, new Vector2Int(3, 2), Side.South, 4)),
                Stairwell(4),
                Room(5, "LadderBase", West(0), LadderEast(1, 1)),
                Room(6, "LadderTop", LadderWest(0, -1), East(1)),
                Room(7, "LadderBaseEnd", LadderEast(0, 1)),
            };
        }

        private static Layout Sealed(int floorCount, params PlacedRoom[] rooms)
        {
            return new Layout(0, 0, rooms, rooms.SelectMany(room => room.Doors).ToList(), floorCount);
        }

        private static void AssertRule(List<LayoutViolation> violations, string rule)
        {
            Assert.That(violations.Select(violation => violation.Rule), Does.Contain(rule), string.Join("\n", violations.Select(violation => violation.ToString())));
        }

        private ConnectionType NewType(string name, int floorDelta)
        {
            ConnectionType type = ScriptableObject.CreateInstance<ConnectionType>();
            type.name = name;
            type.FloorDelta = floorDelta;
            created.Add(type);
            return type;
        }

        private RoomBlueprint NewRoom()
        {
            var root = new GameObject("FloorTestRoom");
            created.Add(root);
            return root.AddComponent<RoomBlueprint>();
        }

        private static void NewDoor(RoomBlueprint room, ConnectionType type, int level, bool climbsUp)
        {
            var door = new GameObject("Door");
            door.transform.SetParent(room.transform, false);
            door.transform.position = new Vector3(20, 0, 15);
            door.transform.rotation = Quaternion.LookRotation(Vector3.right);
            ConnectionBlueprint connection = door.AddComponent<ConnectionBlueprint>();
            connection.ConnectionLength = 20f;
            connection.ConnectionWidth = 1f;
            connection.Type = type;
            connection.Level = level;
            connection.ClimbsUp = climbsUp;
            connection.location = new Vector2Int(4, 3);
            connection.SealingObject = new GameObject("Seal");
            connection.SealingObject.transform.SetParent(door.transform, false);
            connection.SealingObject.SetActive(false);
            room.SetConnections(room.Connections.Append(connection));
        }

        private Roomset NewRoomset(params RoomBlueprint[] rooms)
        {
            Roomset roomset = ScriptableObject.CreateInstance<Roomset>();
            roomset.name = "FloorRoomset";
            roomset.cellSize = 5f;
            roomset.RoomCollection = rooms.ToList();
            created.Add(roomset);
            return roomset;
        }
    }
}
