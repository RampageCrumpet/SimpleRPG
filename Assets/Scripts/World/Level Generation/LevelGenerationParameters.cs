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

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref Seed);
            serializer.SerializeValue(ref MinimumNumberOfRooms);
        }
    }
}
