using System;
using System.Collections.Generic;
using UnityEngine;

namespace SimpleRPG.LevelGeneration
{
    /// <summary>
    /// Short checksums that let two machines confirm they are building the same dungeon.
    /// The server sends its roomset fingerprint with the seed, so a client with different room data refuses to generate,
    /// and the checksum of the server's finished layout, so a client that still ends up somewhere else notices.
    /// Both are stable across machines and runs: they depend only on the data hashed here, never on object identity or memory.
    /// A result of 0 is never returned, so 0 can mean "not provided".
    /// </summary>
    public static class LayoutFingerprint
    {
        /// <summary>
        /// Compares a fingerprint or checksum from the server with the local one.
        /// </summary>
        /// <param name="fromServer"> The value the server sent. Zero means it sent none, which is accepted.</param>
        /// <param name="local"> The value worked out on this machine.</param>
        /// <returns> A description of the mismatch, or null if the values agree or the server sent none.</returns>
        public static string Check(int fromServer, int local)
        {
            return fromServer == 0 || fromServer == local
                ? null
                : $"Server {fromServer:X8}, this machine {local:X8}.";
        }

        /// <summary>
        /// Fingerprints everything the generator reads from a roomset, in order, plus the generator's own version.
        /// </summary>
        public static int OfRoomset(IReadOnlyList<RoomDefinition> definitions)
        {
            var hash = new Hash();
            hash.Add(LayoutGenerator.AlgorithmVersion);
            hash.Add(definitions.Count);

            foreach (RoomDefinition room in definitions)
            {
                hash.Add(room.Index);
                hash.Add(room.Name);
                hash.Add(room.Size.x);
                hash.Add(room.Size.y);
                hash.Add(room.Weight);
                hash.Add(room.MaxCount);
                hash.Add(room.MinCount);
                hash.Add((int)room.Role);
                hash.Add(room.Height);
                hash.Add(room.ExtraCells.Count);
                foreach (Vector2Int cell in room.ExtraCells)
                {
                    hash.Add(cell.x);
                    hash.Add(cell.y);
                }

                hash.Add(room.Doors.Count);

                foreach (DoorDefinition door in room.Doors)
                {
                    hash.Add(door.Point.x);
                    hash.Add(door.Point.y);
                    hash.Add((int)door.Side);
                    hash.Add(door.LengthCells);
                    hash.Add(door.WidthCells);
                    hash.Add(door.TypeId);
                    hash.Add(door.Level);
                    hash.Add(door.FloorDelta);
                }
            }

            return hash.Result;
        }

        /// <summary>
        /// Fingerprints where every room stands and which doors are sealed.
        /// </summary>
        public static int OfLayout(Layout layout)
        {
            var hash = new Hash();
            hash.Add(layout.FloorCount);
            hash.Add(layout.Rooms.Count);

            foreach (PlacedRoom room in layout.Rooms)
            {
                hash.Add(room.Definition.Index);
                hash.Add(room.Position.x);
                hash.Add(room.Position.y);
                hash.Add(room.Floor);
                hash.Add(room.Turns);
            }

            hash.Add(layout.SealedDoors.Count);
            foreach (PlacedDoor door in layout.SealedDoors)
            {
                hash.Add(door.Room.Id);
                hash.Add(door.Index);
            }

            hash.Add(layout.Locks.Count);
            foreach (LockedDoor locked in layout.Locks)
            {
                hash.Add(locked.Door.Room.Id);
                hash.Add(locked.Door.Index);
                hash.Add(locked.KeyId);
                hash.Add(locked.KeyRoom.Id);
            }

            return hash.Result;
        }

        /// <summary>
        /// 32 bit FNV-1a over a stream of values.
        /// </summary>
        private struct Hash
        {
            private uint value;
            private bool started;

            public int Result
            {
                get
                {
                    int result = (int)(started ? value : 2166136261u);
                    return result == 0 ? 1 : result;
                }
            }

            public void Add(int number)
            {
                for (int shift = 0; shift < 32; shift += 8)
                {
                    AddByte((byte)(number >> shift));
                }
            }

            public void Add(float number)
            {
                Add(BitConverter.SingleToInt32Bits(number));
            }

            public void Add(string text)
            {
                Add(text.Length);
                foreach (char character in text)
                {
                    Add((int)character);
                }
            }

            private void AddByte(byte data)
            {
                if (!started)
                {
                    value = 2166136261u;
                    started = true;
                }

                unchecked
                {
                    value = (value ^ data) * 16777619u;
                }
            }
        }
    }
}
