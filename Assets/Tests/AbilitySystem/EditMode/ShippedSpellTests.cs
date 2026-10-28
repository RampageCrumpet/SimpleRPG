using NUnit.Framework;
using SimpleRPG.Gameplay;
using System.Linq;
using Unity.Netcode;
using UnityEditor;
using UnityEngine;
using EntityId = SimpleRPG.Gameplay.EntityId;

namespace SimpleRPG.Abilities.Tests.EditMode
{
    /// <summary>
    /// Checks the Firebolt and Icebolt assets that ship in the game: they compile, are registered, fire a real projectile prefab and deal the
    /// damage they dealt before they became graphs.
    /// </summary>
    public class ShippedSpellTests
    {
        private const string Firebolt = "Assets/Abilities/Abilities/Firebolt.asset";
        private const string Icebolt = "Assets/Abilities/Abilities/Icebolt.asset";

        private static AbilityDefinition Load(string path)
        {
            AbilityDefinition ability = AssetDatabase.LoadAssetAtPath<AbilityDefinition>(path);
            Assert.IsNotNull(ability, $"Could not load {path} as an AbilityDefinition.");
            return ability;
        }

        [TestCase(Firebolt)]
        [TestCase(Icebolt)]
        public void Spell_CompilesWithoutErrors(string path)
        {
            CompileResult result = Load(path).Compile();

            Assert.IsTrue(result.Success, result.Report.ToString());
        }

        [TestCase(Firebolt)]
        [TestCase(Icebolt)]
        public void Spell_HasAnIdAndIsInTheRegistry(string path)
        {
            AbilityDefinition ability = Load(path);

            Assert.IsTrue(ability.HasId);
            DefinitionRegistry registry = AssetDatabase.LoadAssetAtPath<DefinitionRegistry>(Editor.DefinitionRegistryTools.RegistryAssetPath);
            Assert.IsNotNull(registry);
            Assert.IsTrue(registry.TryGet(ability.Id, out GameplayDefinition found));
            Assert.AreSame(ability, found);
        }

        [TestCase(Firebolt)]
        [TestCase(Icebolt)]
        public void Spell_HasAPositiveCooldown(string path)
        {
            Assert.Greater(Load(path).CooldownSeconds, 0f, "A spell needs a cooldown so it can't be spammed.");
        }

        [TestCase(Firebolt)]
        [TestCase(Icebolt)]
        public void Spell_ProjectilePrefab_HasProjectileAndNetworkObject(string path)
        {
            LaunchProjectileConfig launch = (LaunchProjectileConfig)Load(path).Nodes.Single(n => n.TypeId == NodeTypes.LaunchProjectile).Config;

            Assert.IsNotNull(launch.Projectile, "The spell has no projectile prefab.");
            Assert.IsNotNull(launch.Projectile.GetComponent<Projectile>(), "The prefab needs a Projectile component.");
            Assert.IsNotNull(launch.Projectile.GetComponent<NetworkObject>(), "The prefab needs a NetworkObject to be spawned.");
        }

        [TestCase(Firebolt, 20)]
        [TestCase(Icebolt, 15)]
        public void Spell_DealsTheDamageItAlwaysDid(string path, int expected)
        {
            GameplayEventDispatcher events = new GameplayEventDispatcher();
            CombatantRegistry combatants = new CombatantRegistry();
            CombatResolver combat = new CombatResolver(events, combatants);
            ScriptedProjectiles projectiles = new ScriptedProjectiles();
            AbilityExecutor executor = new AbilityExecutor(new ExecutionServices(new AttributeSetRegistry(), combat, projectiles));
            TestCombatant caster = new TestCombatant(1, 100);
            TestCombatant target = new TestCombatant(2, 100);
            combatants.Register(caster);
            combatants.Register(target);

            executor.Execute(Load(path).Compile().Ability, new ExecutionRequest(SourceHandle.FromEntity(caster.Id), caster.Id, default, new Position3(0, 0, 1)));
            projectiles.Hit(0, target.Id);

            Assert.AreEqual(100 - expected, target.Health);
        }
    }
}
