using NUnit.Framework;
using SimpleRPG.Gameplay;
using System.Collections.Generic;
using UnityEngine;
using EntityId = SimpleRPG.Gameplay.EntityId;

namespace SimpleRPG.Abilities.Tests.EditMode
{
    /// <summary>
    /// The damage node's type and bypass settings, from an authored ability to the damage that lands: the node says which defence the damage
    /// meets, and an effect that skips defences has to say so. Runs the real executor and resolver; only the projectile is scripted.
    /// </summary>
    public class DamageNodeMitigationTests
    {
        private sealed class Defended : ICombatant, IDamageMitigator
        {
            private readonly double physical;
            private readonly double magical;

            public Defended(ulong id, int health, double physical, double magical)
            {
                Id = new EntityId(id);
                Health = health;
                IsAlive = true;
                LifeId = 1;
                this.physical = physical;
                this.magical = magical;
            }

            public EntityId Id { get; }

            public int Health { get; private set; }

            public bool IsAlive { get; private set; }

            public int LifeId { get; private set; }

            public Position3 Position => default;

            public void CommitHealth(int health) => Health = health;

            public void CommitDeath() => IsAlive = false;

            public void CommitRevive(int health) => Health = health;

            public double Reduction(DamageType type) => type == DamageType.Magical ? magical : physical;
        }

        private readonly List<Object> created = new List<Object>();
        private CombatantRegistry combatants;
        private ScriptedProjectiles projectiles;
        private AbilityExecutor executor;
        private GameObject prefab;
        private TestCombatant caster;
        private ulong nextTarget = 10;

        [SetUp]
        public void SetUp()
        {
            var events = new GameplayEventDispatcher();
            combatants = new CombatantRegistry();
            var combat = new CombatResolver(events, combatants);
            var attributes = new AttributeSetRegistry();
            projectiles = new ScriptedProjectiles();
            executor = new AbilityExecutor(new ExecutionServices(attributes, combat, projectiles));
            prefab = new GameObject("ProjectilePrefab");
            created.Add(prefab);

            caster = new TestCombatant(1, 100);
            combatants.Register(caster);
            var set = new AttributeSet();
            set.SetBase(new AttributeId(AbilityBuilder.MagicPower), 30);
            attributes.Register(SourceHandle.FromEntity(caster.Id), set);
        }

        [TearDown]
        public void TearDown()
        {
            foreach (Object o in created)
            {
                if (o != null) Object.DestroyImmediate(o);
            }

            created.Clear();
        }

        // Fires the test Firebolt (12 + 0.8 x 30 = 36 damage) at a fresh target with the given defences and returns the damage that landed.
        private int Land(DamageType type, bool bypasses, double physical, double magical)
        {
            AbilityDefinition ability = AbilityBuilder.Firebolt(prefab, damageType: type, bypassesMitigation: bypasses);
            created.Add(ability);
            CompileResult compiled = ability.Compile();
            Assert.IsTrue(compiled.Success, compiled.Report.ToString());

            var target = new Defended(nextTarget++, 1000, physical, magical);
            combatants.Register(target);
            executor.Execute(compiled.Ability, new ExecutionRequest(SourceHandle.FromEntity(caster.Id), caster.Id, new Position3(0, 1, 0), new Position3(0, 0, 1)));
            projectiles.Hit(projectiles.Launches.Count - 1, target.Id);
            return 1000 - target.Health;
        }

        [Test]
        public void ANodeLeftAtItsDefaults_DealsPhysicalDamage_ThatIsMitigated()
        {
            Assert.AreEqual(new DamageConfig().Type, DamageType.Physical);
            Assert.IsFalse(new DamageConfig().BypassesMitigation);

            Assert.AreEqual(18, Land(DamageType.Physical, false, physical: 0.5, magical: 0));
        }

        [Test]
        public void AMagicalNode_MeetsMagicalReduction_NotPhysical()
        {
            Assert.AreEqual(36, Land(DamageType.Magical, false, physical: 0.5, magical: 0), "Armour does not stop it.");
            Assert.AreEqual(18, Land(DamageType.Magical, false, physical: 0, magical: 0.5));
        }

        [Test]
        public void ANodeThatBypasses_SkipsBothDefences()
        {
            Assert.AreEqual(36, Land(DamageType.Physical, true, physical: 0.9, magical: 0.9));
            Assert.AreEqual(36, Land(DamageType.Magical, true, physical: 0.9, magical: 0.9));
        }
    }
}
