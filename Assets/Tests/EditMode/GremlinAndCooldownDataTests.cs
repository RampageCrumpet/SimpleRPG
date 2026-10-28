using NUnit.Framework;
using SimpleRPG.Abilities;
using UnityEditor;
using UnityEngine;

namespace SimpleRPG.Tests.EditMode
{
    /// <summary>
    /// Checks that the enemy and player attacks are wired to the right assets and that each has its own cooldown.
    /// </summary>
    public class GremlinAndCooldownDataTests
    {
        private const string GremlinAbility = "Assets/Abilities/Abilities/MeleeAttackGremlin.asset";
        private const string PlayerAbility = "Assets/Abilities/Abilities/MeleeAttack.asset";

        [Test]
        public void GremlinAbility_HasOneSecondCooldown()
        {
            AbilityDefinition ability = AssetDatabase.LoadAssetAtPath<AbilityDefinition>(GremlinAbility);
            Assert.IsNotNull(ability, $"Could not load {GremlinAbility}.");
            Assert.AreEqual(1f, ability.CooldownSeconds, 0.001f);
        }

        [Test]
        public void PlayerAbility_HasOneSecondCooldown()
        {
            // Queued combo presses bypass the cooldown, so it only limits fresh attacks and no longer needs to be short.
            AbilityDefinition ability = AssetDatabase.LoadAssetAtPath<AbilityDefinition>(PlayerAbility);
            Assert.IsNotNull(ability, $"Could not load {PlayerAbility}.");
            Assert.AreEqual(1f, ability.CooldownSeconds, 0.001f);
        }
    }
}
