using NUnit.Framework;
using SimpleRPG.Gameplay;

namespace SimpleRPG.Abilities.Tests.EditMode
{
    public class GameplayIdTests
    {
        [Test]
        public void DefinitionId_Default_IsNone()
        {
            Assert.IsTrue(default(DefinitionId).IsNone);
            Assert.AreEqual(DefinitionId.None, default(DefinitionId));
        }

        [Test]
        public void DefinitionId_NewId_IsUniqueAndNotNone()
        {
            DefinitionId a = DefinitionId.NewId();
            DefinitionId b = DefinitionId.NewId();

            Assert.IsFalse(a.IsNone);
            Assert.AreNotEqual(a, b);
        }

        [Test]
        public void DefinitionId_RoundTripsThroughItsStringForm()
        {
            DefinitionId id = DefinitionId.NewId();

            Assert.IsTrue(DefinitionId.TryParse(id.ToString(), out DefinitionId parsed));
            Assert.AreEqual(id, parsed);
            Assert.AreEqual(id.GetHashCode(), parsed.GetHashCode());
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase("not-an-id")]
        [TestCase("00000000-0000-0000-0000-000000000001")]
        public void DefinitionId_TryParse_RejectsBadText(string text)
        {
            Assert.IsFalse(DefinitionId.TryParse(text, out DefinitionId id));
            Assert.IsTrue(id.IsNone);
        }

        [Test]
        public void IdCounter_StartsAboveZeroAndIncreases()
        {
            IdCounter counter = new IdCounter();

            Assert.AreEqual(1UL, counter.Next());
            Assert.AreEqual(2UL, counter.Next());
        }

        [Test]
        public void RuntimeIds_ZeroIsInvalid()
        {
            Assert.IsFalse(new EntityId(0).IsValid);
            Assert.IsFalse(new ExecutionId(0).IsValid);
            Assert.IsFalse(new StatusInstanceId(0).IsValid);
            Assert.IsFalse(new ItemInstanceId(0).IsValid);
            Assert.IsTrue(new EntityId(1).IsValid);
        }

        [Test]
        public void DeathTransitionId_NewLifeOfSameEntity_IsADifferentTransition()
        {
            EntityId entity = new EntityId(7);

            Assert.AreEqual(new DeathTransitionId(entity, 1), new DeathTransitionId(entity, 1));
            Assert.AreNotEqual(new DeathTransitionId(entity, 1), new DeathTransitionId(entity, 2));
            Assert.AreNotEqual(new DeathTransitionId(entity, 1), new DeathTransitionId(new EntityId(8), 1));
        }
    }
}
