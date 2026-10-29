using NUnit.Framework;
using SimpleRPG.Gameplay;

namespace SimpleRPG.Abilities.Tests
{
    public class ItemInstanceIdSourceTests
    {
        [Test]
        public void Ids_StartAtOneAndNeverRepeat()
        {
            var source = new ItemInstanceIdSource();

            Assert.AreEqual(1u, source.Next());
            Assert.AreEqual(2u, source.Next());
            Assert.AreEqual(3u, source.Next());
        }

        [Test]
        public void ResettingTheServices_StartsTheSessionsIdsAgain()
        {
            GameplayServices.Reset();
            GameplayServices.Current.ItemInstanceIds.Next();
            GameplayServices.Current.ItemInstanceIds.Next();

            GameplayServices.Reset();

            Assert.AreEqual(1u, GameplayServices.Current.ItemInstanceIds.Next(), "a new session must not inherit the last session's ids");
        }
    }
}
