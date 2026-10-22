using NUnit.Framework;
using SimpleRPG.Abilities;
using UnityEngine;

namespace SimpleRPG.Tests.EditMode
{
    /// <summary>
    /// Checks the pure projectile rules. The shipped spells themselves are checked in the ability system's tests.
    /// </summary>
    public class SpellDataTests
    {
        [Test]
        public void LaunchVelocity_PointsAlongTheDirectionAtTheGivenSpeed()
        {
            Vector3 velocity = ProjectileRules.LaunchVelocity(new Vector3(0f, 0f, 3f), 5f);

            Assert.AreEqual(0f, velocity.x, 0.0001f);
            Assert.AreEqual(0f, velocity.y, 0.0001f);
            Assert.AreEqual(5f, velocity.z, 0.0001f);
        }

        [Test]
        public void LaunchVelocity_ZeroDirection_IsZero()
        {
            Vector3 velocity = ProjectileRules.LaunchVelocity(Vector3.zero, 5f);

            Assert.AreEqual(Vector3.zero, velocity);
        }

        [Test]
        public void ShouldApplyHit_FirstContactWithACharacter_Applies()
        {
            Assert.IsTrue(ProjectileRules.ShouldApplyHit(hasTarget: true, alreadyHit: false));
        }

        [Test]
        public void ShouldApplyHit_ContactWithNothing_DoesNotApply()
        {
            Assert.IsFalse(ProjectileRules.ShouldApplyHit(hasTarget: false, alreadyHit: false));
        }

        [Test]
        public void ShouldApplyHit_SecondContactInTheSameFlight_DoesNotApply()
        {
            Assert.IsFalse(ProjectileRules.ShouldApplyHit(hasTarget: true, alreadyHit: true));
        }

        [Test]
        public void IsLevelGeometry_ASolidCollider_StopsTheProjectile()
        {
            Assert.IsTrue(ProjectileRules.IsLevelGeometry(isTrigger: false, belongsToCharacter: false));
        }

        [Test]
        public void IsLevelGeometry_ATrigger_DoesNot()
        {
            Assert.IsFalse(ProjectileRules.IsLevelGeometry(isTrigger: true, belongsToCharacter: false));
        }

        [Test]
        public void IsLevelGeometry_ACharactersCollider_DoesNot()
        {
            Assert.IsFalse(ProjectileRules.IsLevelGeometry(isTrigger: false, belongsToCharacter: true));
        }
    }
}
