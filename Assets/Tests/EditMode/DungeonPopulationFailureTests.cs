using NUnit.Framework;
using LevelGeneration;
using SimpleRPG.LevelGeneration;
using UnityEngine;

namespace SimpleRPG.Tests
{
    public class DungeonPopulationFailureTests
    {
        private GameObject host;

        [TearDown]
        public void TearDown()
        {
            if (host != null) Object.DestroyImmediate(host);
        }

        [Test]
        public void NoRooms_FailsWithReasonInsteadOfStayingPending()
        {
            host = new GameObject("population");
            var population = host.AddComponent<DungeonPopulation>();

            bool ok = population.TryPopulate(new RoomBlueprint[0], 4f, 1, out string error);

            Assert.IsFalse(ok);
            Assert.IsFalse(population.IsReady);
            Assert.IsTrue(population.HasFailed);
            Assert.IsNotEmpty(error);
            Assert.AreEqual(error, population.FailureReason);
        }

        [Test]
        public void NullRooms_Fails()
        {
            host = new GameObject("population");
            var population = host.AddComponent<DungeonPopulation>();
            Assert.IsFalse(population.TryPopulate(null, 4f, 1, out _));
            Assert.IsTrue(population.HasFailed);
        }
    }
}
