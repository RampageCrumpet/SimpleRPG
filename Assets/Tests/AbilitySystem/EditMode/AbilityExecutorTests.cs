using NUnit.Framework;
using SimpleRPG.Gameplay;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using EntityId = SimpleRPG.Gameplay.EntityId;

namespace SimpleRPG.Abilities.Tests.EditMode
{
    /// <summary>
    /// Runs compiled abilities with the real executor, combat resolver, attribute sets and event dispatcher. Only the projectile itself is
    /// scripted (<see cref="ScriptedProjectiles"/>); the networked projectile is covered by the PlayMode tests.
    /// </summary>
    public class AbilityExecutorTests
    {
        private static readonly AttributeId MagicPower = new AttributeId(AbilityBuilder.MagicPower);

        private readonly List<Object> created = new List<Object>();

        private GameplayEventDispatcher events;
        private CombatantRegistry combatants;
        private CombatResolver combat;
        private AttributeSetRegistry attributes;
        private ScriptedProjectiles projectiles;
        private ExecutionServices services;
        private AbilityExecutor executor;
        private GameObject prefab;
        private List<string> diagnostics;

        private TestCombatant wizard;
        private TestCombatant monster;
        private TestCombatant target;

        [SetUp]
        public void SetUp()
        {
            events = new GameplayEventDispatcher();
            combatants = new CombatantRegistry();
            combat = new CombatResolver(events, combatants);
            attributes = new AttributeSetRegistry();
            projectiles = new ScriptedProjectiles();
            services = new ExecutionServices(attributes, combat, projectiles);
            executor = new AbilityExecutor(services);
            diagnostics = new List<string>();
            services.Diagnostic += diagnostics.Add;
            prefab = new GameObject("ProjectilePrefab");
            created.Add(prefab);

            wizard = Add(1, 100);
            monster = Add(2, 100);
            target = Add(3, 500);
            SetMagicPower(SourceHandle.FromEntity(wizard.Id), 30);
            SetMagicPower(SourceHandle.FromEntity(monster.Id), 8);
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

        private TestCombatant Add(ulong id, int health)
        {
            TestCombatant combatant = new TestCombatant(id, health);
            combatants.Register(combatant);
            return combatant;
        }

        private AttributeSet SetMagicPower(SourceHandle source, double value)
        {
            AttributeSet set = new AttributeSet();
            set.SetBase(MagicPower, value);
            attributes.Register(source, set);
            return set;
        }

        private CompiledAbility Compile(AbilityDefinition ability)
        {
            created.Add(ability);
            CompileResult result = ability.Compile();
            Assert.IsTrue(result.Success, result.Report.ToString());
            return result.Ability;
        }

        private static ExecutionRequest Request(SourceHandle source, EntityId instigator) =>
            new ExecutionRequest(source, instigator, new Position3(0, 1, 0), new Position3(0, 0, 1));

        private int Damage(TestCombatant combatant, int startingHealth) => startingHealth - combatant.Health;

        // ----- Same definition from different sources -----

        [Test]
        public void Firebolt_FromAPlayer_UsesThePlayersMagicPower()
        {
            CompiledAbility firebolt = Compile(AbilityBuilder.Firebolt(prefab));

            executor.Execute(firebolt, Request(SourceHandle.FromEntity(wizard.Id), wizard.Id));
            projectiles.Hit(0, target.Id);

            Assert.AreEqual(12 + 0.8 * 30, Damage(target, 500), 0.5, "12 + 0.8 x 30 = 36");
        }

        [Test]
        public void Firebolt_FromAMonster_UsesTheMonstersMagicPower()
        {
            CompiledAbility firebolt = Compile(AbilityBuilder.Firebolt(prefab));

            executor.Execute(firebolt, Request(SourceHandle.FromEntity(monster.Id), monster.Id));
            projectiles.Hit(0, target.Id);

            Assert.AreEqual(18, Damage(target, 500), "12 + 0.8 x 8 = 18.4, rounded to 18");
        }

        [Test]
        public void Firebolt_FromAChargedItem_UsesTheItemsFixedMagicPower()
        {
            SourceHandle staff = SourceHandle.FromItem(new ItemInstanceId(77));
            SetMagicPower(staff, 20);
            CompiledAbility firebolt = Compile(AbilityBuilder.Firebolt(prefab));

            executor.Execute(firebolt, Request(staff, wizard.Id));
            projectiles.Hit(0, target.Id);

            Assert.AreEqual(28, Damage(target, 500), "12 + 0.8 x 20 = 28, the staff's own stat and not the wizard's");
        }

        [Test]
        public void Firebolt_FromAWorldObject_NeedsNoInstigatorOrCaster()
        {
            SourceHandle brazier = SourceHandle.FromWorldObject(5);
            SetMagicPower(brazier, 10);
            CompiledAbility firebolt = Compile(AbilityBuilder.Firebolt(prefab));

            executor.Execute(firebolt, Request(brazier, default));
            projectiles.Hit(0, target.Id);

            Assert.AreEqual(20, Damage(target, 500), "12 + 0.8 x 10 = 20");
        }

        [Test]
        public void Firebolt_FromASourceWithNoAttributes_UsesTheFallback()
        {
            CompiledAbility firebolt = Compile(AbilityBuilder.Firebolt(prefab, fallback: 5));

            executor.Execute(firebolt, Request(SourceHandle.FromWorldObject(99), default));
            projectiles.Hit(0, target.Id);

            Assert.AreEqual(16, Damage(target, 500), "12 + 0.8 x 5 = 16");
        }

        [Test]
        public void TheSameCompiledAbility_RunsForDifferentSourcesWithoutLeaking()
        {
            CompiledAbility firebolt = Compile(AbilityBuilder.Firebolt(prefab));
            TestCombatant other = Add(4, 500);

            executor.Execute(firebolt, Request(SourceHandle.FromEntity(wizard.Id), wizard.Id));
            executor.Execute(firebolt, Request(SourceHandle.FromEntity(monster.Id), monster.Id));
            projectiles.Hit(0, target.Id);
            projectiles.Hit(1, other.Id);

            Assert.AreEqual(36, Damage(target, 500));
            Assert.AreEqual(18, Damage(other, 500));
        }

        [Test]
        public void InstigatorBinding_ReadsTheInstigator_EvenWhenTheSourceIsAnItem()
        {
            SourceHandle staff = SourceHandle.FromItem(new ItemInstanceId(77));
            SetMagicPower(staff, 20);
            CompiledAbility firebolt = Compile(AbilityBuilder.Firebolt(prefab, kind: AttributeBindingKind.Instigator));

            executor.Execute(firebolt, Request(staff, wizard.Id));
            projectiles.Hit(0, target.Id);

            Assert.AreEqual(36, Damage(target, 500), "Bound to the instigator: the wizard's 30, not the staff's 20");
        }

        // ----- Launch and continuation -----

        [Test]
        public void Execute_LaunchesTheProjectile_AndDealsNoDamageUntilItHits()
        {
            CompiledAbility firebolt = Compile(AbilityBuilder.Firebolt(prefab));

            ExecutionResult result = executor.Execute(firebolt, Request(SourceHandle.FromEntity(wizard.Id), wizard.Id));

            Assert.IsTrue(result.Completed);
            Assert.AreEqual(1, projectiles.Launches.Count);
            Assert.AreEqual(1, executor.PendingContinuations);
            Assert.AreEqual(500, target.Health);
        }

        [Test]
        public void TheLaunch_CarriesSourceInstigatorOriginAndDirection()
        {
            CompiledAbility firebolt = Compile(AbilityBuilder.Firebolt(prefab));

            executor.Execute(firebolt, Request(SourceHandle.FromEntity(wizard.Id), wizard.Id));

            ProjectileLaunch launch = projectiles.Launches[0].Launch;
            Assert.AreEqual(SourceHandle.FromEntity(wizard.Id), launch.Source);
            Assert.AreEqual(wizard.Id, launch.Instigator);
            Assert.AreEqual(1f, launch.Origin.Y);
            Assert.AreEqual(1f, launch.Direction.Z);
            Assert.AreSame(prefab, launch.Prefab);
            Assert.IsTrue(launch.Token.IsValid);
            Assert.IsTrue(launch.Execution.IsValid);
        }

        [Test]
        public void AnImpact_ResumesTheExecution_ExactlyOnce()
        {
            CompiledAbility firebolt = Compile(AbilityBuilder.Firebolt(prefab));
            executor.Execute(firebolt, Request(SourceHandle.FromEntity(wizard.Id), wizard.Id));

            projectiles.Hit(0, target.Id);
            projectiles.Hit(0, target.Id);

            Assert.AreEqual(36, Damage(target, 500), "A second report for the same projectile must do nothing.");
            Assert.AreEqual(0, executor.PendingContinuations);
        }

        [Test]
        public void HittingTheWorld_DealsNoDamage_AndIsNotAnError()
        {
            CompiledAbility firebolt = Compile(AbilityBuilder.Firebolt(prefab));
            executor.Execute(firebolt, Request(SourceHandle.FromEntity(wizard.Id), wizard.Id));

            projectiles.HitWorld(0);

            Assert.AreEqual(500, target.Health);
            Assert.IsEmpty(diagnostics);
            Assert.AreEqual(0, executor.PendingContinuations);
        }

        [Test]
        public void AnExpiredProjectile_ReleasesItsContinuation_AndNothingResumes()
        {
            CompiledAbility firebolt = Compile(AbilityBuilder.Firebolt(prefab));
            executor.Execute(firebolt, Request(SourceHandle.FromEntity(wizard.Id), wizard.Id));

            projectiles.Expire(0);
            projectiles.Hit(0, target.Id);

            Assert.AreEqual(0, executor.PendingContinuations);
            Assert.AreEqual(500, target.Health, "An expired projectile's late impact must not resume anything.");
        }

        [Test]
        public void ARefusedLaunch_IsReported_AndLeavesNothingPending()
        {
            projectiles.Accepts = false;
            CompiledAbility firebolt = Compile(AbilityBuilder.Firebolt(prefab));

            executor.Execute(firebolt, Request(SourceHandle.FromEntity(wizard.Id), wizard.Id));

            Assert.AreEqual(0, executor.PendingContinuations);
            Assert.IsTrue(diagnostics.Any(d => d.Contains("could not be launched") && d.Contains("launch")), string.Join("\n", diagnostics));
        }

        [Test]
        public void TheImpactsBodyLocation_ReachesTheDamage()
        {
            CompiledAbility firebolt = Compile(AbilityBuilder.Firebolt(prefab));
            DamageRequest seen = default;
            events.Subscribe<DamageResolvedEvent>(e => seen = e.Request);
            executor.Execute(firebolt, Request(SourceHandle.FromEntity(wizard.Id), wizard.Id));

            projectiles.Hit(0, target.Id, bodyLocation: 2);

            Assert.AreEqual(2, seen.BodyLocation);
        }

        // ----- Attribution and provenance -----

        [Test]
        public void TheInstigator_IsCreditedWithTheDamage_AndTheKill()
        {
            target = Add(10, 20);
            CompiledAbility firebolt = Compile(AbilityBuilder.Firebolt(prefab));
            DeathContext death = null;
            events.Subscribe<EntityDiedEvent>(e => death = e.Context);

            executor.Execute(firebolt, Request(SourceHandle.FromEntity(wizard.Id), wizard.Id));
            projectiles.Hit(0, target.Id);

            Assert.IsNotNull(death);
            Assert.AreEqual(wizard.Id, death.Killer);
            Assert.AreEqual(SourceHandle.FromEntity(wizard.Id), death.KillingSource);
        }

        [Test]
        public void SpellDamage_IsTaggedAsASpell_NotAWeaponHit()
        {
            CompiledAbility firebolt = Compile(AbilityBuilder.Firebolt(prefab));
            DamageProvenance seen = DamageProvenance.None;
            events.Subscribe<DamageResolvedEvent>(e => seen = e.Request.Provenance);

            executor.Execute(firebolt, Request(SourceHandle.FromEntity(wizard.Id), wizard.Id));
            projectiles.Hit(0, target.Id);

            Assert.AreEqual(DamageProvenance.Spell, seen);
            Assert.AreNotEqual(DamageProvenance.DirectWeaponHit, seen);
        }

        [Test]
        public void DamageFromAHazard_HasNoInstigator()
        {
            SourceHandle brazier = SourceHandle.FromWorldObject(5);
            CompiledAbility firebolt = Compile(AbilityBuilder.Firebolt(prefab));
            DamageRequest seen = default;
            events.Subscribe<DamageResolvedEvent>(e => seen = e.Request);

            executor.Execute(firebolt, Request(brazier, default));
            projectiles.Hit(0, target.Id);

            Assert.IsFalse(seen.Instigator.IsValid);
            Assert.AreEqual(brazier, seen.Source);
        }

        [Test]
        public void TheExecutionId_ReachesTheDamage_AndDiffersPerExecution()
        {
            CompiledAbility firebolt = Compile(AbilityBuilder.Firebolt(prefab));
            List<ExecutionId> seen = new List<ExecutionId>();
            events.Subscribe<DamageResolvedEvent>(e => seen.Add(e.Request.Execution));

            ExecutionResult first = executor.Execute(firebolt, Request(SourceHandle.FromEntity(wizard.Id), wizard.Id));
            ExecutionResult second = executor.Execute(firebolt, Request(SourceHandle.FromEntity(wizard.Id), wizard.Id));
            projectiles.Hit(0, target.Id);
            projectiles.Hit(1, target.Id);

            CollectionAssert.AreEqual(new[] { first.Id, second.Id }, seen);
            Assert.AreNotEqual(first.Id, second.Id);
        }

        // ----- Evaluation policy -----

        [Test]
        public void Snapshot_KeepsTheValueFromActivation_WhenTheStatChangesInFlight()
        {
            AttributeSet wizardAttributes = SetMagicPower(SourceHandle.FromEntity(wizard.Id), 30);
            CompiledAbility firebolt = Compile(AbilityBuilder.Firebolt(prefab, policy: EvaluationPolicy.OnActivation));
            executor.Execute(firebolt, Request(SourceHandle.FromEntity(wizard.Id), wizard.Id));

            wizardAttributes.SetBase(MagicPower, 100);
            projectiles.Hit(0, target.Id);

            Assert.AreEqual(36, Damage(target, 500), "Captured at activation: 12 + 0.8 x 30");
        }

        [Test]
        public void Live_ReadsTheValueAtImpact()
        {
            AttributeSet wizardAttributes = SetMagicPower(SourceHandle.FromEntity(wizard.Id), 30);
            CompiledAbility firebolt = Compile(AbilityBuilder.Firebolt(prefab, policy: EvaluationPolicy.OnExecution));
            executor.Execute(firebolt, Request(SourceHandle.FromEntity(wizard.Id), wizard.Id));

            wizardAttributes.SetBase(MagicPower, 100);
            projectiles.Hit(0, target.Id);

            Assert.AreEqual(92, Damage(target, 500), "Read at impact: 12 + 0.8 x 100");
        }

        [Test]
        public void ProjectileLaunchedBeforeTheCasterDies_StillHitsWithTheCapturedPotency()
        {
            CompiledAbility firebolt = Compile(AbilityBuilder.Firebolt(prefab));
            executor.Execute(firebolt, Request(SourceHandle.FromEntity(wizard.Id), wizard.Id));

            combat.Apply(new DamageRequest(wizard.Id, 1000));
            attributes.Unregister(SourceHandle.FromEntity(wizard.Id));
            Assert.IsFalse(wizard.IsAlive);
            projectiles.Hit(0, target.Id);

            Assert.AreEqual(36, Damage(target, 500), "The spell outlives its caster and keeps the potency it was cast with.");
        }

        [Test]
        public void ADeadVictim_TakesNoFurtherDamage()
        {
            CompiledAbility firebolt = Compile(AbilityBuilder.Firebolt(prefab));
            executor.Execute(firebolt, Request(SourceHandle.FromEntity(wizard.Id), wizard.Id));
            combat.Apply(new DamageRequest(target.Id, 1000));

            projectiles.Hit(0, target.Id);

            Assert.IsEmpty(diagnostics);
            Assert.AreEqual(0, target.Health);
        }

        // ----- Budgets -----

        [Test]
        public void AnExecution_PastItsOperationBudget_IsStopped_AndReported()
        {
            services.MaxOperationsPerExecution = 1;
            CompiledAbility firebolt = Compile(AbilityBuilder.Firebolt(prefab));

            ExecutionResult result = executor.Execute(firebolt, Request(SourceHandle.FromEntity(wizard.Id), wizard.Id));

            Assert.IsFalse(result.Completed);
            Assert.AreEqual(0, projectiles.Launches.Count, "Activate ran; the launch did not.");
            Assert.IsTrue(diagnostics.Any(d => d.Contains("limit")), string.Join("\n", diagnostics));
        }

        [Test]
        public void TheProjectileBudget_IsEnforced()
        {
            services.MaxProjectilesPerExecution = 0;
            CompiledAbility firebolt = Compile(AbilityBuilder.Firebolt(prefab));

            executor.Execute(firebolt, Request(SourceHandle.FromEntity(wizard.Id), wizard.Id));

            Assert.AreEqual(0, projectiles.Launches.Count);
            Assert.AreEqual(0, executor.PendingContinuations);
            Assert.IsTrue(diagnostics.Any(d => d.Contains("projectile limit")), string.Join("\n", diagnostics));
        }

        [Test]
        public void NoDeliveryAvailable_IsReported_NotThrown()
        {
            ExecutionServices bare = new ExecutionServices(attributes, combat, null);
            AbilityExecutor bareExecutor = new AbilityExecutor(bare);
            List<string> messages = new List<string>();
            bare.Diagnostic += messages.Add;
            CompiledAbility firebolt = Compile(AbilityBuilder.Firebolt(prefab));

            Assert.DoesNotThrow(() => bareExecutor.Execute(firebolt, Request(SourceHandle.FromEntity(wizard.Id), wizard.Id)));

            Assert.IsTrue(messages.Any(d => d.Contains("no projectile delivery")));
        }
    }
}
