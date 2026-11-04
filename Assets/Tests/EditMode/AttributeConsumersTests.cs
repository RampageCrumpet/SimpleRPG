using NUnit.Framework;
using SimpleRPG.Gameplay;
using System.Linq;

namespace SimpleRPG.Tests.EditMode
{
    /// <summary>
    /// The list of attributes gameplay reads is what keeps affixes that would do nothing out of loot, so it must stay honest.
    /// </summary>
    public class AttributeConsumersTests
    {
        [Test]
        public void TheAttributesTheGameReadsAreReadable_AndTheOnesNothingReadsAreNot()
        {
            Assert.IsTrue(AttributeConsumers.IsRead(DerivedStats.ArmorRating.Key));
            Assert.IsTrue(AttributeConsumers.IsRead(PrimaryAttributes.Constitution.Key));
            Assert.IsFalse(AttributeConsumers.IsRead(PrimaryAttributes.Strength.Key), "Melee damage does not read Physical Power yet.");
            Assert.IsFalse(AttributeConsumers.IsRead(DerivedStats.PhysicalPower.Key));
            Assert.IsFalse(AttributeConsumers.IsRead(PrimaryAttributes.Dexterity.Key));
            Assert.IsFalse(AttributeConsumers.IsRead(DerivedStats.AttackSpeed.Key));
            Assert.IsFalse(AttributeConsumers.IsRead(DerivedStats.MagicalPower.Key));
            Assert.IsFalse(AttributeConsumers.IsRead(""));
            Assert.IsFalse(AttributeConsumers.IsRead(null));
        }

        [Test]
        public void EveryAttributeListedExists()
        {
            var known = PrimaryAttributes.All.Concat(DerivedStats.All).Select(a => a.Key).ToList();

            foreach (string key in AttributeConsumers.Keys)
            {
                CollectionAssert.Contains(known, key, key + " is listed as read but is not a catalogued attribute.");
            }
        }
    }
}
