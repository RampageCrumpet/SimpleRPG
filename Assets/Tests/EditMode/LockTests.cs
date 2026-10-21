using System.Collections.Generic;
using System.Linq;
using LevelGeneration;
using NUnit.Framework;
using UnityEditor;

namespace SimpleRPG.Tests.EditMode
{
    /// <summary>
    /// Locked doors and keys: the generator must never shut anyone in, and the validator must notice if it did.
    /// </summary>
    public class LockTests
    {
        private const string ModularDungeonPath = "Assets/Prefabs/World/TileSets/ModularDungeon/ModularDungeon.asset";
        private const int Seeds = 40;

        private static List<RoomDefinition> RealRooms() =>
            RoomDefinitionBaker.Bake(AssetDatabase.LoadAssetAtPath<Roomset>(ModularDungeonPath));

        private static List<RoomDefinition> MixedWithExit()
        {
            var exit = new RoomDefinition(4, "Exit", new UnityEngine.Vector2Int(6, 6), UnityEngine.Vector2Int.zero,
                new[] { new DoorDefinition(0, new UnityEngine.Vector2Int(2, 3), Side.West, 4) }, role: RoomRole.Exit);
            return TestRooms.MixedSet().Concat(new[] { exit }).ToList();
        }

        private static GenerationResult Generate(List<RoomDefinition> definitions, int seed, int locks, int roomCount = 15)
        {
            return new LayoutGenerator(seed, definitions) { Style = new LayoutStyle { LockedDoors = locks } }.Generate(roomCount);
        }

        [Test]
        public void Locks_AreAlwaysSolvable_AndValid([NUnit.Framework.Range(1, 3)] int lockCount)
        {
            var failures = new List<string>();
            int placed = 0;

            foreach (List<RoomDefinition> definitions in new[] { RealRooms(), TestRooms.MixedSet(), MixedWithExit() })
            {
                for (int seed = 0; seed < Seeds; seed++)
                {
                    GenerationResult result = Generate(definitions, seed, lockCount);
                    placed += result.Layout.Locks.Count;

                    if (!result.Success)
                    {
                        failures.Add($"Seed {seed}: {result}");
                    }
                    else if (result.Layout.Locks.Count > lockCount)
                    {
                        failures.Add($"Seed {seed}: {result.Layout.Locks.Count} locks for a request of {lockCount}.");
                    }

                    foreach (LayoutViolation violation in LayoutValidator.Validate(result.Layout))
                    {
                        failures.Add($"Seed {seed}: {violation}");
                    }
                }
            }

            Assert.That(failures, Is.Empty, string.Join("\n", failures.Take(20)));
            Assert.That(placed, Is.GreaterThan(0), "No lock was ever placed.");
        }

        [Test]
        public void RealRoomset_PlacesEveryRequestedLock()
        {
            List<RoomDefinition> definitions = RealRooms();
            int shortCount = 0;

            for (int seed = 0; seed < Seeds; seed++)
            {
                if (Generate(definitions, seed, 2, 20).Layout.Locks.Count < 2)
                {
                    shortCount++;
                }
            }

            Assert.That(shortCount, Is.LessThanOrEqualTo(Seeds / 10), $"{shortCount} of {Seeds} layouts could not place two locks.");
        }

        [Test]
        public void FirstLock_StandsBetweenTheStartAndTheExit()
        {
            List<RoomDefinition> definitions = MixedWithExit();
            var failures = new List<string>();

            for (int seed = 0; seed < Seeds; seed++)
            {
                Layout layout = Generate(definitions, seed, 1).Layout;
                PlacedRoom exit = layout.Rooms.First(room => (room.Definition.Role & RoomRole.Exit) != 0);
                LockedDoor locked = layout.Locks.FirstOrDefault();
                if (locked == null)
                {
                    failures.Add($"Seed {seed}: no lock placed.");
                    continue;
                }

                List<(PlacedDoor a, PlacedDoor b)> joins = LockSolver.Joins(layout.Rooms, layout.SealedDoors);
                (PlacedDoor a, PlacedDoor b) join = joins.First(j => j.a == locked.Door || j.a == locked.Mate);
                if (LockSolver.Connected(layout.Rooms, joins, join).Contains(exit))
                {
                    failures.Add($"Seed {seed}: the exit can be reached without crossing {locked}.");
                }
            }

            Assert.That(failures, Is.Empty, string.Join("\n", failures.Take(20)));
        }

        [Test]
        public void SameSeed_GivesTheSameLocks()
        {
            List<RoomDefinition> definitions = RealRooms();

            int first = LayoutFingerprint.OfLayout(Generate(definitions, 7, 2, 20).Layout);
            int second = LayoutFingerprint.OfLayout(Generate(definitions, 7, 2, 20).Layout);

            Assert.That(second, Is.EqualTo(first));
        }

        [Test]
        public void NoLocksRequested_ChangesNothing()
        {
            List<RoomDefinition> definitions = RealRooms();

            Layout plain = new LayoutGenerator(3, definitions).Generate(15).Layout;
            Layout none = Generate(definitions, 3, 0).Layout;

            Assert.That(plain.Locks, Is.Empty);
            Assert.That(LayoutFingerprint.OfLayout(none), Is.EqualTo(LayoutFingerprint.OfLayout(plain)));
        }

        [Test]
        public void Validator_FlagsAKeyBehindItsOwnLock()
        {
            Layout layout = FindLayoutWithABridge(out (PlacedDoor a, PlacedDoor b) join, out PlacedRoom farRoom);
            var locks = new[] { new LockedDoor(join.a, join.b, 1, farRoom) };
            var broken = new Layout(layout.Seed, layout.Attempt, layout.Rooms, layout.SealedDoors, layout.FloorCount, locks);

            Assert.That(LayoutValidator.Validate(broken).Select(violation => violation.Rule), Does.Contain(LayoutValidator.RuleLockUnsolvable));
        }

        [Test]
        public void Validator_FlagsDuplicateKeys_AndLocksOnNoJoin()
        {
            Layout layout = FindLayoutWithABridge(out (PlacedDoor a, PlacedDoor b) join, out PlacedRoom farRoom);
            PlacedRoom keyRoom = layout.Rooms[0];

            var duplicate = new Layout(layout.Seed, layout.Attempt, layout.Rooms, layout.SealedDoors, layout.FloorCount,
                new[] { new LockedDoor(join.a, join.b, 1, keyRoom), new LockedDoor(join.a, join.b, 1, keyRoom) });
            Assert.That(LayoutValidator.Validate(duplicate).Select(violation => violation.Rule), Does.Contain(LayoutValidator.RuleLockInvalid));

            PlacedDoor unrelated = layout.Rooms.SelectMany(room => room.Doors).First(door => door != join.a && door != join.b && !door.Mates(join.a));
            var stray = new Layout(layout.Seed, layout.Attempt, layout.Rooms, layout.SealedDoors, layout.FloorCount,
                new[] { new LockedDoor(join.a, unrelated, 1, keyRoom) });
            Assert.That(LayoutValidator.Validate(stray).Select(violation => violation.Rule), Does.Contain(LayoutValidator.RuleLockInvalid));
        }

        private static Layout FindLayoutWithABridge(out (PlacedDoor a, PlacedDoor b) join, out PlacedRoom farRoom)
        {
            Layout layout = new LayoutGenerator(1, TestRooms.MixedSet()).Generate(8).Layout;
            List<(PlacedDoor a, PlacedDoor b)> joins = LockSolver.Joins(layout.Rooms, layout.SealedDoors);

            foreach ((PlacedDoor a, PlacedDoor b) candidate in joins)
            {
                HashSet<PlacedRoom> startSide = LockSolver.Connected(layout.Rooms, joins, candidate);
                PlacedRoom far = layout.Rooms.FirstOrDefault(room => !startSide.Contains(room));
                if (far != null)
                {
                    join = candidate;
                    farRoom = far;
                    return layout;
                }
            }

            Assert.Fail("The layout has no join that cuts anything off.");
            join = default;
            farRoom = null;
            return layout;
        }
    }
}
