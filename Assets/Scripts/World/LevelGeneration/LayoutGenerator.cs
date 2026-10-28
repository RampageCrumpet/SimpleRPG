using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace SimpleRPG.LevelGeneration
{
    /// <summary>
    /// What came out of <see cref="LayoutGenerator.Generate"/>.
    /// </summary>
    public sealed class GenerationResult
    {
        public GenerationResult(bool success, Layout layout, IReadOnlyList<string> rejectedAttempts)
        {
            Success = success;
            Layout = layout;
            RejectedAttempts = rejectedAttempts;
        }

        /// <summary>
        /// True if a layout passed <see cref="LayoutValidator"/> with at least the requested number of rooms.
        /// False means every attempt was rejected; <see cref="Layout"/> is then the last attempt with its open doors sealed.
        /// </summary>
        public bool Success { get; }

        public Layout Layout { get; }

        /// <summary>
        /// Why each rejected attempt was thrown away, in order. Empty when the first attempt was accepted.
        /// </summary>
        public IReadOnlyList<string> RejectedAttempts { get; }

        public override string ToString() => Success
            ? $"Dungeon seed {Layout.Seed}: {Layout.Rooms.Count} rooms on attempt {Layout.Attempt + 1}."
            : $"Dungeon seed {Layout.Seed} failed all {RejectedAttempts.Count} attempts: {string.Join(" | ", RejectedAttempts)}";
    }

    /// <summary>
    /// Works out where rooms go. Pure: it reads <see cref="RoomDefinition"/>s and a seed and produces a <see cref="Layout"/>,
    /// without creating any Unity objects, so the same seed gives the same layout on every machine.
    /// </summary>
    /// <remarks>
    /// Seeds are stable for a given <see cref="AlgorithmVersion"/>: the same seed, roomset and room counts give the same layout.
    /// Anything that changes layouts (placement rules, selection, how random numbers are drawn) must raise the version, which is part of
    /// the roomset fingerprint, so a client running different rules is caught instead of building a different dungeon.
    /// Random numbers come from three separate streams (rooms, doors, turns), each derived from the attempt seed.
    /// </remarks>
    public class LayoutGenerator
    {
        /// <summary>
        /// Raised whenever a change to the generator would give an existing seed a different layout.
        /// </summary>
        public const int AlgorithmVersion = 6;

        private const int RoomStream = 1;
        private const int DoorStream = 2;
        private const int TurnStream = 3;
        private const int LoopStream = 4;
        private const int LockStream = 5;

        /// <summary>
        /// The seed for one random stream, derived from the attempt seed so that streams do not share draws.
        /// </summary>
        public static int StreamSeed(int attemptSeed, int stream)
        {
            unchecked
            {
                uint x = (uint)attemptSeed + 0x9E3779B9u * (uint)(stream + 1);
                x ^= x >> 16;
                x *= 0x85EBCA6Bu;
                x ^= x >> 13;
                x *= 0xC2B2AE35u;
                x ^= x >> 16;
                return (int)x;
            }
        }

        /// <summary>
        /// How many random rooms (per room in the set) are tried against one open door before it is sealed.
        /// </summary>
        private const int TriesPerDoor = 8;

        private readonly IReadOnlyList<RoomDefinition> roomDefinitions;
        private readonly IRoomSelector selector;

        /// <summary>
        /// The seed we want to use for generating our level.
        /// </summary>
        public int Seed { get; }

        /// <summary>
        /// How many layouts to try, each from a seed derived from <see cref="Seed"/>, before giving up.
        /// </summary>
        public int MaxAttempts { get; set; } = 5;

        /// <summary>
        /// How loopy and how bushy the dungeon should be. The default leaves both to the natural behaviour of the generator.
        /// </summary>
        public LayoutStyle Style { get; set; } = new LayoutStyle();

        /// <param name="seed"> The seed we want to use for generating our level.</param>
        /// <param name="roomDefinitions"> The rooms we can draw from. At least one needs a weight above zero.</param>
        /// <param name="selector"> Decides which room to try next. Defaults to <see cref="WeightedRoomSelector"/>.</param>
        public LayoutGenerator(int seed, IReadOnlyList<RoomDefinition> roomDefinitions, IRoomSelector selector = null)
        {
            if (roomDefinitions == null || roomDefinitions.Count == 0)
            {
                throw new ArgumentException("At least one room definition is needed to generate a layout.", nameof(roomDefinitions));
            }

            if (!roomDefinitions.Any(room => room.Weight > 0))
            {
                throw new ArgumentException("Every room definition has a weight of zero, so no room could ever be picked.", nameof(roomDefinitions));
            }

            Seed = seed;
            this.roomDefinitions = roomDefinitions;
            this.selector = selector ?? new WeightedRoomSelector();
        }

        /// <summary>
        /// Generates a layout. Each failed attempt is replaced by one from a seed derived from (<see cref="Seed"/>, attempt),
        /// so every machine runs the same sequence and ends on the same layout.
        /// </summary>
        /// <param name="minimumNumberOfRooms"> The minimum number of rooms the layout will contain.</param>
        /// <param name="maximumNumberOfRooms"> The most rooms it may contain. Once reached, the doors still open are sealed.
        /// Zero or less uses <see cref="DefaultMaximumRooms"/>.</param>
        /// <param name="floorCount"> How many floors the dungeon may span. The start room is on the bottom floor (floor 0, at the world origin
        /// where players spawn), so vertical doors lead up from it; one floor gives the flat dungeon.</param>
        public GenerationResult Generate(int minimumNumberOfRooms, int maximumNumberOfRooms = 0, int floorCount = 1)
        {
            floorCount = Math.Max(1, floorCount);
            int maximum = maximumNumberOfRooms > 0 ? Math.Max(maximumNumberOfRooms, minimumNumberOfRooms) : DefaultMaximumRooms(minimumNumberOfRooms);
            var rejected = new List<string>();
            LayoutBuilder last = null;

            int attempts = Math.Max(1, MaxAttempts);

            for (int attempt = 0; attempt < attempts; attempt++)
            {
                var builder = new LayoutBuilder(roomDefinitions, selector, AttemptSeed(attempt), floorCount, Style ?? new LayoutStyle());
                last = builder;

                if (!builder.Build(minimumNumberOfRooms, maximum))
                {
                    rejected.Add($"attempt {attempt + 1}: {builder.FailureReason}");
                    continue;
                }

                Layout layout = builder.ToLayout(Seed, attempt, floorCount);
                List<LayoutViolation> violations = LayoutValidator.Validate(layout);
                if (violations.Count == 0)
                {
                    return new GenerationResult(true, layout, rejected);
                }

                rejected.Add($"attempt {attempt + 1}: {string.Join("; ", violations)}");
            }

            // Out of attempts. Keep the last layout, closing whatever is still open, so a caller can still play it.
            last.SealAllOpenDoors();
            return new GenerationResult(false, last.ToLayout(Seed, attempts - 1, floorCount), rejected);
        }

        /// <summary>
        /// The room limit used when none is given: the minimum plus a small slack, a tenth of the minimum and never less than two.
        /// Rooms are added freely only up to the minimum; the slack lets a room that closes several doors at once finish a branch.
        /// Whatever is still open at the limit is sealed. Pass a maximum equal to the minimum for an exact room count.
        /// </summary>
        public static int DefaultMaximumRooms(int minimumNumberOfRooms)
        {
            return minimumNumberOfRooms + Math.Max(2, minimumNumberOfRooms / 10);
        }

        /// <summary>
        /// The seed used for one attempt. The first attempt uses the generator's seed unchanged.
        /// </summary>
        private int AttemptSeed(int attempt)
        {
            return attempt == 0 ? Seed : unchecked(Seed * 1000003 + attempt);
        }

        /// <summary>
        /// The state of one attempt: what has been placed, which cells it covers and which doors are still open.
        /// </summary>
        private sealed class LayoutBuilder
        {
            private readonly IReadOnlyList<RoomDefinition> definitions;

            // Separate streams, so a change to how one decision draws numbers does not shift the others.
            private readonly System.Random roomRandom;
            private readonly System.Random doorRandom;
            private readonly System.Random turnRandom;
            private readonly System.Random loopRandom;
            private readonly System.Random lockRandom;
            private readonly List<LockedDoor> locks = new List<LockedDoor>();
            private readonly LayoutStyle style;

            // How far apart two open doors can be and still both be joined by one room.
            private readonly int loopReach;
            private bool loopSearchExhausted;
            private readonly Dictionary<Vector3Int, PlacedRoom> grid = new Dictionary<Vector3Int, PlacedRoom>();
            private readonly List<PlacedRoom> rooms = new List<PlacedRoom>();

            // Cells between two joined rooms that neither covers. A door longer than the rooms' own reach leaves a gap that must stay clear.
            private readonly HashSet<Vector3Int> reserved = new HashSet<Vector3Int>();
            private readonly int floorCount;

            // Open doors in the order they opened, because the random pick indexes into it. The lookup finds the door a new door would join.
            private readonly List<PlacedDoor> openDoors = new List<PlacedDoor>();
            private readonly Dictionary<(Vector2Int point, Side side, int floor), PlacedDoor> openLookup = new Dictionary<(Vector2Int, Side, int), PlacedDoor>();
            private readonly List<PlacedDoor> sealedDoors = new List<PlacedDoor>();

            private readonly IRoomSelector selector;
            private readonly Dictionary<RoomDefinition, int> placedCounts = new Dictionary<RoomDefinition, int>();

            public LayoutBuilder(IReadOnlyList<RoomDefinition> definitions, IRoomSelector selector, int attemptSeed, int floorCount, LayoutStyle style)
            {
                this.style = style;
                loopReach = definitions.Max(definition => Math.Max(definition.Size.x, definition.Size.y) + definition.Doors.Select(door => door.LengthCells).DefaultIfEmpty(0).Max()) + 2;
                this.floorCount = floorCount;
                this.definitions = definitions;
                this.selector = selector;
                roomRandom = new System.Random(StreamSeed(attemptSeed, RoomStream));
                doorRandom = new System.Random(StreamSeed(attemptSeed, DoorStream));
                turnRandom = new System.Random(StreamSeed(attemptSeed, TurnStream));
                loopRandom = new System.Random(StreamSeed(attemptSeed, LoopStream));
                lockRandom = new System.Random(StreamSeed(attemptSeed, LockStream));
            }

            /// <summary>
            /// Why <see cref="Build"/> returned false.
            /// </summary>
            public string FailureReason { get; private set; }

            public Layout ToLayout(int seed, int attempt, int floors)
            {
                return new Layout(seed, attempt, rooms.ToList(), sealedDoors.ToList(), floors, locks.ToList());
            }

            public void SealAllOpenDoors()
            {
                sealedDoors.AddRange(openDoors);
                openDoors.Clear();
                openLookup.Clear();
            }

            /// <summary>
            /// Places rooms until the minimum is met and every door is joined or sealed.
            /// </summary>
            /// <returns> False if every door was sealed before the minimum room count was reached.</returns>
            public bool Build(int minimumNumberOfRooms, int maximumNumberOfRooms)
            {
                // Place a starting room to seed our dungeon.
                // A room taller than the whole dungeon can never stand anywhere, so it cannot begin it either.
                RoomDefinition start = selector.SelectStart(roomRandom, definitions.Where(definition => definition.Height <= floorCount).ToList());
                if (start == null)
                {
                    FailureReason = "no room could begin the dungeon";
                    return false;
                }

                Place(new PlacedRoom(0, start, Vector2Int.zero, 0, 0));

                // Every pass either places a room or counts a miss against a door, and a door with enough misses is sealed,
                // so this always finishes.
                var misses = new Dictionary<PlacedDoor, int>();

                while (rooms.Count < minimumNumberOfRooms || openDoors.Count > 0)
                {
                    // Nothing left to build off of, so the minimum can never be reached.
                    if (openDoors.Count == 0)
                    {
                        FailureReason = $"ran out of doors to build on before reaching {minimumNumberOfRooms} rooms";
                        return false;
                    }

                    // Big enough: wall off whatever is still open rather than growing further.
                    if (rooms.Count >= maximumNumberOfRooms)
                    {
                        SealAllOpenDoors();
                        break;
                    }

                    // Above the natural loop ratio, some steps go looking for a room that joins several open doors instead of growing.
                    if (style.LoopRatio > LayoutStyle.Natural && loopRandom.NextDouble() < (style.LoopRatio - LayoutStyle.Natural) * 2f && TrySeekLoop(minimumNumberOfRooms))
                    {
                        continue;
                    }

                    PlacedDoor target = openDoors[doorRandom.Next(openDoors.Count)];
                    var context = new SelectionContext(rooms.Count, minimumNumberOfRooms, openDoors.Count, placedCounts, target, floorCount, UnmetRequired(), style.JunctionFactor);
                    RoomDefinition definition = selector.Select(roomRandom, definitions, context);

                    if (definition != null && TryPlaceAgainst(target, definition, minimumNumberOfRooms))
                    {
                        continue;
                    }

                    // Nothing we tried fits here. After enough misses the door is closed with its wall rather than retried forever.
                    misses.TryGetValue(target, out int count);
                    count++;

                    if (count >= definitions.Count * TriesPerDoor)
                    {
                        Remove(target);
                        sealedDoors.Add(target);
                        misses.Remove(target);
                    }
                    else
                    {
                        misses[target] = count;
                    }
                }

                List<RoomDefinition> owed = UnmetRequired();
                if (owed.Count > 0)
                {
                    FailureReason = $"required room(s) never placed: {string.Join(", ", owed.Select(room => room.Name).Distinct())}";
                    return false;
                }

                if (!PlaceExit())
                {
                    FailureReason = "no dead end could be swapped for an exit room";
                    return false;
                }

                PlaceLocks();
                return true;
            }

            /// <summary>
            /// Locks doors, one key each, until the style's count is reached or no more can be placed safely. A lock goes on a join
            /// whose removal cuts part of the dungeon off, and its key in a room on the start's side of that cut. The first lock cuts the
            /// exit off if there is one. Every placement is checked by <see cref="LockSolver.Reachable"/>, so a player can always reach every room.
            /// </summary>
            private void PlaceLocks()
            {
                if (style.LockedDoors <= 0)
                {
                    return;
                }

                List<(PlacedDoor a, PlacedDoor b)> joins = LockSolver.Joins(rooms, sealedDoors);
                PlacedRoom exit = rooms.FirstOrDefault(room => (room.Definition.Role & RoomRole.Exit) != 0);

                for (int keyId = 1; keyId <= style.LockedDoors; keyId++)
                {
                    List<(PlacedDoor a, PlacedDoor b)> candidates = joins
                        .Where(join => !locks.Any(locked => locked.Door == join.a))
                        .OrderBy(_ => lockRandom.Next())
                        .ToList();

                    // The first lock stands between the player and the exit, when there is a join that can.
                    if (keyId == 1 && exit != null)
                    {
                        List<(PlacedDoor a, PlacedDoor b)> gating = candidates.Where(join => !LockSolver.Connected(rooms, joins, join).Contains(exit)).ToList();
                        if (gating.Count > 0)
                        {
                            candidates = gating;
                        }
                    }

                    if (!TryLock(keyId, candidates, joins, exit))
                    {
                        return;
                    }
                }
            }

            private bool TryLock(int keyId, List<(PlacedDoor a, PlacedDoor b)> candidates, List<(PlacedDoor a, PlacedDoor b)> joins, PlacedRoom exit)
            {
                foreach ((PlacedDoor a, PlacedDoor b) join in candidates)
                {
                    HashSet<PlacedRoom> startSide = LockSolver.Connected(rooms, joins, join);
                    if (startSide.Count == rooms.Count)
                    {
                        // Not a bridge: another route leads round, so a lock here would guard nothing.
                        continue;
                    }

                    List<PlacedRoom> keyRooms = rooms
                        .Where(room => startSide.Contains(room) && room != rooms[0] && room != exit && !locks.Any(locked => locked.KeyRoom == room))
                        .OrderBy(_ => lockRandom.Next())
                        .ToList();

                    foreach (PlacedRoom keyRoom in keyRooms)
                    {
                        var tentative = new List<LockedDoor>(locks) { new LockedDoor(join.a, join.b, keyId, keyRoom) };
                        if (LockSolver.Reachable(rooms, joins, tentative).Count == rooms.Count)
                        {
                            locks.Add(tentative[tentative.Count - 1]);
                            return true;
                        }
                    }
                }

                return false;
            }

            /// <summary>
            /// Looks through every open door for a room that would join at least two of them, and places the first it finds.
            /// </summary>
            private bool TrySeekLoop(int minimumNumberOfRooms)
            {
                // A loop joins two open doors, so with fewer there is nothing to look for. A search that found nothing
                // finds nothing again until a room is placed or a door closes.
                if (openDoors.Count < 2 || loopSearchExhausted)
                {
                    return false;
                }

                int offset = loopRandom.Next(definitions.Count);

                foreach (PlacedDoor target in openDoors.ToList())
                {
                    // The second door the room joins has to be within a room's reach of where the target ends, or it cannot be it.
                    if (!openDoors.Any(other => other != target && Math.Abs(other.Point.x - target.Far.x) + Math.Abs(other.Point.y - target.Far.y) <= loopReach))
                    {
                        continue;
                    }

                    for (int i = 0; i < definitions.Count; i++)
                    {
                        RoomDefinition definition = definitions[(i + offset) % definitions.Count];
                        if (definition.Weight <= 0 || (definition.Role & (RoomRole.Start | RoomRole.Exit)) != 0)
                        {
                            continue;
                        }

                        if (definition.MaxCount > 0 && placedCounts.TryGetValue(definition, out int placed) && placed >= definition.MaxCount)
                        {
                            continue;
                        }

                        if (TryPlaceAgainst(target, definition, minimumNumberOfRooms, requireLoop: true))
                        {
                            return true;
                        }
                    }
                }

                loopSearchExhausted = true;
                return false;
            }

            /// <summary>
            /// One entry for every room still owed to meet a <see cref="RoomDefinition.MinCount"/>.
            /// </summary>
            private List<RoomDefinition> UnmetRequired()
            {
                var owed = new List<RoomDefinition>();
                foreach (RoomDefinition definition in definitions.Where(definition => definition.MinCount > 0 && definition.Weight > 0))
                {
                    placedCounts.TryGetValue(definition, out int placed);
                    for (int i = placed; i < definition.MinCount; i++)
                    {
                        owed.Add(definition);
                    }
                }

                return owed;
            }

            /// <summary>
            /// Swaps an exit room in for the dead end farthest from the start that it fits. Does nothing if the set has no exit room.
            /// Dead ends are tried farthest first, and the exit rooms in a random order, so every machine makes the same swap.
            /// </summary>
            /// <returns> False if the set has an exit room and none could be placed.</returns>
            private bool PlaceExit()
            {
                List<RoomDefinition> exits = definitions
                    .Where(definition => (definition.Role & RoomRole.Exit) != 0 && definition.Weight > 0 && definition.Doors.Count == 1)
                    .OrderBy(_ => roomRandom.Next())
                    .ToList();

                if (exits.Count == 0)
                {
                    return true;
                }

                Dictionary<PlacedRoom, int> distances = DistancesFromStart();
                List<PlacedRoom> deadEnds = rooms
                    .Skip(1)
                    .Where(room => room.Doors.Count == 1 && !sealedDoors.Contains(room.Doors[0]) && distances.ContainsKey(room))
                    .OrderByDescending(room => distances[room])
                    .ThenBy(room => room.Id)
                    .ToList();

                foreach (PlacedRoom deadEnd in deadEnds)
                {
                    foreach (RoomDefinition exit in exits)
                    {
                        if (TryReplace(deadEnd, exit))
                        {
                            return true;
                        }
                    }
                }

                return false;
            }

            /// <summary>
            /// Takes a dead end out and tries to stand another room where it was, putting the dead end back if that fails.
            /// </summary>
            private bool TryReplace(PlacedRoom deadEnd, RoomDefinition replacement)
            {
                PlacedDoor door = deadEnd.Doors[0];
                PlacedDoor mate = rooms.Where(room => room != deadEnd).SelectMany(room => room.Doors).FirstOrDefault(door.Mates);
                if (mate == null)
                {
                    return false;
                }

                int index = rooms.IndexOf(deadEnd);
                Unplace(deadEnd, mate);

                if (TryPlaceAgainst(mate, replacement, int.MaxValue, deadEnd.Id, index))
                {
                    return true;
                }

                Place(deadEnd, index);
                return false;
            }

            /// <summary>
            /// How many doors apart each room is from the start, following joined doors only.
            /// </summary>
            private Dictionary<PlacedRoom, int> DistancesFromStart()
            {
                var byPosition = new Dictionary<(Vector2Int, Side, int), PlacedDoor>();
                foreach (PlacedDoor door in rooms.SelectMany(room => room.Doors).Where(door => !sealedDoors.Contains(door)))
                {
                    byPosition[(door.Point, door.Side, door.Floor)] = door;
                }

                var distances = new Dictionary<PlacedRoom, int> { [rooms[0]] = 0 };
                var queue = new Queue<PlacedRoom>();
                queue.Enqueue(rooms[0]);

                while (queue.Count > 0)
                {
                    PlacedRoom room = queue.Dequeue();
                    foreach (PlacedDoor door in room.Doors)
                    {
                        if (byPosition.TryGetValue((door.Far, GridMath.Opposite(door.Side), door.FarFloor), out PlacedDoor other)
                            && door.Mates(other) && !distances.ContainsKey(other.Room))
                        {
                            distances[other.Room] = distances[room] + 1;
                            queue.Enqueue(other.Room);
                        }
                    }
                }

                return distances;
            }

            /// <summary>
            /// Takes a joined room out again: its cells and the passage it shared with its mate are freed and the mate opens.
            /// </summary>
            private void Unplace(PlacedRoom room, PlacedDoor mate)
            {
                foreach (Vector3Int cell in room.Cells3D())
                {
                    grid.Remove(cell);
                }

                foreach (PlacedDoor door in room.Doors.Where(door => door.Mates(mate)))
                {
                    foreach ((int slice, Vector3Int cell) in door.PassageCells3D())
                    {
                        if (slice < door.LengthCells && !door.Room.Contains(cell) && !mate.Room.Contains(cell))
                        {
                            reserved.Remove(cell);
                        }
                    }
                }

                openDoors.Add(mate);
                openLookup[(mate.Point, mate.Side, mate.Floor)] = mate;

                rooms.Remove(room);
                placedCounts[room.Definition]--;
            }

            /// <summary>
            /// Tries every way of standing a room of this kind against an open door, in a random order of turns, and places the first that works.
            /// </summary>
            /// <param name="replaceId"> The id to give the room when it takes another room's place, or -1 to add it as a new room.</param>
            /// <param name="replaceIndex"> Where in the room list it takes its place.</param>
            private bool TryPlaceAgainst(PlacedDoor target, RoomDefinition definition, int minimumNumberOfRooms, int replaceId = -1, int replaceIndex = -1, bool requireLoop = false)
            {
                int[] turnOrder = Enumerable.Range(0, 4).OrderBy(_ => turnRandom.Next()).ToArray();

                foreach (int turns in turnOrder)
                {
                    foreach (DoorDefinition door in definition.Doors)
                    {
                        if (GridMath.Rotate(door.Side, turns) != GridMath.Opposite(target.Side)
                            || door.LengthCells != target.LengthCells
                            || door.TypeId != target.TypeId
                            || door.FloorDelta != -target.FloorDelta)
                        {
                            continue;
                        }

                        // The new door has to stand where the open door ends.
                        Vector2Int position = target.Far - GridMath.RotatePoint(door.Point, turns);
                        var candidate = new PlacedRoom(replaceId >= 0 ? replaceId : rooms.Count, definition, position, turns, target.FarFloor - door.Level);

                        // Looking for a loop: skip the full check unless the room would join a second open door.
                        if (requireLoop && candidate.Doors.Count(candidateDoor => FindOpenMate(candidateDoor) != null) < 2)
                        {
                            continue;
                        }

                        if (!Fits(candidate, out int openAfter, out int joined))
                        {
                            continue;
                        }

                        // A room that joins two or more open doors closes a loop. Below the natural ratio some of them are refused.
                        if (requireLoop && joined < 2)
                        {
                            continue;
                        }

                        if (joined >= 2 && !requireLoop && replaceId < 0 && style.LoopRatio < LayoutStyle.Natural
                            && loopRandom.NextDouble() >= style.LoopRatio / LayoutStyle.Natural)
                        {
                            continue;
                        }

                        // Before the minimum, keep at least one door open to build on. After it, only accept rooms that close more than they open.
                        bool wanted = replaceId >= 0 || (rooms.Count < minimumNumberOfRooms ? openAfter != 0 : openAfter < openDoors.Count);
                        if (!wanted)
                        {
                            continue;
                        }

                        Place(candidate, replaceIndex);
                        return true;
                    }
                }

                return false;
            }

            /// <summary>
            /// Checks that a room can stand where it is: it overlaps nothing, none of its doors open onto a wall, and it does not wall off an open door.
            /// </summary>
            /// <param name="openAfter"> How many doors would be open once the room is placed.</param>
            /// <param name="joined"> How many of the room's doors would join a door that is open now.</param>
            private bool Fits(PlacedRoom candidate, out int openAfter, out int joined)
            {
                openAfter = 0;
                joined = 0;

                if (candidate.Floor < 0 || candidate.TopFloor > floorCount)
                {
                    return false;
                }

                foreach (Vector3Int cell in candidate.Cells3D())
                {
                    if (grid.ContainsKey(cell) || reserved.Contains(cell))
                    {
                        return false;
                    }
                }

                foreach (PlacedDoor door in candidate.Doors)
                {
                    if (FindOpenMate(door) != null)
                    {
                        joined++;
                        continue;
                    }

                    // No door to join, so nothing may stand in the way beyond it and it may not open onto its own room.
                    foreach ((int slice, Vector3Int cell) in door.PassageCells3D())
                    {
                        if (cell.z < 0 || cell.z >= floorCount)
                        {
                            return false;
                        }

                        bool ownRoom = candidate.Contains(cell);
                        if (ownRoom ? slice == door.LengthCells : grid.ContainsKey(cell) || reserved.Contains(cell))
                        {
                            return false;
                        }
                    }
                }

                // An open door must not end up facing into the new room unless the room has the door that joins it.
                foreach (PlacedDoor open in openDoors)
                {
                    bool facesRoom = open.PassageCells3D().Any(passage => candidate.Contains(passage.cell));
                    if (facesRoom && !candidate.Doors.Any(open.Mates))
                    {
                        return false;
                    }
                }

                openAfter = openDoors.Count + (candidate.Doors.Count - joined) - joined;
                return true;
            }

            /// <summary>
            /// The open door that a door would join if its room were placed, or null.
            /// </summary>
            private PlacedDoor FindOpenMate(PlacedDoor door)
            {
                if (openLookup.TryGetValue((door.Far, GridMath.Opposite(door.Side), door.FarFloor), out PlacedDoor open)
                    && open.LengthCells == door.LengthCells
                    && open.FloorDelta == -door.FloorDelta
                    && open.TypeId == door.TypeId
                    && open.Room != door.Room)
                {
                    return open;
                }

                return null;
            }

            private void Place(PlacedRoom room, int index = -1)
            {
                loopSearchExhausted = false;
                foreach (Vector3Int cell in room.Cells3D())
                {
                    grid.Add(cell, room);
                }

                foreach (PlacedDoor door in room.Doors)
                {
                    PlacedDoor mate = FindOpenMate(door);
                    if (mate != null)
                    {
                        Remove(mate);
                        ReservePassage(door, mate);
                    }
                    else
                    {
                        openDoors.Add(door);
                        openLookup[(door.Point, door.Side, door.Floor)] = door;
                    }
                }

                if (index >= 0)
                {
                    rooms.Insert(index, room);
                }
                else
                {
                    rooms.Add(room);
                }

                placedCounts.TryGetValue(room.Definition, out int count);
                placedCounts[room.Definition] = count + 1;
            }

            /// <summary>
            /// Keeps the cells a joined pair of doors passes through clear of other rooms, wherever neither of the two rooms covers them.
            /// </summary>
            private void ReservePassage(PlacedDoor door, PlacedDoor mate)
            {
                foreach ((int slice, Vector3Int cell) in door.PassageCells3D())
                {
                    if (slice < door.LengthCells && !door.Room.Contains(cell) && !mate.Room.Contains(cell))
                    {
                        reserved.Add(cell);
                    }
                }
            }

            private void Remove(PlacedDoor door)
            {
                loopSearchExhausted = false;
                openDoors.Remove(door);
                openLookup.Remove((door.Point, door.Side, door.Floor));
            }
        }
    }
}
