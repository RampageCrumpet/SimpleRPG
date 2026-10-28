using NUnit.Framework;
using SimpleRPG.Gameplay;
using SimpleRPG.InventorySystem.LootGeneration;

namespace SimpleRPG.Tests.EditMode
{
    /// <summary>
    /// Loot must roll from the seeded gameplay stream so one logged seed reproduces a session.
    /// </summary>
    public class GameplayRandomSourceTests
    {
        [Test]
        public void SameSeed_GivesTheSameSequence_AndADifferentSeedDoesNot()
        {
            var a = new GameplayRandomSource(new SeededRandom(1234));
            var b = new GameplayRandomSource(new SeededRandom(1234));
            var c = new GameplayRandomSource(new SeededRandom(4321));

            bool differs = false;
            for (int i = 0; i < 50; i++)
            {
                double x = a.NextDouble();
                Assert.AreEqual(x, b.NextDouble());
                Assert.That(x, Is.GreaterThanOrEqualTo(0d).And.LessThan(1d));
                if (x != c.NextDouble()) differs = true;
            }

            Assert.IsTrue(differs);
        }

        [Test]
        public void Current_FollowsTheGameplayServicesStream()
        {
            GameplayServices.Reset().Random = new SeededRandom(99);
            double first = GameplayRandomSource.Current.NextDouble();
            GameplayServices.Reset().Random = new SeededRandom(99);
            Assert.AreEqual(first, GameplayRandomSource.Current.NextDouble(), "the same stream seed gives the same first roll");
            GameplayServices.Reset();
        }
    }
}
