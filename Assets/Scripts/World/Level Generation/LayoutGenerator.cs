using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace LevelGeneration
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
        public const int AlgorithmVersion = 4;

        private const int RoomStream = 1;
        private const int DoorStream = 2;
        private const int TurnStream = 3;

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
                var builder = new LayoutBuilder(roomDefinitions, selector, AttemptSeed(attempt), floorCount);
                last = builder;

                if (!builder.Build(minimumNumberOfRooms, maximum))
                {
                    rejected.Add($"attempt {attempt + 1}: ran out of doors to build on before reaching {minimumNumberOfRooms} rooms");
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
        /// The room limit used when none is given: twice the minimum, and never less than four more.
        /// Rooms are added freely only up to the minimum, but rooms that close several doors at once can still add a few more.
        /// </summary>
        public static int DefaultMaximumRooms(int minimumNumberOfRooms)
        {
            return Math.Max(minimumNumberOfRooms + 4, minimumNumberOfRooms * 2);
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

            public LayoutBuilder(IReadOnlyList<RoomDefinition> definitions, IRoomSelector selector, int attemptSeed, int floorCount)
            {
                this.floorCount = floorCount;
                this.definitions = definitions;
                this.selector = selector;
                roomRandom = new System.Random(StreamSeed(attemptSeed, RoomStream));
                doorRandom = new System.Random(StreamSeed(attemptSeed, DoorStream));
                turnRandom = new System.Random(StreamSeed(attemptSeed, TurnStream));
            }

            public Layout ToLayout(int seed, int attempt, int floors)
            {
                return new Layout(seed, attempt, rooms.ToList(), sealedDoors.ToList(), floors);
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
                        return false;
                    }

                    // Big enough: wall off whatever is still open rather than growing further.
                    if (rooms.Count >= maximumNumberOfRooms)
                    {
                        SealAllOpenDoors();
                        break;
                    }

                    PlacedDoor target = openDoors[doorRandom.Next(openDoors.Count)];
                    var context = new SelectionContext(rooms.Count, minimumNumberOfRooms, openDoors.Count, placedCounts, target, floorCount);
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

                return true;
            }

            /// <summary>
            /// Tries every way of standing a room of this kind against an open door, in a random order of turns, and places the first that works.
            /// </summary>
            private bool TryPlaceAgainst(PlacedDoor target, RoomDefinition definition, int minimumNumberOfRooms)
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
                        var candidate = new PlacedRoom(rooms.Count, definition, position, turns, target.FarFloor - door.Level);

                        if (!Fits(candidate, out int openAfter))
                        {
                            continue;
                        }

                        // Before the minimum, keep at least one door open to build on. After it, only accept rooms that close more than they open.
                        bool wanted = rooms.Count < minimumNumberOfRooms ? openAfter != 0 : openAfter < openDoors.Count;
                        if (!wanted)
                        {
                            continue;
                        }

                        Place(candidate);
                        return true;
                    }
                }

                return false;
            }

            /// <summary>
            /// Checks that a room can stand where it is: it overlaps nothing, none of its doors open onto a wall, and it does not wall off an open door.
            /// </summary>
            /// <param name="openAfter"> How many doors would be open once the room is placed.</param>
            private bool Fits(PlacedRoom candidate, out int openAfter)
            {
                openAfter = 0;

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

                int joined = 0;
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

            private void Place(PlacedRoom room)
            {
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

                rooms.Add(room);
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
                openDoors.Remove(door);
                openLookup.Remove((door.Point, door.Side, door.Floor));
            }
        }
    }
}
