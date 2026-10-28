using System.Collections.Generic;
using System.Linq;
using SimpleRPG.LevelGeneration;
using NUnit.Framework;
using UnityEngine;

namespace SimpleRPG.Tests.EditMode
{
    /// <summary>
    /// Checks that doors only join doors of their own type: in the generator, the selector, the validator and the roomset rules.
    /// </summary>
    public class ConnectionTypeTests
    {
        private static readonly int Gate = ConnectionType.IdFor("Gate");

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
        public void TypeIds_AreStableDistinctAndNeverStandard()
        {
            Assert.AreEqual(ConnectionType.IdFor("Gate"), ConnectionType.IdFor("Gate"));
            Assert.AreNotEqual(ConnectionType.IdFor("Gate"), ConnectionType.IdFor("Sewer"));
            Assert.AreNotEqual(ConnectionType.StandardId, ConnectionType.IdFor("Gate"));
            Assert.AreEqual(ConnectionType.StandardId, ConnectionType.IdFor((ConnectionType)null));
        }

        [Test]
        public void DoorsOfDifferentTypes_DoNotMate_EvenWhenTheyLineUp()
        {
            PlacedRoom standard = new PlacedRoom(0, Room("Standard", East(0, ConnectionType.StandardId)), Vector2Int.zero, 0);
            PlacedRoom gate = new PlacedRoom(1, Room("Gate", West(0, Gate)), new Vector2Int(6, 0), 0);
            PlacedRoom matching = new PlacedRoom(2, Room("StandardToo", West(0, ConnectionType.StandardId)), new Vector2Int(6, 0), 0);

            Assert.IsFalse(standard.Doors[0].Mates(gate.Doors[0]), "A standard door must not join a gate.");
            Assert.IsTrue(standard.Doors[0].Mates(matching.Doors[0]));
        }

        [Test]
        public void ValidatorReportsADoorOpeningOntoADoorOfAnotherType()
        {
            PlacedRoom standard = new PlacedRoom(0, Room("Standard", East(0, ConnectionType.StandardId)), Vector2Int.zero, 0);
            PlacedRoom gate = new PlacedRoom(1, Room("Gate", West(0, Gate)), new Vector2Int(6, 0), 0);

            var layout = new Layout(0, 0, new[] { standard, gate }, new PlacedDoor[0]);

            Assert.That(LayoutValidator.Validate(layout).Select(v => v.Rule), Does.Contain(LayoutValidator.RuleDoorFacesWall));
        }

        [Test]
        public void MixedTypeSet_GivesValidLayouts_WhereOnlyMatchingTypesJoin()
        {
            List<RoomDefinition> set = MixedTypeSet();
            var failures = new List<string>();
            int seedsWithAGate = 0;

            for (int seed = 0; seed < 200; seed++)
            {
                GenerationResult result = new LayoutGenerator(seed, set).Generate(12);
                if (!result.Success)
                {
                    failures.Add($"Seed {seed}: {result}");
                    continue;
                }

                failures.AddRange(LayoutValidator.Validate(result.Layout).Select(violation => $"Seed {seed}: {violation}"));

                // Every joined pair has the same type on both sides; the validator only accepts a join through Mates.
                List<PlacedDoor> doors = result.Layout.Rooms.SelectMany(room => room.Doors).ToList();
                var joined = doors.Where(door => doors.Any(other => door.Mates(other))).ToList();
                if (joined.Any(door => door.TypeId == Gate))
                {
                    seedsWithAGate++;
                }
            }

            Assert.That(failures, Is.Empty, string.Join("\n", failures.Take(10)));
            Assert.That(seedsWithAGate, Is.GreaterThan(50), "The test is only meaningful if gates actually get used.");
        }

        [Test]
        public void Selector_OnlyOffersRoomsWithADoorThatCanJoinTheTarget()
        {
            List<RoomDefinition> set = MixedTypeSet();
            PlacedDoor gateTarget = new PlacedRoom(0, Room("Anchor", East(0, Gate)), Vector2Int.zero, 0).Doors[0];
            var context = new SelectionContext(1, 10, 1, new Dictionary<RoomDefinition, int>(), gateTarget);
            var selector = new WeightedRoomSelector();
            var random = new System.Random(9);

            IEnumerable<RoomDefinition> picks = Enumerable.Range(0, 300).Select(_ => selector.Select(random, set, context));

            Assert.That(picks, Has.All.Matches<RoomDefinition>(room => room.Doors.Any(door => door.TypeId == Gate)));
        }

        [Test]
        public void Roomset_WithTwoTypesOfDifferentLengths_IsFine_ButOneTypeWithTwoLengthsIsNot()
        {
            ConnectionType gateType = NewType("Gate");
            RoomBlueprint standardRoom = RoomWithDoor(null, 20f);
            RoomBlueprint gateRoom = RoomWithDoor(gateType, 30f);
            Assert.That(RoomValidator.CheckRoomsetComposition(Roomset(standardRoom, gateRoom)).Select(e => e.Rule),
                Does.Not.Contain(RoomValidator.RuleRoomsetDoorLengths), "Each type has its own length.");

            RoomBlueprint shortGate = RoomWithDoor(gateType, 15f);
            Assert.That(RoomValidator.CheckRoomsetComposition(Roomset(standardRoom, gateRoom, shortGate)).Select(e => e.Rule),
                Does.Contain(RoomValidator.RuleRoomsetDoorLengths));
        }

        [Test]
        public void Roomset_WithTwoTypesThatShareAnId_IsReported()
        {
            RoomBlueprint first = RoomWithDoor(NewType("Gate"), 20f);
            RoomBlueprint second = RoomWithDoor(NewType("Gate"), 20f);

            Assert.That(RoomValidator.CheckRoomsetComposition(Roomset(first, second)).Select(e => e.Rule),
                Does.Contain(RoomValidator.RuleRoomsetConnectionTypeClash));
        }

        [Test]
        public void Baker_CarriesTheDoorTypeIntoTheDefinition()
        {
            ConnectionType gateType = NewType("Gate");
            RoomBlueprint room = RoomWithDoor(gateType, 20f);
            room.Size = new Vector2Int(6, 6);

            RoomDefinition baked = RoomDefinitionBaker.Bake(room, 0, 5f);

            Assert.AreEqual(Gate, baked.Doors[0].TypeId);
        }

        /// <summary>
        /// Standard rooms, a room that turns a standard door into a gate, and gate rooms. Gates are only reachable through the gate room.
        /// </summary>
        private static List<RoomDefinition> MixedTypeSet()
        {
            int standard = ConnectionType.StandardId;
            return new List<RoomDefinition>
            {
                Room("StandardDeadEnd", West(0, standard)),
                Room("StandardCorridor", West(0, standard), East(1, standard)),
                Room("StandardCrossroads", West(0, standard), East(1, standard), North(2, standard), South(3, standard)),
                Room("GateWay", West(0, standard), East(1, Gate)),
                Room("GateCorridor", West(0, Gate), East(1, Gate)),
                Room("GateDeadEnd", West(0, Gate)),
            };
        }

        private static DoorDefinition West(int index, int type) => new DoorDefinition(index, new Vector2Int(2, 3), Side.West, 4, typeId: type);
        private static DoorDefinition East(int index, int type) => new DoorDefinition(index, new Vector2Int(4, 3), Side.East, 4, typeId: type);
        private static DoorDefinition North(int index, int type) => new DoorDefinition(index, new Vector2Int(3, 4), Side.North, 4, typeId: type);
        private static DoorDefinition South(int index, int type) => new DoorDefinition(index, new Vector2Int(3, 2), Side.South, 4, typeId: type);

        private static RoomDefinition Room(string name, params DoorDefinition[] doors)
        {
            return new RoomDefinition(0, name, new Vector2Int(6, 6), Vector2Int.zero, doors.ToList());
        }

        private ConnectionType NewType(string name)
        {
            ConnectionType type = ScriptableObject.CreateInstance<ConnectionType>();
            type.name = name;
            created.Add(type);
            return type;
        }

        private Roomset Roomset(params RoomBlueprint[] rooms)
        {
            Roomset roomset = ScriptableObject.CreateInstance<Roomset>();
            roomset.name = "TypedRoomset";
            roomset.cellSize = 5f;
            roomset.RoomCollection = rooms.ToList();
            created.Add(roomset);
            return roomset;
        }

        private RoomBlueprint RoomWithDoor(ConnectionType type, float length)
        {
            var root = new GameObject("TypedRoom");
            created.Add(root);
            RoomBlueprint room = root.AddComponent<RoomBlueprint>();

            var door = new GameObject("Door");
            door.transform.SetParent(root.transform, false);
            door.transform.position = new Vector3(20, 0, 15);
            door.transform.rotation = Quaternion.LookRotation(Vector3.right);
            ConnectionBlueprint connection = door.AddComponent<ConnectionBlueprint>();
            connection.ConnectionLength = length;
            connection.ConnectionWidth = 1f;
            connection.Type = type;
            connection.location = new Vector2Int(4, 3);
            connection.SealingObject = new GameObject("Seal");
            connection.SealingObject.transform.SetParent(door.transform, false);
            connection.SealingObject.SetActive(false);
            room.SetConnections(new[] { connection });
            return room;
        }
    }
}
