using System.IO;
using NUnit.Framework;
using SimpleRPG.Abilities;
using UnityEditor;
using UnityEngine;

namespace SimpleRPG.Tests.EditMode
{
    /// <summary>
    /// Checks that the enemy and player attacks are wired to the right assets, and that the player's cooldown
    /// lets a queued press land during the first swing.
    /// </summary>
    public class GremlinAndCooldownDataTests
    {
        private const string GremlinPrefab = "Assets/Prefabs/Characters/Enemies/Gremlin.prefab";
        private const string GremlinAbility = "Assets/Abilitities/Abillities/MeleeAttackGremlin.asset";
        private const string PlayerAbility = "Assets/Abilitities/Abillities/MeleeAttack.asset";
        private const string GremlinController = "Assets/Prefabs/Characters/Enemies/GremlinAnimationController.controller";
        private const float AttackOneHitStart = 56f / 60f;

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
        public void PlayerCooldown_DoesNotBlockAQueuedPressDuringAttackOne()
        {
            // A queued press is only accepted once the cooldown has run out. It must expire before the first hit opens,
            // so a press anywhere in the first swing can still queue the combo.
            Ability ability = AssetDatabase.LoadAssetAtPath<Ability>(PlayerAbility);
            Assert.IsNotNull(ability, $"Could not load {PlayerAbility}.");
            Assert.Less(ability.CooldownTime, AttackOneHitStart,
                "The player's melee cooldown should expire before attack 1's hitbox opens.");
        }
    }
}
