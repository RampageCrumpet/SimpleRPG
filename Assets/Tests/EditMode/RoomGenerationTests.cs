using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using SimpleRPG.LevelGeneration;
using SimpleRPG.RoomGeneration;
using UnityEngine;

namespace SimpleRPG.Tests.EditMode
{
    /// <summary>
    /// Structural plans only: no scene, no meshes. Covers the geometry primitives, seeds, connection mapping, the validator, stairs,
    /// the chamber generator and the retry/fallback pipeline.
    /// </summary>
    public class RoomGenerationTests
    {
        private static DoorDefinition Door(int index, int x, int y, Side side, int width = 2, int level = 0, int length = 0, int delta = 0) =>
            new DoorDefinition(index, new Vector2Int(x, y), side, length, width, ConnectionType.StandardId, level, delta);

        private static RoomGenerationContext Context(int w, int h, int seed, ChamberSettings chamber = null, int levels = 1, RoomGenerationSettings settings = null, params DoorDefinition[] doors) =>
            new RoomGenerationContext("test", new Vector2Int(w, h), levels, doors, seed, settings ?? new RoomGenerationSettings(), chamber);

        private static RoomGenerationContext TwoDoors(int w, int h, int seed, ChamberSettings chamber = null) =>
            Context(w, h, seed, chamber, 1, null, Door(0, 0, h / 2, Side.West), Door(1, w, h / 2, Side.East));

        private static void AssertValid(GeneratedRoom room, RoomGenerationContext context)
        {
            Assert.IsFalse(room.IsFailure, room.Failure);
            RoomPlanReport report = RoomPlanValidator.Validate(room, context);
            Assert.IsTrue(report.IsValid, report.ToString());
        }

        // ---- Primitives ----

        [Test]
        public void Rect_SharedEdgeIsNotOverlap_AndSubtractKeepsArea()
        {
            var a = new RectMm(0, 0, 10, 10);
            Assert.IsFalse(a.Overlaps(new RectMm(10, 0, 20, 10)));
            Assert.IsTrue(a.Overlaps(new RectMm(9, 9, 20, 20)));

            var pieces = new List<RectMm>();
            a.Subtract(new RectMm(3, 4, 6, 7), pieces);
            long area = pieces.Sum(p => (long)p.Width * p.Height);
            Assert.AreEqual(100L - 9, area);
            for (int i = 0; i < pieces.Count; i++)
            {
                for (int j = i + 1; j < pieces.Count; j++)
                {
                    Assert.IsFalse(pieces[i].Overlaps(pieces[j]));
                }
            }
        }

        [Test]
        public void Box_SubtractLeavesLintelAndSides()
        {
            var wall = new BoxMm(new RectMm(0, 0, 10, 1), 0, 10);
            var pieces = new List<BoxMm>();
            wall.Subtract(new BoxMm(new RectMm(4, 0, 6, 1), 0, 6), pieces);
            long volume = pieces.Sum(p => (long)p.Area.Width * p.Area.Height * (p.MaxZ - p.MinZ));
            Assert.AreEqual(100L - 12, volume);
            Assert.IsFalse(pieces.Any(p => p.Overlaps(new BoxMm(new RectMm(4, 0, 6, 1), 0, 6))));
        }

        // ---- Seeds ----

        [Test]
        public void Seed_IsStableAndSensitiveToEveryInput()
        {
            int baseline = RoomSeed.ForRoom(7, 0, 3, 2, "chamber", 1);
            Assert.AreEqual(baseline, RoomSeed.ForRoom(7, 0, 3, 2, "chamber", 1));
            Assert.AreNotEqual(baseline, RoomSeed.ForRoom(8, 0, 3, 2, "chamber", 1));
            Assert.AreNotEqual(baseline, RoomSeed.ForRoom(7, 1, 3, 2, "chamber", 1));
            Assert.AreNotEqual(baseline, RoomSeed.ForRoom(7, 0, 4, 2, "chamber", 1));
            Assert.AreNotEqual(baseline, RoomSeed.ForRoom(7, 0, 3, 3, "chamber", 1));
            Assert.AreNotEqual(baseline, RoomSeed.ForRoom(7, 0, 3, 2, "maze", 1));
            Assert.AreNotEqual(baseline, RoomSeed.ForRoom(7, 0, 3, 2, "chamber", 2));
            Assert.AreEqual(baseline, RoomSeed.ForRetry(baseline, 0));
            Assert.AreNotEqual(baseline, RoomSeed.ForRetry(baseline, 1));
        }

        [Test]
        public void Random_IsReproducibleAndInRange()
        {
            var a = new RoomRandom(42);
            var b = new RoomRandom(42);
            for (int i = 0; i < 100; i++)
            {
                int value = a.Next(17);
                Assert.AreEqual(value, b.Next(17));
                Assert.IsTrue(value >= 0 && value < 17);
            }
        }

        // ---- Connections ----

        [Test]
        public void Connections_MapToExactMillimetres()
        {
            RoomGenerationContext context = Context(6, 6, 1, null, 1, null, Door(0, 0, 3, Side.West), Door(1, 6, 3, Side.East, 4));
            ConnectionMap map = ConnectionMap.Build(context);

            Assert.IsNull(map.Failure);
            Assert.AreEqual(2, map.Openings.Count);
            Assert.AreEqual(new RectMm(0, 4000, 300, 8000), map.Openings[0].Volume.Area);
            Assert.AreEqual(new RectMm(11700, 4000, 12000, 8000), map.Openings[1].Volume.Area.Intersect(new RectMm(11700, 4000, 12000, 8000)));
            Assert.AreEqual(8000, map.Openings[1].Volume.Area.Height);
            Assert.AreEqual(3000, map.Openings[0].Volume.MaxZ);
        }

        [Test]
        public void Connections_Unsupported_AreRejectedWithReasons()
        {
            Assert.IsNotNull(ConnectionMap.Build(Context(6, 6, 1, null, 1, null, Door(0, 0, 3, Side.West, delta: 1))).Failure);
            Assert.IsNotNull(ConnectionMap.Build(Context(6, 6, 1, null, 1, null, Door(0, 0, 3, Side.West, length: 2))).Failure);
            Assert.IsNotNull(ConnectionMap.Build(Context(6, 6, 1, null, 1, null, Door(0, 2, 3, Side.West))).Failure, "off the edge it faces");
            Assert.IsNotNull(ConnectionMap.Build(Context(6, 6, 1, null, 1, null, Door(0, 0, 0, Side.West))).Failure, "span leaves the edge");
            Assert.IsNotNull(ConnectionMap.Build(Context(6, 6, 1, null, 1, null, Door(0, 0, 3, Side.West, level: 1))).Failure, "level above the room");
            Assert.IsNotNull(ConnectionMap.Build(Context(6, 6, 1, null, 1, null, Door(0, 0, 3, Side.West), Door(1, 0, 4, Side.West))).Failure, "overlapping doors");

            GeneratedRoom room = new ChamberGenerator().Generate(Context(6, 6, 1, null, 1, null, Door(0, 0, 3, Side.West, length: 2)));
            Assert.IsTrue(room.IsFailure);
            StringAssert.Contains("length", room.Failure);
        }

        // ---- Chamber ----

        [Test]
        public void EmptyChamber_IsValid_AndHasNoInteriorFeatures()
        {
            RoomGenerationContext context = TwoDoors(6, 6, 5);
            GeneratedRoom room = new ChamberGenerator().Generate(context);

            AssertValid(room, context);
            Assert.AreEqual(0, room.Solids.Count);
            Assert.AreEqual(4, room.Walls.Count);
            Assert.AreEqual(0, room.Stairs.Count);
            Assert.AreEqual(1, room.Ceilings.Count);
        }

        [Test]
        public void Chamber_PreservesEveryConnectionExactly()
        {
            RoomGenerationContext context = Context(8, 6, 11, null, 1, null,
                Door(0, 0, 2, Side.West), Door(1, 8, 4, Side.East), Door(2, 3, 6, Side.North, 4), Door(3, 5, 0, Side.South));
            GeneratedRoom room = new ChamberGenerator().Generate(context);
            AssertValid(room, context);

            ConnectionMap map = ConnectionMap.Build(context);
            for (int i = 0; i < map.Openings.Count; i++)
            {
                Assert.AreEqual(map.Openings[i].Volume, room.Openings[i].Volume);
                Assert.AreEqual(map.Protected[i].Approach, room.Protected[i].Approach);
            }
        }

        [Test]
        public void Chamber_ConnectionsNearCornersAndOnAdjacentWalls()
        {
            RoomGenerationContext context = Context(6, 6, 3, null, 1, null, Door(0, 0, 1, Side.West), Door(1, 1, 6, Side.North), Door(2, 6, 5, Side.East), Door(3, 5, 0, Side.South));
            AssertValid(new ChamberGenerator().Generate(context), context);
        }

        [Test]
        public void Chamber_TooSmall_FailsWithReason()
        {
            RoomGenerationContext context = Context(1, 1, 1, null, 1, null, Door(0, 0, 1, Side.West, 2));
            GeneratedRoom room = new ChamberGenerator().Generate(context);
            Assert.IsTrue(room.IsFailure);
            Assert.IsNotEmpty(room.Failure);
        }

        [Test]
        public void Chamber_WithPillarsAndPartitions_AlwaysValid_OverManySeeds()
        {
            foreach (PillarLayout layout in new[] { PillarLayout.Grid, PillarLayout.Rows })
            {
                for (int seed = 0; seed < 40; seed++)
                {
                    var chamber = new ChamberSettings { Pillars = layout, Partitions = seed % 3 };
                    foreach ((int w, int h) in new[] { (6, 6), (10, 4), (4, 10), (12, 12) })
                    {
                        RoomGenerationContext context = TwoDoors(w, h, seed, chamber);
                        GeneratedRoom room = new ChamberGenerator().Generate(context);
                        AssertValid(room, context);
                    }
                }
            }
        }

        [Test]
        public void Chamber_Pillars_ActuallyAppearInALargeRoom()
        {
            RoomGenerationContext context = TwoDoors(12, 12, 9, new ChamberSettings { Pillars = PillarLayout.Grid });
            GeneratedRoom room = new ChamberGenerator().Generate(context);
            AssertValid(room, context);
            Assert.Greater(room.Solids.Count, 0);
        }

        // ---- Determinism ----

        [Test]
        public void SameContext_SamePlanAndChecksum_RegardlessOfOrder()
        {
            var chamber = new ChamberSettings { Pillars = PillarLayout.Grid, Partitions = 2 };
            RoomGenerationContext a = TwoDoors(10, 8, 21, chamber);
            RoomGenerationContext b = TwoDoors(8, 10, 22, chamber);

            int a1 = RoomPlanChecksum.Of(new ChamberGenerator().Generate(a));
            int b1 = RoomPlanChecksum.Of(new ChamberGenerator().Generate(b));
            int b2 = RoomPlanChecksum.Of(new ChamberGenerator().Generate(b));
            int a2 = RoomPlanChecksum.Of(new ChamberGenerator().Generate(a));

            Assert.AreEqual(a1, a2);
            Assert.AreEqual(b1, b2);
            Assert.AreNotEqual(a1, b1);
        }

        [Test]
        public void DifferentSeeds_GiveDifferentPartitionedRooms()
        {
            var chamber = new ChamberSettings { Pillars = PillarLayout.Grid, Partitions = 2 };
            var checksums = new HashSet<int>();
            for (int seed = 0; seed < 20; seed++)
            {
                checksums.Add(RoomPlanChecksum.Of(new ChamberGenerator().Generate(TwoDoors(12, 12, seed, chamber))));
            }

            Assert.Greater(checksums.Count, 1);
        }

        // ---- Validator ----

        [Test]
        public void Validator_CatchesPillarInDoorway()
        {
            RoomGenerationContext context = TwoDoors(6, 6, 1);
            GeneratedRoom room = new ChamberGenerator().Generate(context);
            ProtectedRegion region = room.Protected[0];
            room.Solids.Add(new SolidVolume(new BoxMm(region.Approach.Area.Inflate(-100), 0, 3000)));

            Assert.IsTrue(RoomPlanValidator.Validate(room, context).Has(RoomPlanValidator.Obstructed));
        }

        [Test]
        public void Validator_CatchesWallThatSplitsTheRoom()
        {
            RoomGenerationContext context = TwoDoors(8, 6, 1);
            GeneratedRoom room = new ChamberGenerator().Generate(context);
            RectMm interior = RoomShell.Interior(context);
            room.Walls.Add(new WallSegment(new RectMm(8000 - 150, interior.MinY, 8000 + 150, interior.MaxY), 0, 4000, WallKind.Partition));

            Assert.IsTrue(RoomPlanValidator.Validate(room, context).Has(RoomPlanValidator.Unreachable));
        }

        [Test]
        public void Validator_CatchesGapNarrowerThanMinimumPassage()
        {
            RoomGenerationContext context = TwoDoors(8, 6, 1);
            GeneratedRoom room = new ChamberGenerator().Generate(context);
            RectMm interior = RoomShell.Interior(context);
            // Leaves a 600 mm gap, half the 1200 mm minimum.
            room.Walls.Add(new WallSegment(new RectMm(8000 - 150, interior.MinY + 600, 8000 + 150, interior.MaxY), 0, 4000, WallKind.Partition));

            Assert.IsFalse(RoomPlanValidator.Validate(room, context).IsValid);

            room = new ChamberGenerator().Generate(context);
            room.Walls.Add(new WallSegment(new RectMm(8000 - 150, interior.MinY + 1500, 8000 + 150, interior.MaxY), 0, 4000, WallKind.Partition));
            Assert.IsTrue(RoomPlanValidator.Validate(room, context).IsValid, "a 1500 mm gap is wide enough");
        }

        [Test]
        public void Validator_NeverAcceptsAGapNarrowerThanTheMinimumPassage_AtAnyResolutionOrAlignment()
        {
            // A partition leaves a gap beside the south wall. Whatever the raster step and however the partition is shifted off the raster,
            // a gap under the 1200 mm minimum must be refused: rounding may only ever make the check stricter.
            foreach (int step in new[] { 50, 100, 250, 500 })
            {
                for (int shift = 0; shift < 100; shift += 21)
                {
                    for (int gap = 1000; gap <= 1700; gap += 37)
                    {
                        var settings = new RoomGenerationSettings { ValidationResolutionMm = step };
                        RoomGenerationContext context = Context(8, 6, 1, null, 1, settings, Door(0, 0, 3, Side.West), Door(1, 8, 3, Side.East));
                        GeneratedRoom room = new ChamberGenerator().Generate(context);
                        room.Walls.Add(new WallSegment(new RectMm(7850 + shift, 300 + gap + shift, 8150 + shift, 11700), 0, 4000, WallKind.Partition));

                        bool accepted = RoomPlanValidator.Validate(room, context).IsValid;
                        Assert.IsFalse(accepted && gap + shift < 1200, $"accepted a {gap + shift} mm gap at raster step {step}");
                    }
                }
            }
        }

        [Test]
        public void Validator_CatchesMovedOpening_AndOutOfBounds()
        {
            RoomGenerationContext context = TwoDoors(6, 6, 1);
            GeneratedRoom room = new ChamberGenerator().Generate(context);
            Opening moved = room.Openings[0];
            room.Openings[0] = new Opening(moved.ConnectionIndex, moved.Side, new BoxMm(moved.Volume.Area.Translate(0, 500), moved.Volume.MinZ, moved.Volume.MaxZ));
            Assert.IsTrue(RoomPlanValidator.Validate(room, context).Has(RoomPlanValidator.ConnectionChanged));

            room = new ChamberGenerator().Generate(context);
            room.Solids.Add(new SolidVolume(new BoxMm(new RectMm(11000, 5000, 13000, 6000), 0, 1000)));
            Assert.IsTrue(RoomPlanValidator.Validate(room, context).Has(RoomPlanValidator.OutOfBounds));
        }

        [Test]
        public void Validator_AllowsASingleConnectionRoomWithAnInterior_AndRejectsTinyOnes()
        {
            RoomGenerationContext context = Context(6, 6, 2, null, 1, null, Door(0, 0, 3, Side.West));
            AssertValid(new ChamberGenerator().Generate(context), context);

            // A wall that boxes in the approach leaves only the doorway: no usable interior.
            GeneratedRoom room = new ChamberGenerator().Generate(context);
            RectMm approach = room.Protected[0].Approach.Area;
            room.Walls.Add(new WallSegment(new RectMm(approach.MaxX, 300, approach.MaxX + 300, 11700), 0, 4000, WallKind.Partition));
            Assert.IsFalse(RoomPlanValidator.Validate(room, context).IsValid);
        }

        // ---- Stairs ----

        [Test]
        public void StairPlanner_RisersAndTreadsMeetTheLimits_AndRiseIsExact()
        {
            var settings = new RoomGenerationSettings();
            for (int flights = 1; flights <= 4; flights++)
            {
                StairTemplate template = StairPlanner.Plan(settings, 8000, flights);
                StairRun run = template.Place(Side.East, 0, 0, 0, 0, 200);
                Assert.AreEqual(flights, run.Flights.Count);
                Assert.AreEqual(flights - 1, run.Landings.Count);
                Assert.AreEqual(8000, run.Last.TopZ);

                foreach (StairFlight flight in run.Flights)
                {
                    int previous = flight.BaseZ;
                    for (int step = 0; step < flight.Steps; step++)
                    {
                        int riser = flight.StepTopZ(step) - previous;
                        Assert.LessOrEqual(riser, settings.StepHeightMm);
                        Assert.Greater(riser, 0);
                        previous = flight.StepTopZ(step);
                    }

                    Assert.AreEqual(flight.TopZ, previous);
                    Assert.GreaterOrEqual(flight.RunMm / flight.Steps, settings.StepDepthMm);
                }
            }
        }

        [Test]
        public void StairPlanner_StepsTileTheFlightExactly()
        {
            var settings = new RoomGenerationSettings();
            StairRun run = StairPlanner.Plan(settings, 8000, 1).Place(Side.North, 1000, 2000, 0, 0, 200);
            StairFlight flight = run.First;
            int covered = 0;
            for (int step = 0; step < flight.Steps; step++)
            {
                RectMm area = flight.StepArea(step);
                Assert.AreEqual(flight.Area.MinX, area.MinX);
                Assert.AreEqual(flight.Area.MaxX, area.MaxX);
                covered += area.Height;
            }

            Assert.AreEqual(flight.Area.Height, covered);
        }

        [Test]
        public void Chamber_WithDoorsOnTwoLevels_BuildsStairsThatConnectThem()
        {
            for (int seed = 0; seed < 30; seed++)
            {
                RoomGenerationContext context = Context(10, 10, seed, null, 2, null, Door(0, 0, 3, Side.West, level: 0), Door(1, 10, 7, Side.East, level: 1));
                GeneratedRoom room = new ChamberGenerator().Generate(context);
                AssertValid(room, context);
                Assert.AreEqual(1, room.Stairs.Count);
                Assert.AreEqual(8000, room.Stairs[0].Last.TopZ);
                Assert.IsTrue(room.Floors.Any(f => f.Level == 1 && f.Area.Width < 20000 || f.Level == 1 && f.Area.Height < 20000), "the upper floor has a well");
            }
        }

        [Test]
        public void Chamber_ThreeLevels_GetTwoStairsAndAllDoorsConnect()
        {
            for (int seed = 0; seed < 10; seed++)
            {
                RoomGenerationContext context = Context(12, 12, seed, new ChamberSettings { Pillars = PillarLayout.Grid }, 3, null,
                    Door(0, 0, 3, Side.West, level: 0), Door(1, 12, 9, Side.East, level: 2), Door(2, 6, 12, Side.North, level: 1));
                GeneratedRoom room = new ChamberGenerator().Generate(context);
                AssertValid(room, context);
                Assert.AreEqual(2, room.Stairs.Count);
            }
        }

        [Test]
        public void Chamber_StairsThatCannotFit_FailClearly()
        {
            RoomGenerationContext context = Context(4, 4, 1, null, 2, null, Door(0, 0, 2, Side.West, level: 0), Door(1, 4, 2, Side.East, level: 1));
            GeneratedRoom room = new ChamberGenerator().Generate(context);
            Assert.IsTrue(room.IsFailure);
            StringAssert.Contains("stairs", room.Failure.ToLowerInvariant());
        }

        [Test]
        public void Chamber_UpperDoorOnly_BuildsNoStairs()
        {
            RoomGenerationContext context = Context(6, 6, 1, null, 2, null, Door(0, 0, 3, Side.West, level: 1), Door(1, 6, 3, Side.East, level: 1));
            GeneratedRoom room = new ChamberGenerator().Generate(context);
            AssertValid(room, context);
            Assert.AreEqual(0, room.Stairs.Count);
            Assert.AreEqual(8000, room.Floors[0].TopZ);
        }

        [Test]
        public void Validator_CatchesStairsMissingOrBlocked()
        {
            RoomGenerationContext context = Context(10, 10, 1, null, 2, null, Door(0, 0, 3, Side.West, level: 0), Door(1, 10, 7, Side.East, level: 1));
            GeneratedRoom room = new ChamberGenerator().Generate(context);
            room.Stairs.Clear();
            Assert.IsTrue(RoomPlanValidator.Validate(room, context).Has(RoomPlanValidator.Stairs));

            room = new ChamberGenerator().Generate(context);
            StairRun run = room.Stairs[0];
            // Put a ceiling-height slab across the climb: no headroom.
            room.Solids.Add(new SolidVolume(new BoxMm(run.First.Area, run.First.StepTopZ(2) + 500, run.First.StepTopZ(2) + 700)));
            Assert.IsFalse(RoomPlanValidator.Validate(room, context).IsValid);
        }

        // ---- Pipeline ----

        private sealed class BrokenGenerator : IRoomGenerator
        {
            public int Calls;
            public string Id => "broken";
            public int Version => 3;

            public GeneratedRoom Generate(RoomGenerationContext context)
            {
                Calls++;
                var room = new ChamberGenerator().Generate(context);
                RectMm interior = RoomShell.Interior(context);
                room.Walls.Add(new WallSegment(new RectMm(interior.CenterX - 150, interior.MinY, interior.CenterX + 150, interior.MaxY), 0, 4000, WallKind.Partition));
                return room;
            }
        }

        [Test]
        public void Pipeline_FallsBackToAnOpenChamber_AndReportsTheFailure()
        {
            RoomGenerationContext context = TwoDoors(8, 6, 77);
            var broken = new BrokenGenerator();
            RoomGenerationResult result = RoomGenerationPipeline.Generate(context, broken, 2);

            Assert.AreEqual(3, broken.Calls);
            Assert.IsTrue(result.Succeeded);
            Assert.IsTrue(result.UsedFallback);
            Assert.IsTrue(RoomPlanValidator.Validate(result.Room, result.Context).IsValid);
            Assert.AreEqual(3, result.Diagnostics.Count);
            StringAssert.Contains("broken", result.Diagnostics[0]);
            StringAssert.Contains("seed", result.Diagnostics[0]);
        }

        [Test]
        public void Pipeline_ReturnsFailure_WhenNothingCanBeBuilt()
        {
            RoomGenerationContext context = Context(6, 6, 1, null, 1, null, Door(0, 0, 3, Side.West, length: 2));
            RoomGenerationResult result = RoomGenerationPipeline.Generate(context, new ChamberGenerator(), 1);
            Assert.IsFalse(result.Succeeded);
            Assert.IsTrue(result.Diagnostics.Count >= 1);
        }

        [Test]
        public void Pipeline_RetrySeedsAreDerivedFromTheRoomSeedOnly()
        {
            RoomGenerationContext context = TwoDoors(8, 6, 1234);
            var first = RoomGenerationPipeline.Generate(context, new BrokenGenerator(), 2);
            var second = RoomGenerationPipeline.Generate(context, new BrokenGenerator(), 2);
            Assert.AreEqual(RoomPlanChecksum.Of(first.Room), RoomPlanChecksum.Of(second.Room));
            Assert.AreEqual(first.Context.Seed, second.Context.Seed);
        }

        // ---- Sweep ----

        [Test]
        public void Sweep_ManySeedsAndFootprints_NeverReturnsInvalidGeometry()
        {
            var failures = new List<string>();
            int built = 0;
            for (int seed = 0; seed < 25; seed++)
            {
                foreach ((int w, int h) in new[] { (3, 3), (4, 6), (6, 4), (8, 8), (14, 5) })
                {
                    var chamber = new ChamberSettings { Pillars = (PillarLayout)(seed % 3), Partitions = seed % 2 };
                    RoomGenerationContext context = Context(w, h, seed, chamber, 1, null, Door(0, 0, h / 2, Side.West), Door(1, w, h / 2, Side.East), Door(2, w / 2, 0, Side.South));
                    GeneratedRoom room = new ChamberGenerator().Generate(context);
                    if (room.IsFailure)
                    {
                        continue;
                    }

                    built++;
                    RoomPlanReport report = RoomPlanValidator.Validate(room, context);
                    if (!report.IsValid)
                    {
                        failures.Add($"{context}: {report}");
                    }
                }
            }

            Assert.IsEmpty(failures, string.Join("\n", failures));
            Assert.Greater(built, 50);
        }

        // ---- Checksums ----

        [Test]
        public void DungeonChecksum_DependsOnOrderAndContent()
        {
            Assert.AreNotEqual(RoomPlanChecksum.Combine(new[] { 1, 2 }), RoomPlanChecksum.Combine(new[] { 2, 1 }));
            Assert.AreEqual(RoomPlanChecksum.Combine(new[] { 1, 2 }), RoomPlanChecksum.Combine(new[] { 1, 2 }));
            Assert.AreNotEqual(0, RoomPlanChecksum.Combine(new int[0]));
        }
    }
}
