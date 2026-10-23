using NUnit.Framework;
using SimpleRPG.Gameplay;
using System.Collections.Generic;
using UnityEditor;

namespace SimpleRPG.Abilities.Tests.EditMode
{
    /// <summary>
    /// The shipped melee attacks scale weapon damage and combo scalar by Physical Power: +5% per point, read from the attacker's stat sheet at the hit.
    /// </summary>
    public class ShippedMeleePowerTests
    {
        private const string PlayerMelee = "Assets/Abilitities/Abillities/MeleeAttack.asset";
        private const string MonsterMelee = "Assets/Abilitities/Abillities/MeleeAttackGremlin.asset";

        private GameplayEventDispatcher events;
        private CombatantRegistry combatants;
        private AttributeSetRegistry attributes;
        private ScriptedMelee melee;
        private AbilityExecutor executor;
        private TestCombatant attacker;
        private TestCombatant victim;

        [SetUp]
        public void SetUp()
        {
            events = new GameplayEventDispatcher();
            combatants = new CombatantRegistry();
            attributes = new AttributeSetRegistry();
            melee = new ScriptedMelee();
            executor = new AbilityExecutor(new ExecutionServices(attributes, new CombatResolver(events, combatants), null, melee));

            attacker = new TestCombatant(1, 100);
            victim = new TestCombatant(2, 1000);
            combatants.Register(attacker);
            combatants.Register(victim);
        }

        private AttributeSet GivePower(double power)
        {
            AttributeSet set = new StatSheet(StatOwnership.Direct, new[] { new StatRule(DerivedStats.PhysicalPower, power) }).CreateSet();
            attributes.Register(SourceHandle.FromEntity(attacker.Id), set);
            return set;
        }

        private int Hit(string path, float weaponDamage = 15f, float scalar = 1f)
        {
            AbilityDefinition definition = AssetDatabase.LoadAssetAtPath<AbilityDefinition>(path);
            CompileResult result = definition.Compile();
            Assert.IsTrue(result.Success, result.Report.ToString());

            executor.Execute(result.Ability, new ExecutionRequest(SourceHandle.FromEntity(attacker.Id), attacker.Id, default, new Position3(0, 0, 1)));
            int before = victim.Health;
            melee.Hit(0, victim.Id, comboScalar: scalar, weaponDamage: weaponDamage);
            return before - victim.Health;
        }

        [TestCase(PlayerMelee)]
        [TestCase(MonsterMelee)]
        public void WithoutPhysicalPower_DamageIsUnchanged(string path)
        {
            Assert.AreEqual(15, Hit(path));
        }

        [TestCase(PlayerMelee)]
        [TestCase(MonsterMelee)]
        public void EachPointOfPhysicalPower_AddsFivePercentOnce(string path)
        {
            GivePower(10);

            // 15 * (1 + 0.05 * 10) = 22.5, rounded away from zero.
            Assert.AreEqual(23, Hit(path));
        }

        [TestCase(PlayerMelee)]
        [TestCase(MonsterMelee)]
        public void WeaponDamageAndComboScalarStillParticipateOnce(string path)
        {
            GivePower(20);

            // 10 * 0.8 * (1 + 0.05 * 20) = 16.
            Assert.AreEqual(16, Hit(path, weaponDamage: 10f, scalar: 0.8f));
        }

        [Test]
        public void AModifierOnPhysicalPower_ChangesTheNextHit()
        {
            AttributeSet set = GivePower(10);
            Assert.AreEqual(23, Hit(PlayerMelee));

            ModifierHandle handle = set.AddModifier(AttributeModifier.Flat(DerivedStats.PhysicalPower, 10, default));
            Assert.AreEqual(30, Hit(PlayerMelee), "15 * 2.0");

            set.RemoveModifier(handle);
            Assert.AreEqual(23, Hit(PlayerMelee));
        }

        [Test]
        public void TheSkeletonsProfile_HitsForAboutForty()
        {
            StatProfile profile = AssetDatabase.LoadAssetAtPath<StatProfile>("Assets/Stats/SkeletonStats.asset");
            attributes.Register(SourceHandle.FromEntity(attacker.Id), profile.CreateSet());

            int damage = Hit(MonsterMelee);

            Assert.That(damage, Is.InRange(38, 42));
        }
    }
}
