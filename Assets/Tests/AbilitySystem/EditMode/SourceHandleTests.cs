using NUnit.Framework;
using SimpleRPG.Gameplay;

namespace SimpleRPG.Abilities.Tests.EditMode
{
    public class SourceHandleTests
    {
        [Test]
        public void Default_IsNone()
        {
            Assert.AreEqual(SourceKind.None, default(SourceHandle).Kind);
            Assert.AreEqual(SourceHandle.None, default(SourceHandle));
        }

        [Test]
        public void EntitySource_RoundTripsItsEntity()
        {
            SourceHandle source = SourceHandle.FromEntity(new EntityId(5));

            Assert.AreEqual(SourceKind.Entity, source.Kind);
            Assert.IsTrue(source.TryGetEntity(out EntityId entity));
            Assert.AreEqual(new EntityId(5), entity);
        }

        [Test]
        public void ItemSource_IsNotAnEntity()
        {
            SourceHandle source = SourceHandle.FromItem(new ItemInstanceId(5));

            Assert.IsFalse(source.TryGetEntity(out EntityId entity));
            Assert.IsFalse(entity.IsValid);
            Assert.IsTrue(source.TryGetItem(out ItemInstanceId item));
            Assert.AreEqual(new ItemInstanceId(5), item);
        }

        [Test]
        public void SameIdOfDifferentKinds_AreNotEqual()
        {
            Assert.AreNotEqual(SourceHandle.FromEntity(new EntityId(5)), SourceHandle.FromWorldObject(5));
            Assert.AreNotEqual(SourceHandle.FromItem(new ItemInstanceId(5)), SourceHandle.FromWorldObject(5));
        }

        [Test]
        public void StatusSource_RoundTripsItsStatus()
        {
            SourceHandle source = SourceHandle.FromStatus(new StatusInstanceId(9));

            Assert.IsTrue(source.TryGetStatus(out StatusInstanceId status));
            Assert.AreEqual(new StatusInstanceId(9), status);
        }

        [Test]
        public void EqualHandles_HaveEqualHashes()
        {
            Assert.AreEqual(SourceHandle.FromEntity(new EntityId(3)).GetHashCode(), SourceHandle.FromEntity(new EntityId(3)).GetHashCode());
        }
    }
}
