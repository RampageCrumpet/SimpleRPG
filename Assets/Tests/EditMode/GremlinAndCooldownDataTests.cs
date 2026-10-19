using System.IO;
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
        private const string GremlinPrefab = "Assets/Prefabs/Characters/Enemies/Gremlin.prefab";
        private const string GremlinAbility = "Assets/Abilitities/Abillities/MeleeAttackGremlin.asset";
        private const string PlayerAbility = "Assets/Abilitities/Abillities/MeleeAttack.asset";
        private const string GremlinController = "Assets/Prefabs/Characters/Enemies/GremlinAnimationController.controller";

        [Test]
        public void GremlinAbility_HasOneSecondCooldown()
        {
            Ability ability = AssetDatabase.LoadAssetAtPath<Ability>(GremlinAbility);
            Assert.IsNotNull(ability, $"Could not load {GremlinAbility}.");
            Assert.AreEqual(1f, ability.CooldownTime, 0.001f);
        }

        [Test]
        public void GremlinPrefab_UsesItsOwnAbilityAndController()
        {
            string prefabText = File.ReadAllText(GremlinPrefab);

            Assert.IsTrue(prefabText.Contains(AssetDatabase.AssetPathToGUID(GremlinAbility)),
                "Gremlin.prefab should reference MeleeAttackGremlin.asset.");
            Assert.IsTrue(prefabText.Contains(AssetDatabase.AssetPathToGUID(GremlinController)),
                "Gremlin.prefab should reference GremlinAnimationController.controller.");
            Assert.IsFalse(prefabText.Contains(AssetDatabase.AssetPathToGUID("Assets/Prefabs/Characters/Player/PlayerAnimationController.controller")),
                "Gremlin.prefab should no longer use the player's controller.");
        }

        [Test]
        public void GremlinController_HasNoLongswordStates()
        {
            RuntimeAnimatorController controller = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(GremlinController);
            Assert.IsNotNull(controller, $"Could not load {GremlinController}.");

            foreach (AnimationClip clip in controller.animationClips)
            {
                Assert.IsFalse(clip.name.StartsWith("Longsword_"), $"Gremlin controller should not use the longsword clip {clip.name}.");
            }
        }

        [Test]
        public void PlayerAbility_HasOneSecondCooldown()
        {
            // Queued combo presses bypass the cooldown, so it only limits fresh attacks and no longer needs to be short.
            Ability ability = AssetDatabase.LoadAssetAtPath<Ability>(PlayerAbility);
            Assert.IsNotNull(ability, $"Could not load {PlayerAbility}.");
            Assert.AreEqual(1f, ability.CooldownTime, 0.001f);
        }
    }
}
