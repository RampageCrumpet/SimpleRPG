using System;
using System.Collections.Generic;
using SimpleRPG.LevelGeneration;

namespace SimpleRPG.RoomGeneration
{
    public enum WallKind
    {
        Perimeter = 0,
        Partition = 1,
    }

    public enum PieceKind
    {
        Floor = 0,
        Ceiling = 1,
        Wall = 2,
        Pillar = 3,
        Stair = 4,
    }

    /// <summary>
    /// A wall: the rectangle it stands on (so its thickness is the short side) between two heights. Openings are cut out of it separately.
    /// </summary>
    public readonly struct WallSegment
    {
        public WallSegment(RectMm area, int bottomZ, int topZ, WallKind kind)
        {
            Area = area;
            BottomZ = bottomZ;
            TopZ = topZ;
            Kind = kind;
        }

        public RectMm Area { get; }
        public int BottomZ { get; }
        public int TopZ { get; }
        public WallKind Kind { get; }
        public int ThicknessMm => Math.Min(Area.Width, Area.Height);
        public BoxMm Box => new BoxMm(Area, BottomZ, TopZ);
    }

    /// <summary>
    /// A walkable surface at one elevation: its top is where feet go, and a slab of the given thickness lies under it.
    /// A landing is a floor surface inside a staircase.
    /// </summary>
    public readonly struct FloorSurface
    {
        public FloorSurface(RectMm area, int topZ, int thicknessMm, int level, bool isLanding = false)
        {
            Area = area;
            TopZ = topZ;
            ThicknessMm = thicknessMm;
            Level = level;
            IsLanding = isLanding;
        }

        public RectMm Area { get; }
        public int TopZ { get; }
        public int ThicknessMm { get; }

        /// <summary>
        /// The level this surface belongs to; for a landing, the level the staircase starts from.
        /// </summary>
        public int Level { get; }

        public bool IsLanding { get; }
        public BoxMm Box => new BoxMm(Area, TopZ - ThicknessMm, TopZ);
    }

    public readonly struct CeilingSlab
    {
        public CeilingSlab(RectMm area, int bottomZ, int thicknessMm)
        {
            Area = area;
            BottomZ = bottomZ;
            ThicknessMm = thicknessMm;
        }

        public RectMm Area { get; }
        public int BottomZ { get; }
        public int ThicknessMm { get; }
        public BoxMm Box => new BoxMm(Area, BottomZ, BottomZ + ThicknessMm);
    }

    /// <summary>
    /// A free-standing solid such as a pillar. Solid occupancy is kept apart from walls so each can be thick or thin independently.
    /// </summary>
    public readonly struct SolidVolume
    {
        public SolidVolume(BoxMm box, PieceKind kind = PieceKind.Pillar)
        {
            Box = box;
            Kind = kind;
        }

        public BoxMm Box { get; }
        public PieceKind Kind { get; }
    }

    /// <summary>
    /// The doorway of one connection, cut through whatever walls cross it.
    /// </summary>
    public readonly struct Opening
    {
        public Opening(int connectionIndex, Side side, BoxMm volume)
        {
            ConnectionIndex = connectionIndex;
            Side = side;
            Volume = volume;
        }

        public int ConnectionIndex { get; }

        /// <summary>
        /// The way the connection faces, which is the way out of the room.
        /// </summary>
        public Side Side { get; }

        public BoxMm Volume { get; }
    }

    /// <summary>
    /// What must stay clear for one connection: the doorway itself and the approach in front of it. Nothing may be built here.
    /// </summary>
    public readonly struct ProtectedRegion
    {
        public ProtectedRegion(int connectionIndex, int level, Side side, BoxMm doorway, BoxMm approach)
        {
            ConnectionIndex = connectionIndex;
            Level = level;
            Side = side;
            Doorway = doorway;
            Approach = approach;
        }

        public int ConnectionIndex { get; }
        public int Level { get; }
        public Side Side { get; }
        public BoxMm Doorway { get; }
        public BoxMm Approach { get; }
    }

    /// <summary>
    /// One straight run of steps. Step <c>i</c> of <c>Steps</c> covers the i-th share of the footprint along <see cref="Direction"/>
    /// and rises to <c>BaseZ + RiseMm * (i + 1) / Steps</c> (integer division), standing on solid down to <see cref="SupportZ"/>.
    /// </summary>
    public readonly struct StairFlight
    {
        public StairFlight(RectMm area, Side direction, int baseZ, int riseMm, int steps, int supportZ)
        {
            Area = area;
            Direction = direction;
            BaseZ = baseZ;
            RiseMm = riseMm;
            Steps = steps;
            SupportZ = supportZ;
        }

        public RectMm Area { get; }

        /// <summary>
        /// The way the flight climbs.
        /// </summary>
        public Side Direction { get; }

        /// <summary>
        /// The height of the surface the first step starts from.
        /// </summary>
        public int BaseZ { get; }

        public int RiseMm { get; }
        public int Steps { get; }
        public int SupportZ { get; }
        public int TopZ => BaseZ + RiseMm;

        /// <summary>
        /// The extent along the climb.
        /// </summary>
        public int RunMm => Direction == Side.East || Direction == Side.West ? Area.Width : Area.Height;

        /// <summary>
        /// The extent across the climb.
        /// </summary>
        public int WidthMm => Direction == Side.East || Direction == Side.West ? Area.Height : Area.Width;

        /// <summary>
        /// The top of step <paramref name="index"/>.
        /// </summary>
        public int StepTopZ(int index) => BaseZ + (int)((long)RiseMm * (index + 1) / Steps);

        /// <summary>
        /// The footprint of step <paramref name="index"/>.
        /// </summary>
        public RectMm StepArea(int index)
        {
            long run = RunMm;
            int from = (int)(run * index / Steps);
            int to = (int)(run * (index + 1) / Steps);
            switch (Direction)
            {
                case Side.East: return new RectMm(Area.MinX + from, Area.MinY, Area.MinX + to, Area.MaxY);
                case Side.West: return new RectMm(Area.MaxX - to, Area.MinY, Area.MaxX - from, Area.MaxY);
                case Side.North: return new RectMm(Area.MinX, Area.MinY + from, Area.MaxX, Area.MinY + to);
                default: return new RectMm(Area.MinX, Area.MaxY - to, Area.MaxX, Area.MaxY - from);
            }
        }

        /// <summary>
        /// The strip just before the first step (the walker's entry onto the flight), <paramref name="depth"/> deep.
        /// </summary>
        public RectMm EntryStrip(int depth)
        {
            switch (Direction)
            {
                case Side.East: return new RectMm(Area.MinX - depth, Area.MinY, Area.MinX, Area.MaxY);
                case Side.West: return new RectMm(Area.MaxX, Area.MinY, Area.MaxX + depth, Area.MaxY);
                case Side.North: return new RectMm(Area.MinX, Area.MinY - depth, Area.MaxX, Area.MinY);
                default: return new RectMm(Area.MinX, Area.MaxY, Area.MaxX, Area.MaxY + depth);
            }
        }

        /// <summary>
        /// The strip just past the last step, <paramref name="depth"/> deep.
        /// </summary>
        public RectMm ExitStrip(int depth)
        {
            switch (Direction)
            {
                case Side.East: return new RectMm(Area.MaxX, Area.MinY, Area.MaxX + depth, Area.MaxY);
                case Side.West: return new RectMm(Area.MinX - depth, Area.MinY, Area.MinX, Area.MaxY);
                case Side.North: return new RectMm(Area.MinX, Area.MaxY, Area.MaxX, Area.MaxY + depth);
                default: return new RectMm(Area.MinX, Area.MinY - depth, Area.MaxX, Area.MinY);
            }
        }
    }

    /// <summary>
    /// A staircase joining a level to the one above it: one or more flights, with landings where the climb turns back.
    /// The floor above has a well cut out wherever the run passes.
    /// </summary>
    public sealed class StairRun
    {
        public StairRun(int fromLevel, IReadOnlyList<StairFlight> flights, IReadOnlyList<FloorSurface> landings)
        {
            FromLevel = fromLevel;
            Flights = flights;
            Landings = landings;
        }

        public int FromLevel { get; }
        public int ToLevel => FromLevel + 1;
        public IReadOnlyList<StairFlight> Flights { get; }
        public IReadOnlyList<FloorSurface> Landings { get; }

        public StairFlight First => Flights[0];
        public StairFlight Last => Flights[Flights.Count - 1];

        /// <summary>
        /// The rectangles of the floor above that the run passes through.
        /// </summary>
        public IEnumerable<RectMm> Well()
        {
            foreach (StairFlight flight in Flights)
            {
                yield return flight.Area;
            }

            foreach (FloorSurface landing in Landings)
            {
                yield return landing.Area;
            }
        }

        /// <summary>
        /// The smallest rectangle containing the whole run.
        /// </summary>
        public RectMm Bounds()
        {
            RectMm bounds = default;
            foreach (RectMm area in Well())
            {
                bounds = bounds.Union(area);
            }

            return bounds;
        }
    }

    /// <summary>
    /// A piece of resolved structure, ready for meshing and for obstruction checks.
    /// </summary>
    public readonly struct StructuralPiece
    {
        public StructuralPiece(BoxMm box, PieceKind kind)
        {
            Box = box;
            Kind = kind;
        }

        public BoxMm Box { get; }
        public PieceKind Kind { get; }
    }

    /// <summary>
    /// A room's structural plan: plain data in millimetres, with no Unity objects and no grid. Each kind of structure has its own list, so
    /// a thin wall, a solid pillar and a walkable surface are never confused. Generators fill it; after that it is read only by convention.
    /// </summary>
    public sealed class GeneratedRoom
    {
        public GeneratedRoom(string generatorId, int seed, RectMm footprint, int minLevel, int maxLevel)
        {
            GeneratorId = generatorId;
            Seed = seed;
            Footprint = footprint;
            MinLevel = minLevel;
            MaxLevel = maxLevel;
        }

        /// <summary>
        /// A plan that could not be built. It has no geometry, and <see cref="Failure"/> says why.
        /// </summary>
        public static GeneratedRoom Failed(string generatorId, int seed, string reason)
        {
            return new GeneratedRoom(generatorId, seed, default, 0, 0) { Failure = reason ?? "unknown" };
        }

        public string GeneratorId { get; }
        public int Seed { get; }
        public RectMm Footprint { get; }

        /// <summary>
        /// The lowest and highest levels the plan builds floors for. A tall room whose doors are all on one level builds only that one.
        /// </summary>
        public int MinLevel { get; }

        public int MaxLevel { get; }

        public string Failure { get; private set; }
        public bool IsFailure => Failure != null;

        public List<FloorSurface> Floors { get; } = new List<FloorSurface>();
        public List<CeilingSlab> Ceilings { get; } = new List<CeilingSlab>();
        public List<WallSegment> Walls { get; } = new List<WallSegment>();
        public List<SolidVolume> Solids { get; } = new List<SolidVolume>();
        public List<StairRun> Stairs { get; } = new List<StairRun>();
        public List<Opening> Openings { get; } = new List<Opening>();
        public List<ProtectedRegion> Protected { get; } = new List<ProtectedRegion>();

        /// <summary>
        /// Every solid piece of the plan with openings cut out of the walls, in a fixed order: floors and landings, ceilings, walls, solids, steps.
        /// This is what is meshed, given colliders, and checked for obstruction, so the three can never disagree.
        /// </summary>
        public List<StructuralPiece> ResolvePieces()
        {
            var pieces = new List<StructuralPiece>();

            foreach (FloorSurface floor in Floors)
            {
                pieces.Add(new StructuralPiece(floor.Box, PieceKind.Floor));
            }

            foreach (StairRun run in Stairs)
            {
                foreach (FloorSurface landing in run.Landings)
                {
                    pieces.Add(new StructuralPiece(landing.Box, PieceKind.Floor));
                }
            }

            foreach (CeilingSlab ceiling in Ceilings)
            {
                pieces.Add(new StructuralPiece(ceiling.Box, PieceKind.Ceiling));
            }

            var cut = new List<BoxMm>();
            var next = new List<BoxMm>();
            foreach (WallSegment wall in Walls)
            {
                cut.Clear();
                cut.Add(wall.Box);
                foreach (Opening opening in Openings)
                {
                    next.Clear();
                    foreach (BoxMm part in cut)
                    {
                        part.Subtract(opening.Volume, next);
                    }

                    cut.Clear();
                    cut.AddRange(next);
                }

                foreach (BoxMm part in cut)
                {
                    pieces.Add(new StructuralPiece(part, PieceKind.Wall));
                }
            }

            foreach (SolidVolume solid in Solids)
            {
                pieces.Add(new StructuralPiece(solid.Box, solid.Kind));
            }

            foreach (StairRun run in Stairs)
            {
                foreach (StairFlight flight in run.Flights)
                {
                    for (int step = 0; step < flight.Steps; step++)
                    {
                        pieces.Add(new StructuralPiece(new BoxMm(flight.StepArea(step), flight.SupportZ, flight.StepTopZ(step)), PieceKind.Stair));
                    }
                }
            }

            return pieces;
        }
    }
}
