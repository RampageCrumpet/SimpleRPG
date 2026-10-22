using NUnit.Framework;
using SimpleRPG.Gameplay;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using EntityId = SimpleRPG.Gameplay.EntityId;

namespace SimpleRPG.Abilities.Tests.EditMode
{
    /// <summary>
    /// Runs the melee ability with the real executor, combat resolver and event dispatcher. Only the swing itself (the weapon and its animation)
    /// is scripted; the real melee mechanics are covered by the PlayMode tests.
    /// </summary>
    public class MeleeExecutorTests
    {
        private readonly List<Object> created = new List<Object>();

        private GameplayEventDispatcher events;
        private CombatantRegistry combatants;
        private CombatResolver combat;
        private ScriptedMelee melee;
        private ExecutionServices services;
        private AbilityExecutor executor;
        private List<string> diagnostics;
        private CompiledAbility ability;

        private TestCombatant attacker;
        private TestCombatant victim;
        private TestCombatant bystander;

        [SetUp]
        public void SetUp()
        {
            events = new GameplayEventDispatcher();
            combatants = new CombatantRegistry();
            combat = new CombatResolver(events, combatants);
            melee = new ScriptedMelee();
            services = new ExecutionServices(new AttributeSetRegistry(), combat, null, melee);
            executor = new AbilityExecutor(services);
            diagnostics = new List<string>();
            services.Diagnostic += diagnostics.Add;

            attacker = Add(1, 100);
            victim = Add(2, 100);
            bystander = Add(3, 100);

            AbilityDefinition definition = AbilityBuilder.MeleeAttack();
            created.Add(definition);
            CompileResult result = definition.Compile();
            Assert.IsTrue(result.Success, result.Report.ToString());
            ability = result.Ability;
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

        private ExecutionResult Swing() =>
            executor.Execute(ability, new ExecutionRequest(SourceHandle.FromEntity(attacker.Id), attacker.Id, default, new Position3(0, 0, 1)));

        [Test]
        public void MeleeAbility_Compiles_AndHasTheSwingDamageChain()
        {
            Assert.AreEqual(3, ability.Operations.Length, "Activate, Melee Swing and Damage.");
        }

        [Test]
        public void Execute_StartsOneSwing_AndDealsNoDamageUntilItHits()
        {
            ExecutionResult result = Swing();

            Assert.IsTrue(result.Completed);
            Assert.AreEqual(1, melee.Swings.Count);
            Assert.AreEqual(1, executor.PendingContinuations);
            Assert.AreEqual(100, victim.Health);
        }

        [Test]
        public void TheSwing_CarriesTheSourceAndInstigator()
        {
            Swing();

            MeleeSwingLaunch launch = melee.Swings[0].Launch;
            Assert.AreEqual(SourceHandle.FromEntity(attacker.Id), launch.Source);
            Assert.AreEqual(attacker.Id, launch.Instigator);
            Assert.IsTrue(launch.Token.IsValid);
            Assert.IsTrue(launch.Execution.IsValid);
        }

        [TestCase(10f, 1.00f, 10)]
        [TestCase(10f, 0.79f, 8)]
        [TestCase(10f, 0.84f, 8)]
        [TestCase(1f, 0.79f, 1)]
        [TestCase(25f, 1.00f, 25)]
        public void Hit_DealsTheWeaponsDamageScaledByTheAttack(float weaponDamage, float scalar, int expected)
        {
            Swing();

            melee.Hit(0, victim.Id, comboScalar: scalar, weaponDamage: weaponDamage);

            Assert.AreEqual(100 - expected, victim.Health);
        }

        [Test]
        public void HitsAcrossAttacks_Accumulate()
        {
            Swing();
            melee.Hit(0, victim.Id, attackNumber: 1, comboScalar: 1.00f);
            Swing();
            melee.Hit(1, victim.Id, attackNumber: 3, comboScalar: 0.84f);

            Assert.AreEqual(100 - 10 - 8, victim.Health);
        }

        [Test]
        public void OneSwing_CanHitSeveralTargets()
        {
            Swing();

            melee.Hit(0, victim.Id);
            melee.Hit(0, bystander.Id);

            Assert.AreEqual(90, victim.Health);
            Assert.AreEqual(90, bystander.Health);
            Assert.AreEqual(1, executor.PendingContinuations, "The swing stays open until it completes.");
        }

        [Test]
        public void ASwingsHit_CarriesTheBodyLocation()
        {
            DamageRequest seen = default;
            events.Subscribe<DamageResolvedEvent>(e => seen = e.Request);
            Swing();

            melee.Hit(0, victim.Id, bodyLocation: 3);

            Assert.AreEqual(3, seen.BodyLocation);
        }

        [Test]
        public void MeleeDamage_IsADirectWeaponHit_AttributedToTheAttacker()
        {
            DamageRequest seen = default;
            events.Subscribe<DamageResolvedEvent>(e => seen = e.Request);
            Swing();

            melee.Hit(0, victim.Id);

            Assert.AreEqual(DamageProvenance.DirectWeaponHit, seen.Provenance);
            Assert.AreEqual(attacker.Id, seen.Instigator);
            Assert.AreEqual(SourceHandle.FromEntity(attacker.Id), seen.Source);
        }

        [Test]
        public void TheKillingBlow_CreditsTheAttacker()
        {
            victim = Add(10, 5);
            DeathContext death = null;
            events.Subscribe<EntityDiedEvent>(e => death = e.Context);
            Swing();

            melee.Hit(0, victim.Id);

            Assert.AreEqual(attacker.Id, death.Killer);
            Assert.AreEqual(DamageProvenance.DirectWeaponHit, death.KillingProvenance);
        }

        [Test]
        public void ACompletedSwing_ReleasesItsContinuation_AndLaterHitsAreIgnored()
        {
            Swing();

            melee.Complete(0);
            melee.Hit(0, victim.Id);

            Assert.AreEqual(0, executor.PendingContinuations);
            Assert.AreEqual(100, victim.Health);
        }

        [Test]
        public void CompletingASwingTwice_IsHarmless()
        {
            Swing();

            melee.Complete(0);

            Assert.DoesNotThrow(() => melee.Complete(0));
        }

        [Test]
        public void TwoSwingsInProgress_AreIndependent()
        {
            Swing();
            Swing();

            melee.Complete(0);
            melee.Hit(1, victim.Id);

            Assert.AreEqual(1, executor.PendingContinuations);
            Assert.AreEqual(90, victim.Health, "The second swing is still open and lands its hit.");
        }

        [Test]
        public void ARefusedSwing_IsReported_AndLeavesNothingPending()
        {
            melee.Accepts = false;

            Swing();

            Assert.AreEqual(0, executor.PendingContinuations);
            Assert.IsTrue(diagnostics.Any(d => d.Contains("could not start")), string.Join("\n", diagnostics));
        }

        [Test]
        public void NoMeleeDelivery_IsReported_NotThrown()
        {
            ExecutionServices bare = new ExecutionServices(new AttributeSetRegistry(), combat, null);
            AbilityExecutor bareExecutor = new AbilityExecutor(bare);
            List<string> messages = new List<string>();
            bare.Diagnostic += messages.Add;

            Assert.DoesNotThrow(() => bareExecutor.Execute(ability, new ExecutionRequest(SourceHandle.FromEntity(attacker.Id), attacker.Id, default, default)));

            Assert.IsTrue(messages.Any(d => d.Contains("no melee delivery")));
        }

        [Test]
        public void ASwingWithNoInstigator_IsRefused_BecauseThereIsNoOneToSwing()
        {
            executor.Execute(ability, new ExecutionRequest(SourceHandle.FromWorldObject(5), default, default, default));

            Assert.AreEqual(0, melee.Swings.Count);
            Assert.IsTrue(diagnostics.Any(d => d.Contains("needs an instigator")), string.Join("\n", diagnostics));
        }

        [Test]
        public void AHitOnADeadTarget_DealsNothing()
        {
            Swing();
            combat.Apply(new DamageRequest(victim.Id, 1000));

            melee.Hit(0, victim.Id);

            Assert.IsEmpty(diagnostics);
            Assert.AreEqual(0, victim.Health);
        }

        [Test]
        public void ZeroWeaponDamage_DealsNothing()
        {
            Swing();

            melee.Hit(0, victim.Id, weaponDamage: 0f);

            Assert.AreEqual(100, victim.Health);
        }

        [Test]
        public void MeleeEventData_IsRejectedOutsideAHit()
        {
            AbilityDefinition definition = AbilityBuilder.Empty("Misplaced");
            created.Add(definition);
            string damage = definition.AddNode(NodeTypes.Damage, null, "damage");
            string weaponDamage = definition.AddNode(NodeTypes.EventWeaponDamage, null, "weaponDamage");
            string victimNode = definition.AddNode(NodeTypes.EventVictim, null, "victim");
            definition.Connect("activate", BuiltInNodes.Next, damage, BuiltInNodes.In);
            definition.Connect(weaponDamage, BuiltInNodes.Value, damage, BuiltInNodes.Amount);
            definition.Connect(victimNode, BuiltInNodes.Value, damage, BuiltInNodes.Target);

            CompileResult result = definition.Compile();

            Assert.IsFalse(result.Success);
            Assert.IsTrue(result.Report.Errors.Any(e => e.NodeId == "weaponDamage" && e.Text.Contains("only available after an impact or hit")), result.Report.ToString());
        }

        [Test]
        public void TheMeleeNodes_AreInThePalette()
        {
            Assert.IsTrue(AbilityNodeRegistry.Default.Search("melee").Any(d => d.TypeId == NodeTypes.MeleeSwing));
            Assert.IsTrue(AbilityNodeRegistry.Default.Search("combo").Any(d => d.TypeId == NodeTypes.EventComboScalar));
            Assert.IsTrue(AbilityNodeRegistry.Default.Search("weapon").Any(d => d.TypeId == NodeTypes.EventWeaponDamage));
        }

        [Test]
        public void ARunningSwing_CountsAgainstTheOperationBudget()
        {
            services.MaxOperationsPerExecution = 3;
            Swing();

            melee.Hit(0, victim.Id);
            melee.Hit(0, bystander.Id);

            // Activate and the swing took two operations, the first hit's damage the third; the second hit has no budget left.
            Assert.AreEqual(90, victim.Health);
            Assert.AreEqual(100, bystander.Health);
            Assert.IsTrue(diagnostics.Any(d => d.Contains("limit")), string.Join("\n", diagnostics));
        }
    }
}
