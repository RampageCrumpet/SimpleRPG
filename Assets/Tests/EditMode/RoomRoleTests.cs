using System.Collections.Generic;
using System.Linq;
using LevelGeneration;
using NUnit.Framework;
using UnityEngine;

namespace SimpleRPG.Tests.EditMode
{
    /// <summary>
    /// Exit rooms and minimum counts: the generator must put an exit at the far end of the dungeon and place every room it owes.
    /// </summary>
    public class RoomRoleTests
    {
        private const int Seeds = 200;

        private static DoorDefinition WestDoor() => new DoorDefinition(0, new Vector2Int(2, 3), Side.West, 4);

        private static RoomDefinition ExitRoom(int index) =>
            new RoomDefinition(index, "Exit", new Vector2Int(6, 6), Vector2Int.zero, new[] { WestDoor() }, role: RoomRole.Exit);

        private static RoomDefinition TreasureRoom(int index, int minCount) =>
            new RoomDefinition(index, "Treasure", new Vector2Int(6, 6), Vector2Int.zero, new[] { WestDoor() }, minCount: minCount);

        private static List<RoomDefinition> SetWith(RoomDefinition extra)
        {
            return TestRooms.MixedSet().Concat(new[] { extra }).ToList();
        }

        [Test]
        public void ExitRoom_AppearsOnceAsADeadEnd_AtTheFarthestDeadEnd()
        {
            List<RoomDefinition> definitions = SetWith(ExitRoom(4));
            var failures = new List<string>();

            for (int seed = 0; seed < Seeds; seed++)
            {
                GenerationResult result = new LayoutGenerator(seed, definitions).Generate(10);
                if (!result.Success)
                {
                    failures.Add($"Seed {seed}: {result}");
                    continue;
                }

                List<PlacedRoom> exits = result.Layout.Rooms.Where(room => (room.Definition.Role & RoomRole.Exit) != 0).ToList();
                if (exits.Count != 1)
                {
                    failures.Add($"Seed {seed}: {exits.Count} exit rooms.");
                    continue;
                }

                Dictionary<PlacedRoom, int> distances = Distances(result.Layout);
                int exitDistance = distances[exits[0]];
                PlacedRoom farther = result.Layout.Rooms.FirstOrDefault(room => room != exits[0] && room.Doors.Count == 1 && distances.TryGetValue(room, out int d) && d > exitDistance);
                if (farther != null)
                {
                    failures.Add($"Seed {seed}: exit is {exitDistance} doors from the start but {farther} is {distances[farther]}.");
                }

                foreach (LayoutViolation violation in LayoutValidator.Validate(result.Layout))
                {
                    failures.Add($"Seed {seed}: {violation}");
                }

                for (int i = 0; i < result.Layout.Rooms.Count; i++)
                {
                    if (result.Layout.Rooms[i].Id != i)
                    {
                        failures.Add($"Seed {seed}: room at position {i} has id {result.Layout.Rooms[i].Id}.");
                        break;
                    }
                }
            }

            Assert.That(failures, Is.Empty, string.Join("\n", failures.Take(20)));
        }

        [Test]
        public void ExitRoom_IsNeverUsedToGrowTheDungeon_OrToBeginIt()
        {
            List<RoomDefinition> definitions = new List<RoomDefinition> { ExitRoom(0), TestRooms.Corridor(1), TestRooms.DeadEnd(2) };

            for (int seed = 0; seed < 50; seed++)
            {
                Layout layout = new LayoutGenerator(seed, definitions).Generate(6).Layout;
                Assert.That(layout.Rooms[0].Definition.Name, Is.Not.EqualTo("Exit"), $"Seed {seed}");
                Assert.That(layout.Rooms.Count(room => room.Definition.Name == "Exit"), Is.LessThanOrEqualTo(1), $"Seed {seed}");
            }
        }

        [Test]
        public void MinCount_GuaranteesTheRoomAppears([NUnit.Framework.Range(1, 3)] int minCount)
        {
            List<RoomDefinition> definitions = SetWith(TreasureRoom(4, minCount));
            var failures = new List<string>();

            for (int seed = 0; seed < Seeds; seed++)
            {
                GenerationResult result = new LayoutGenerator(seed, definitions).Generate(10);
                int treasure = result.Layout.Rooms.Count(room => room.Definition.Name == "Treasure");

                if (!result.Success || treasure < minCount)
                {
                    failures.Add($"Seed {seed}: {treasure} treasure rooms, success {result.Success}. {result}");
                }
                else if (LayoutValidator.Validate(result.Layout).Count > 0)
                {
                    failures.Add($"Seed {seed}: {string.Join("; ", LayoutValidator.Validate(result.Layout))}");
                }
            }

            Assert.That(failures, Is.Empty, string.Join("\n", failures.Take(20)));
        }

        [Test]
        public void MinCount_ThatCannotBeMet_FailsAndSaysWhy()
        {
            List<RoomDefinition> definitions = SetWith(TreasureRoom(4, 50));

            GenerationResult result = new LayoutGenerator(1, definitions).Generate(4, 4);

            Assert.IsFalse(result.Success);
            Assert.That(result.RejectedAttempts.Any(reason => reason.Contains("Treasure")), string.Join("\n", result.RejectedAttempts));
        }

        [Test]
        public void Fingerprint_ChangesWithMinCount()
        {
            int one = LayoutFingerprint.OfRoomset(SetWith(TreasureRoom(4, 1)));
            int two = LayoutFingerprint.OfRoomset(SetWith(TreasureRoom(4, 2)));

            Assert.That(two, Is.Not.EqualTo(one));
        }

        private static Dictionary<PlacedRoom, int> Distances(Layout layout)
        {
            var sealedDoors = new HashSet<PlacedDoor>(layout.SealedDoors);
            var distances = new Dictionary<PlacedRoom, int> { [layout.Rooms[0]] = 0 };
            var queue = new Queue<PlacedRoom>();
            queue.Enqueue(layout.Rooms[0]);

            while (queue.Count > 0)
            {
                PlacedRoom room = queue.Dequeue();
                foreach (PlacedDoor door in room.Doors.Where(door => !sealedDoors.Contains(door)))
                {
                    PlacedDoor mate = layout.Rooms.SelectMany(other => other.Doors).FirstOrDefault(door.Mates);
                    if (mate != null && !distances.ContainsKey(mate.Room))
                    {
                        distances[mate.Room] = distances[room] + 1;
                        queue.Enqueue(mate.Room);
                    }
                }
            }

            return distances;
        }
    }
}
