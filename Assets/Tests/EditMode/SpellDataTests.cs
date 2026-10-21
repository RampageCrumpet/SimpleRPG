using NUnit.Framework;
using SimpleRPG.Abilities;
using UnityEditor;
using UnityEngine;

namespace SimpleRPG.Tests.EditMode
{
    /// <summary>
    /// Checks that the Firebolt and Icebolt assets are configured to fire and deal damage, and that the projectile rules behave.
    /// </summary>
    public class SpellDataTests
    {
        private const string Firebolt = "Assets/Abilitities/Abillities/Firebolt.asset";
        private const string Icebolt = "Assets/Abilitities/Abillities/Icebolt.asset";

        [TestCase(Firebolt)]
        [TestCase(Icebolt)]
        public void Spell_LoadsAsFireProjectileAbility(string path)
        {
            FireProjectileAbility spell = AssetDatabase.LoadAssetAtPath<FireProjectileAbility>(path);

            Assert.IsNotNull(spell, $"Could not load {path} as a FireProjectileAbility.");
            Assert.AreEqual(typeof(FireProjectileAbillityBehaviour), spell.AbilityBehavior);
        }

        [TestCase(Firebolt)]
        [TestCase(Icebolt)]
        public void Spell_DealsPositiveDamage(string path)
        {
            FireProjectileAbility spell = AssetDatabase.LoadAssetAtPath<FireProjectileAbility>(path);

            Assert.IsNotNull(spell, $"Could not load {path}.");
            Assert.IsNotNull(spell.Damage, $"{spell.AbillityName} has no DamageInfo.");
            Assert.Greater(spell.Damage.Damage, 0, $"{spell.AbillityName} must deal damage to be useful.");
        }

        [TestCase(Firebolt)]
        [TestCase(Icebolt)]
        public void Spell_HasPositiveCooldown(string path)
        {
            Ability spell = AssetDatabase.LoadAssetAtPath<Ability>(path);

            Assert.IsNotNull(spell, $"Could not load {path}.");
            Assert.Greater(spell.CooldownTime, 0f, $"{spell.AbillityName} needs a cooldown so it can't be spammed.");
        }

        [TestCase(Firebolt)]
        [TestCase(Icebolt)]
        public void Spell_ProjectilePrefab_HasProjectileAndNetworkObject(string path)
        {
            FireProjectileAbility spell = AssetDatabase.LoadAssetAtPath<FireProjectileAbility>(path);

            Assert.IsNotNull(spell, $"Could not load {path}.");
            Assert.IsNotNull(spell.Projectile, $"{spell.AbillityName} has no projectile prefab.");
            Assert.IsNotNull(spell.Projectile.GetComponent<Projectile>(), $"{spell.AbillityName}'s prefab needs a Projectile component.");
            Assert.IsNotNull(spell.Projectile.GetComponent<Unity.Netcode.NetworkObject>(), $"{spell.AbillityName}'s prefab needs a NetworkObject to be spawned.");
        }

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
    }
}
