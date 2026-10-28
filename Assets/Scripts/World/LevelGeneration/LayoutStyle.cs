using System;

namespace SimpleRPG.LevelGeneration
{
    /// <summary>
    /// How a dungeon is shaped, as opposed to how big it is. Both dials run from 0 to 1 and 0.5 is the generator's natural behaviour.
    /// </summary>
    public sealed class LayoutStyle
    {
        public const float Natural = 0.5f;

        private float loopRatio = Natural;
        private float branching = Natural;
        private int lockedDoors;

        /// <summary>
        /// How many loops (ways to walk in a circle) the dungeon has. 0 gives a tree with none, 0.5 leaves loops to chance,
        /// and above that the generator goes looking for places to close one, more often the closer to 1.
        /// </summary>
        public float LoopRatio
        {
            get => loopRatio;
            set => loopRatio = Math.Min(1f, Math.Max(0f, value));
        }

        /// <summary>
        /// How bushy the dungeon is. 0 favours long winding corridors, 0.5 leaves the room weights as they are,
        /// and 1 favours junctions and dead ends.
        /// </summary>
        public float Branching
        {
            get => branching;
            set => branching = Math.Min(1f, Math.Max(0f, value));
        }

        /// <summary>
        /// How many locked doors to put in the dungeon. Each has its own key in a room the player can reach first.
        /// The generator places as many as it can without shutting anyone in, which may be fewer than asked.
        /// </summary>
        public int LockedDoors
        {
            get => lockedDoors;
            set => lockedDoors = Math.Max(0, value);
        }

        /// <summary>
        /// How much more (or less) likely junction rooms are to be picked: a quarter at 0, unchanged at 0.5 and four times at 1.
        /// </summary>
        public float JunctionFactor => (float)Math.Pow(4.0, 2.0 * (Branching - Natural));
    }
}
