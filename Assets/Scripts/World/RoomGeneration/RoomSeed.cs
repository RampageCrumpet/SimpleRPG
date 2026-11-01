using System;

namespace SimpleRPG.RoomGeneration
{
    /// <summary>
    /// Explicit, platform independent seed derivation and random numbers. Nothing here depends on <c>string.GetHashCode</c>,
    /// <c>System.Random</c> or the order rooms are generated in.
    /// </summary>
    public static class RoomSeed
    {
        /// <summary>
        /// Raised when anything in this namespace changes in a way that alters generated geometry for an existing seed
        /// (shared validation, stair planning, wall layout). Part of every room seed.
        /// </summary>
        public const int GeometryVersion = 1;

        /// <summary>
        /// The seed of one room: a function of the dungeon, the layout attempt that produced it, the room's identity in that layout,
        /// the generator and the geometry version, and of nothing else.
        /// </summary>
        public static int ForRoom(int dungeonSeed, int layoutAttempt, int placedRoomId, int definitionIndex, string generatorId, int generatorVersion)
        {
            ulong hash = 14695981039346656037UL;
            hash = Add(hash, dungeonSeed);
            hash = Add(hash, layoutAttempt);
            hash = Add(hash, placedRoomId);
            hash = Add(hash, definitionIndex);
            hash = Add(hash, generatorId ?? string.Empty);
            hash = Add(hash, generatorVersion);
            hash = Add(hash, GeometryVersion);
            return Finish(hash);
        }

        /// <summary>
        /// The seed of the n-th retry (n of 0 is the original). Derived from the room seed alone.
        /// </summary>
        public static int ForRetry(int roomSeed, int retry)
        {
            if (retry == 0)
            {
                return roomSeed;
            }

            ulong hash = 14695981039346656037UL;
            hash = Add(hash, roomSeed);
            hash = Add(hash, retry);
            return Finish(hash);
        }

        /// <summary>
        /// An independent sub-stream seed for one purpose inside a generator, so adding a decision in one place never shifts another.
        /// </summary>
        public static int Stream(int seed, int stream)
        {
            ulong hash = 14695981039346656037UL;
            hash = Add(hash, seed);
            hash = Add(hash, 0x5EED + stream);
            return Finish(hash);
        }

        internal static ulong Add(ulong hash, int value)
        {
            unchecked
            {
                for (int shift = 0; shift < 32; shift += 8)
                {
                    hash = (hash ^ (byte)(value >> shift)) * 1099511628211UL;
                }
            }

            return hash;
        }

        internal static ulong Add(ulong hash, string text)
        {
            hash = Add(hash, text.Length);
            foreach (char character in text)
            {
                hash = Add(hash, (int)character);
            }

            return hash;
        }

        private static int Finish(ulong hash)
        {
            unchecked
            {
                hash ^= hash >> 33;
                hash *= 0xFF51AFD7ED558CCDUL;
                hash ^= hash >> 33;
                hash *= 0xC4CEB9FE1A85EC53UL;
                hash ^= hash >> 33;
                return (int)(uint)(hash >> 16);
            }
        }
    }

    /// <summary>
    /// A small seeded generator (SplitMix64) with only integer outputs, identical on every platform.
    /// </summary>
    public struct RoomRandom
    {
        private ulong state;

        public RoomRandom(int seed)
        {
            state = (ulong)(uint)seed * 0x9E3779B97F4A7C15UL + 0x1234567UL;
        }

        public ulong NextUInt64()
        {
            unchecked
            {
                state += 0x9E3779B97F4A7C15UL;
                ulong z = state;
                z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
                z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
                return z ^ (z >> 31);
            }
        }

        /// <summary>
        /// A number from 0 up to but not including <paramref name="exclusiveMax"/>. Zero if the bound is not positive.
        /// </summary>
        public int Next(int exclusiveMax)
        {
            return exclusiveMax <= 0 ? 0 : (int)(NextUInt64() % (ulong)exclusiveMax);
        }

        /// <summary>
        /// A number from <paramref name="min"/> up to and including <paramref name="max"/>.
        /// </summary>
        public int Range(int min, int max)
        {
            return max <= min ? min : min + Next(max - min + 1);
        }

        public bool Chance(int percent)
        {
            return Next(100) < percent;
        }

        /// <summary>
        /// Fisher-Yates, in place.
        /// </summary>
        public void Shuffle<T>(System.Collections.Generic.IList<T> list)
        {
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = Next(i + 1);
                T swap = list[i];
                list[i] = list[j];
                list[j] = swap;
            }
        }
    }
}
