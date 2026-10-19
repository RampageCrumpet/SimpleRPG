using System.Collections.Generic;

namespace LevelGeneration
{
    /// <summary>
    /// A finished dungeon plan: which rooms stand where, and which doors were closed with a wall.
    /// Plain data, so it can be generated, checked and compared without any Unity objects.
    /// Rooms are identified by <see cref="PlacedRoom.Id"/> and doors by their room and <see cref="PlacedDoor.Index"/>.
    /// </summary>
    public sealed class Layout
    {
        public Layout(int seed, int attempt, IReadOnlyList<PlacedRoom> rooms, IReadOnlyList<PlacedDoor> sealedDoors)
        {
            Seed = seed;
            Attempt = attempt;
            Rooms = rooms;
            SealedDoors = sealedDoors;
        }

        /// <summary>
        /// The seed the generator was given.
        /// </summary>
        public int Seed { get; }

        /// <summary>
        /// Which try produced this layout, starting at 0. Later tries use a seed derived from <see cref="Seed"/>.
        /// </summary>
        public int Attempt { get; }

        public IReadOnlyList<PlacedRoom> Rooms { get; }

        /// <summary>
        /// Doors that no room could be attached to, which the spawner closes with their sealing object.
        /// </summary>
        public IReadOnlyList<PlacedDoor> SealedDoors { get; }
    }
}
