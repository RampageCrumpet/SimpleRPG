using System.Diagnostics;
using SimpleRPG.LevelGeneration;
using NUnit.Framework;

namespace SimpleRPG.Tests.EditMode
{
    /// <summary>
    /// Times layout generation at several sizes and logs the result, so a change to the generator can be compared before and after.
    /// Nothing here asserts a time, because machines differ, or a size, because the hand-built set may not reach every count.
    /// </summary>
    public class LayoutPerformanceTests
    {
        private static readonly int[] RoomCounts = { 20, 100, 400 };
        private const int SeedsPerSize = 10;

        [Test]
        public void GenerationTime_ByRoomCount([ValueSource(nameof(RoomCounts))] int roomCount)
        {
            var definitions = TestRooms.MixedSet();
            int generated = 0;
            int succeeded = 0;
            var timer = Stopwatch.StartNew();

            for (int seed = 0; seed < SeedsPerSize; seed++)
            {
                GenerationResult result = new LayoutGenerator(seed, definitions).Generate(roomCount);
                succeeded += result.Success ? 1 : 0;
                generated += result.Layout.Rooms.Count;
            }

            timer.Stop();
            UnityEngine.Debug.Log($"Generated {SeedsPerSize} layouts of at least {roomCount} rooms ({generated} rooms in all, {succeeded} accepted) in {timer.ElapsedMilliseconds} ms, {timer.Elapsed.TotalMilliseconds / SeedsPerSize:F1} ms each.");
        }
    }
}
