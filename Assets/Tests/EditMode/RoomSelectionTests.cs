using System.Collections.Generic;
using System.Linq;
using SimpleRPG.LevelGeneration;
using NUnit.Framework;
using UnityEngine;

namespace SimpleRPG.Tests.EditMode
{
    /// <summary>
    /// Checks the room selection policy: designated start rooms, weights, per-room limits and the dead-end bias.
    /// </summary>
    public class RoomSelectionTests
    {
        private const int Seeds = 100;

        [Test]
        public void StartRoom_BeginsTheDungeonAndIsNeverUsedElsewhere()
        {
            RoomDefinition entrance = WithPolicy(TestRooms.DeadEnd(4), role: RoomRole.Start);
            List<RoomDefinition> rooms = TestRooms.MixedSet();
            rooms.Add(entrance);

            for (int seed = 0; seed < Seeds; seed++)
            {
                GenerationResult result = new LayoutGenerator(seed, rooms).Generate(10);

                Assert.IsTrue(result.Success, result.ToString());
                Assert.AreSame(entrance, result.Layout.Rooms[0].Definition, $"Seed {seed} did not begin in the start room.");
                Assert.That(result.Layout.Rooms.Count(room => room.Definition == entrance), Is.EqualTo(1), $"Seed {seed} used the start room again.");
            }
        }

        [Test]
        public void RoomsAtTheirMaximumCount_AreNotPlacedAgain()
        {
            RoomDefinition limitedCrossroads = WithPolicy(TestRooms.Crossroads(3), maxCount: 2);
            var rooms = new List<RoomDefinition> { TestRooms.DeadEnd(0), TestRooms.Corridor(1), TestRooms.Corner(2), limitedCrossroads };

            for (int seed = 0; seed < Seeds; seed++)
            {
                GenerationResult result = new LayoutGenerator(seed, rooms).Generate(12);

                Assert.IsTrue(result.Success, result.ToString());
                Assert.That(result.Layout.Rooms.Count(room => room.Definition == limitedCrossroads), Is.LessThanOrEqualTo(2), $"Seed {seed} used the limited room too often.");
            }
        }

        [Test]
        public void DeadEnds_AreFavouredOnceTheMinimumIsReached()
        {
            List<RoomDefinition> rooms = TestRooms.MixedSet();
            var selector = new WeightedRoomSelector();

            float before = DeadEndShare(selector, rooms, placed: 2, minimum: 10);
            float after = DeadEndShare(selector, rooms, placed: 10, minimum: 10);

            Assert.That(before, Is.EqualTo(0.25f).Within(0.05f), "Before the minimum every room is equally likely.");
            Assert.That(after, Is.EqualTo(0.5f).Within(0.05f), "After the minimum a dead end weighs three times as much as each of the others.");
        }

        [Test]
        public void WeightsDecideHowOftenARoomIsPicked()
        {
            RoomDefinition common = WithPolicy(TestRooms.Corridor(0), weight: 9f);
            RoomDefinition rare = WithPolicy(TestRooms.Corner(1), weight: 1f);
            var rooms = new List<RoomDefinition> { common, rare };
            var random = new System.Random(5);
            var selector = new WeightedRoomSelector();
            var context = new SelectionContext(1, 10, 1, new Dictionary<RoomDefinition, int>());

            int commonPicks = Enumerable.Range(0, 2000).Count(_ => selector.Select(random, rooms, context) == common);

            Assert.That(commonPicks / 2000f, Is.EqualTo(0.9f).Within(0.03f));
        }

        [Test]
        public void ZeroWeightRooms_AreNeverPicked()
        {
            RoomDefinition banned = WithPolicy(TestRooms.Corridor(0), weight: 0f);
            RoomDefinition allowed = TestRooms.Corner(1);
            var random = new System.Random(1);
            var selector = new WeightedRoomSelector();
            var context = new SelectionContext(1, 10, 1, new Dictionary<RoomDefinition, int>());

            Assert.That(Enumerable.Range(0, 200).Select(_ => selector.Select(random, new[] { banned, allowed }, context)), Has.All.SameAs(allowed));
            Assert.Throws<System.ArgumentException>(() => new LayoutGenerator(1, new[] { banned }));
        }

        [Test]
        public void ValidatorReportsRoomsUsedPastTheirLimit()
        {
            RoomDefinition limited = WithPolicy(TestRooms.Corridor(0), maxCount: 1);
            var first = new PlacedRoom(0, limited, Vector2Int.zero, 0);
            var second = new PlacedRoom(1, limited, new Vector2Int(6, 0), 0);
            var layout = new Layout(0, 0, new[] { first, second }, new[] { first.Doors[0], second.Doors[1] });

            Assert.That(LayoutValidator.Validate(layout).Select(v => v.Rule), Does.Contain(LayoutValidator.RuleRoomOveruse));
        }

        [Test]
        public void ValidatorReportsStartRoomsPlacedAwayFromTheStart()
        {
            RoomDefinition entrance = WithPolicy(TestRooms.Corridor(0), role: RoomRole.Start);
            var first = new PlacedRoom(0, TestRooms.Corridor(1), Vector2Int.zero, 0);
            var second = new PlacedRoom(1, entrance, new Vector2Int(6, 0), 0);
            var layout = new Layout(0, 0, new[] { first, second }, new[] { first.Doors[0], second.Doors[1] });

            Assert.That(LayoutValidator.Validate(layout).Select(v => v.Rule), Does.Contain(LayoutValidator.RuleStartRoomMisplaced));
        }

        private static float DeadEndShare(IRoomSelector selector, List<RoomDefinition> rooms, int placed, int minimum)
        {
            var random = new System.Random(11);
            var context = new SelectionContext(placed, minimum, 1, new Dictionary<RoomDefinition, int>());
            const int draws = 4000;

            return Enumerable.Range(0, draws).Count(_ => selector.Select(random, rooms, context).IsDeadEnd) / (float)draws;
        }

        private static RoomDefinition WithPolicy(RoomDefinition room, float weight = 1f, int maxCount = 0, RoomRole role = RoomRole.None)
        {
            return new RoomDefinition(room.Index, room.Name, room.Size, room.OriginOffset, room.Doors, weight, maxCount, role);
        }
    }
}
