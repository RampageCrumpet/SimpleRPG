using Unity.Netcode;

namespace SimpleRPG.LevelGeneration
{
    /// <summary>
    /// Everything a machine needs, beyond its own copy of the <see cref="Roomset"/>, to rebuild the same dungeon as the server.
    /// The server picks these once and sends them to every client, including clients that join late.
    /// New generation inputs (floor count, allowed connection types, ...) belong here so the network protocol stays a single value.
    /// </summary>
    public struct LevelGenerationParameters : INetworkSerializable
    {
        /// <summary>
        /// The seed for the dungeon's random number generator.
        /// </summary>
        public int Seed;

        /// <summary>
        /// The minimum number of rooms the dungeon must contain.
        /// </summary>
        public int MinimumNumberOfRooms;

        /// <summary>
        /// The most rooms the dungeon may contain. Zero lets the generator choose from the minimum.
        /// </summary>
        public int MaximumNumberOfRooms;

        /// <summary>
        /// How many floors the dungeon may span. Zero or one gives a single floor.
        /// </summary>
        public int FloorCount;

        /// <summary>
        /// See <see cref="LayoutStyle.LoopRatio"/>. Zero is a real value (no loops), so a caller building these by hand must set it.
        /// </summary>
        public float LoopRatio;

        /// <summary>
        /// See <see cref="LayoutStyle.Branching"/>. Zero is a real value (long corridors), so a caller building these by hand must set it.
        /// </summary>
        public float Branching;

        /// <summary>
        /// See <see cref="LayoutStyle.LockedDoors"/>.
        /// </summary>
        public int LockedDoors;

        /// <summary>
        /// Fingerprint of the server's roomset and generator version (see <c>LayoutFingerprint.OfRoomset</c>). Filled in by the server;
        /// a client whose own fingerprint differs must not generate. Zero means the server did not provide one.
        /// </summary>
        public int RoomsetFingerprint;

        /// <summary>
        /// Checksum of the layout the server generated (see <c>LayoutFingerprint.OfLayout</c>). A client whose layout differs has desynced.
        /// Zero means the server did not provide one.
        /// </summary>
        public int LayoutChecksum;

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref Seed);
            serializer.SerializeValue(ref MinimumNumberOfRooms);
            serializer.SerializeValue(ref MaximumNumberOfRooms);
            serializer.SerializeValue(ref FloorCount);
            serializer.SerializeValue(ref LoopRatio);
            serializer.SerializeValue(ref Branching);
            serializer.SerializeValue(ref LockedDoors);
            serializer.SerializeValue(ref RoomsetFingerprint);
            serializer.SerializeValue(ref LayoutChecksum);
        }
    }
}
