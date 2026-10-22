using NUnit.Framework;
using SimpleRPG.Gameplay;
using System;
using System.Collections.Generic;

namespace SimpleRPG.Abilities.Tests.EditMode
{
    /// <summary>
    /// Tests the damage and death pipeline with the real resolver and dispatcher. <see cref="Combatant"/> is a plain in-memory implementation of the
    /// game's combatant interface, standing in only for the networked character.
    /// </summary>
    public class CombatPipelineTests
    {
        private sealed class Combatant : ICombatant
        {
            public Combatant(ulong id, int health, Position3 position = default)
            {
                Id = new EntityId(id);
                Health = health;
                IsAlive = true;
                LifeId = 1;
                Position = position;
            }

            public EntityId Id { get; }

            public int Health { get; private set; }

            public bool IsAlive { get; private set; }

            public int LifeId { get; private set; }

            public Position3 Position { get; set; }

            public int DeathCommits { get; private set; }

            public void CommitHealth(int health) => Health = health;

            public void CommitDeath()
            {
                IsAlive = false;
                DeathCommits++;
            }

            public void CommitRevive(int health)
            {
                Health = health;
                IsAlive = true;
                LifeId++;
            }
        }

        private sealed class FixedSources : IDeathSourceSnapshotter
        {
            private readonly SourceHandle[] sources;

            public FixedSources(params SourceHandle[] sources) => this.sources = sources;

            public void Capture(EntityId victim, List<SourceHandle> into) => into.AddRange(sources);
        }

        private sealed class Preventer : IFatalDamageInterceptor
        {
            public FatalDamageDecision Intercept(ICombatant target, in DamageRequest request) => FatalDamageDecision.Prevent;
        }

        private sealed class Ping : GameplayEvent
        {
        }

        private GameplayEventDispatcher events;
        private CombatantRegistry registry;
        private CombatResolver combat;
        private List<string> diagnostics;
        private List<string> log;

        [SetUp]
        public void SetUp()
        {
            events = new GameplayEventDispatcher();
            registry = new CombatantRegistry();
            combat = new CombatResolver(events, registry);
            diagnostics = new List<string>();
            log = new List<string>();
            events.Diagnostic += diagnostics.Add;
        }

        private Combatant Add(ulong id, int health, Position3 position = default)
        {
            Combatant combatant = new Combatant(id, health, position);
            registry.Register(combatant);
            return combatant;
        }

        private static DamageRequest Hit(Combatant target, int amount, Combatant by = null, DamageProvenance provenance = DamageProvenance.None) =>
            new DamageRequest(target.Id, amount, by != null ? by.Id : default, provenance: provenance);

        [Test]
        public void NonLethalDamage_LowersHealth_AndRaisesOnlyDamageResolved()
        {
            Combatant target = Add(1, 100);
            List<GameplayEvent> seen = new List<GameplayEvent>();
            events.Subscribe<DamageResolvedEvent>(seen.Add);
            events.Subscribe<EntityDiedEvent>(seen.Add);

            DamageOutcome outcome = combat.Apply(Hit(target, 30));

            Assert.AreEqual(DamageResult.Applied, outcome.Result);
            Assert.AreEqual(70, target.Health);
            Assert.IsTrue(target.IsAlive);
            Assert.AreEqual(1, seen.Count);
            DamageResolvedEvent resolved = (DamageResolvedEvent)seen[0];
            Assert.AreEqual(100, resolved.HealthBefore);
            Assert.AreEqual(70, resolved.HealthAfter);
            Assert.IsFalse(resolved.Fatal);
        }

        [Test]
        public void LethalDamage_KillsOnce_AndClampsHealthToZero()
        {
            Combatant target = Add(1, 10);

            DamageOutcome outcome = combat.Apply(Hit(target, 50));

            Assert.AreEqual(DamageResult.Killed, outcome.Result);
            Assert.AreEqual(0, target.Health);
            Assert.IsFalse(target.IsAlive);
            Assert.AreEqual(1, target.DeathCommits);
        }

        [Test]
        public void Death_RaisesDamageResolved_ThenEntityDied_ThenFinalize_InThatOrder()
        {
            Combatant target = Add(1, 10);
            events.Subscribe<DamageResolvedEvent>(e => log.Add("resolved"));
            events.Subscribe<EntityDiedEvent>(e => log.Add("died"));
            events.Subscribe<DeathFinalizeEvent>(e => log.Add("finalize"));

            combat.Apply(Hit(target, 10));

            CollectionAssert.AreEqual(new[] { "resolved", "died", "finalize" }, log);
        }

        [Test]
        public void EntityDied_CarriesTheCapturedContext()
        {
            Combatant killer = Add(2, 100);
            Combatant victim = Add(1, 10, new Position3(4, 5, 6));
            SourceHandle amulet = SourceHandle.FromItem(new ItemInstanceId(77));
            combat.DeathSources = new FixedSources(amulet);
            DeathContext context = null;
            events.Subscribe<EntityDiedEvent>(e => context = e.Context);

            combat.Apply(new DamageRequest(victim.Id, 10, killer.Id, provenance: DamageProvenance.Spell));

            Assert.AreEqual(new DeathTransitionId(victim.Id, 1), context.Transition);
            Assert.AreEqual(victim.Id, context.Victim);
            Assert.AreEqual(killer.Id, context.Killer);
            Assert.AreEqual(DamageProvenance.Spell, context.KillingProvenance);
            Assert.AreEqual(4f, context.Position.X);
            Assert.AreEqual(5f, context.Position.Y);
            Assert.AreEqual(6f, context.Position.Z);
            CollectionAssert.AreEqual(new[] { amulet }, context.QualifyingSources);
        }

        [Test]
        public void DamageToTheDead_IsRejected_AndNeverRaisesASecondDeath()
        {
            Combatant target = Add(1, 10);
            int deaths = 0;
            events.Subscribe<EntityDiedEvent>(e => deaths++);

            combat.Apply(Hit(target, 10));
            DamageOutcome second = combat.Apply(Hit(target, 10));

            Assert.AreEqual(DamageResult.RejectedTargetDead, second.Result);
            Assert.AreEqual(1, deaths);
            Assert.AreEqual(1, target.DeathCommits);
        }

        [Test]
        public void TwoFatalHitsInOneBurst_OnlyOneKills()
        {
            Combatant target = Add(1, 10);
            int deaths = 0;
            events.Subscribe<EntityDiedEvent>(e => deaths++);

            // The second fatal hit arrives while the first one's events are being delivered.
            events.Subscribe<DamageResolvedEvent>(e =>
            {
                if (e.Fatal) combat.Apply(Hit(target, 10));
            });

            combat.Apply(Hit(target, 10));

            Assert.AreEqual(1, deaths);
            Assert.AreEqual(1, target.DeathCommits);
        }

        [Test]
        public void ReadingZeroHealthRepeatedly_DoesNotRaiseDeath()
        {
            Combatant target = Add(1, 10);
            int deaths = 0;
            events.Subscribe<EntityDiedEvent>(e => deaths++);
            combat.Apply(Hit(target, 10));

            for (int i = 0; i < 20; i++)
            {
                combat.Apply(Hit(target, 1));
                events.Drain();
            }

            Assert.AreEqual(1, deaths);
        }

        [Test]
        public void ReactionsGetTheSnapshot_AndCleanupRunsAfterThem()
        {
            Combatant victim = Add(1, 10);
            SourceHandle amulet = SourceHandle.FromItem(new ItemInstanceId(77));
            combat.DeathSources = new FixedSources(amulet);
            bool amuletEquipped = true;

            events.Subscribe<EntityDiedEvent>(e =>
            {
                log.Add("reaction sees " + e.Context.QualifyingSources.Count + " source, equipped=" + amuletEquipped);
            });
            events.Subscribe<DeathFinalizeEvent>(e =>
            {
                amuletEquipped = false;
                log.Add("cleanup");
            });

            combat.Apply(Hit(victim, 10));

            CollectionAssert.AreEqual(new[] { "reaction sees 1 source, equipped=True", "cleanup" }, log);
        }

        [Test]
        public void DamageFromAHandler_WaitsItsTurn_InsteadOfRunningInsideTheHandler()
        {
            Combatant first = Add(1, 10);
            Combatant second = Add(2, 10);
            bool insideFirstDeath = false;

            events.Subscribe<EntityDiedEvent>(e =>
            {
                if (e.Context.Victim == first.Id)
                {
                    insideFirstDeath = true;
                    DamageOutcome outcome = combat.Apply(Hit(second, 10));
                    Assert.AreEqual(DamageResult.Killed, outcome.Result, "The second target's health is committed immediately.");
                    log.Add("first handler done");
                    insideFirstDeath = false;
                }
                else
                {
                    log.Add("second died, insideFirstDeath=" + insideFirstDeath);
                }
            });

            combat.Apply(Hit(first, 10));

            CollectionAssert.AreEqual(new[] { "first handler done", "second died, insideFirstDeath=False" }, log);
            Assert.IsEmpty(diagnostics, "The assertion inside the handler must not have failed.");
        }

        [Test]
        public void CausedEvents_AreOneDeeperThanTheirCause()
        {
            Combatant first = Add(1, 10);
            Combatant second = Add(2, 10);
            List<int> depths = new List<int>();

            events.Subscribe<EntityDiedEvent>(e =>
            {
                depths.Add(e.Depth);
                if (e.Context.Victim == first.Id) combat.Apply(Hit(second, 10));
            });

            combat.Apply(Hit(first, 10));

            CollectionAssert.AreEqual(new[] { 0, 1 }, depths);
        }

        [Test]
        public void ACauseChainPastTheDepthLimit_IsRefused_WithoutCorruptingState()
        {
            // Each death's reaction damages the next entity, so every link is one event deeper.
            List<Combatant> chain = new List<Combatant>();
            for (ulong i = 1; i <= 20; i++) chain.Add(Add(i, 5));

            List<DamageResult> results = new List<DamageResult>();
            events.Subscribe<EntityDiedEvent>(e =>
            {
                int next = chain.FindIndex(c => c.Id == e.Context.Victim) + 1;
                if (next < chain.Count) results.Add(combat.Apply(Hit(chain[next], 5)).Result);
            });

            combat.Apply(Hit(chain[0], 5));

            Assert.Contains(DamageResult.RejectedTooDeep, results);
            int dead = chain.FindAll(c => !c.IsAlive).Count;
            Assert.AreEqual(events.MaxDepth + 1, dead, "Exactly the entities within the depth limit die.");
            foreach (Combatant c in chain) Assert.LessOrEqual(c.DeathCommits, 1);
            Assert.AreEqual(0, events.PendingCount);
        }

        [Test]
        public void ARunawayEventLoop_StopsAtTheEventBudget()
        {
            GameplayEventDispatcher small = new GameplayEventDispatcher(maxEventsPerDrain: 10, maxDepth: 1000);
            small.Diagnostic += diagnostics.Add;
            int delivered = 0;
            small.Subscribe<Ping>(e =>
            {
                delivered++;
                small.Enqueue(new Ping());
            });
            small.Enqueue(new Ping());

            DrainResult result = small.Drain();

            Assert.IsTrue(result.BudgetExceeded);
            Assert.AreEqual(10, delivered);
            Assert.AreEqual(0, small.PendingCount);
            Assert.IsTrue(diagnostics.Exists(d => d.Contains("budget")));
        }

        [Test]
        public void ThrowingHandler_IsReported_AndDoesNotStopTheOthers()
        {
            Combatant target = Add(1, 10);
            events.Subscribe<EntityDiedEvent>(e => throw new InvalidOperationException("boom"));
            events.Subscribe<EntityDiedEvent>(e => log.Add("second handler"));

            combat.Apply(Hit(target, 10));

            CollectionAssert.AreEqual(new[] { "second handler" }, log);
            Assert.IsTrue(diagnostics.Exists(d => d.Contains("boom")));
        }

        [Test]
        public void Unsubscribe_StopsDelivery()
        {
            Combatant target = Add(1, 10);
            SubscriptionHandle handle = events.Subscribe<EntityDiedEvent>(e => log.Add("died"));

            Assert.IsTrue(events.Unsubscribe(handle));
            Assert.IsFalse(events.Unsubscribe(handle));
            combat.Apply(Hit(target, 10));

            Assert.IsEmpty(log);
        }

        [Test]
        public void Revive_StartsANewLife_ThatCanDieAgainWithANewTransition()
        {
            Combatant target = Add(1, 10);
            List<DeathTransitionId> deaths = new List<DeathTransitionId>();
            events.Subscribe<EntityDiedEvent>(e => deaths.Add(e.Context.Transition));

            combat.Apply(Hit(target, 10));
            Assert.IsTrue(combat.Revive(target.Id, 20));
            Assert.IsTrue(target.IsAlive);
            Assert.AreEqual(2, target.LifeId);
            combat.Apply(Hit(target, 20));

            Assert.AreEqual(2, deaths.Count);
            Assert.AreNotEqual(deaths[0], deaths[1]);
            Assert.AreEqual(new DeathTransitionId(target.Id, 2), deaths[1]);
        }

        [Test]
        public void Revive_OfALivingEntityOrAnUnknownOne_ReportsFalse()
        {
            Combatant target = Add(1, 10);

            Assert.IsFalse(combat.Revive(target.Id, 5));
            Assert.IsFalse(combat.Revive(new EntityId(99), 5));
            Assert.Throws<ArgumentOutOfRangeException>(() => combat.Revive(target.Id, 0));
        }

        [Test]
        public void FatalInterceptor_CanPreventTheDeath()
        {
            Combatant target = Add(1, 10);
            combat.AddFatalInterceptor(new Preventer());
            int deaths = 0;
            events.Subscribe<EntityDiedEvent>(e => deaths++);

            DamageOutcome outcome = combat.Apply(Hit(target, 50));

            Assert.AreEqual(DamageResult.Prevented, outcome.Result);
            Assert.AreEqual(1, target.Health);
            Assert.IsTrue(target.IsAlive);
            Assert.AreEqual(0, deaths);
        }

        [Test]
        public void Interceptor_IsNotConsulted_ForNonLethalDamage()
        {
            Combatant target = Add(1, 10);
            combat.AddFatalInterceptor(new Preventer());

            DamageOutcome outcome = combat.Apply(Hit(target, 3));

            Assert.AreEqual(DamageResult.Applied, outcome.Result);
            Assert.AreEqual(7, target.Health);
        }

        [TestCase(0)]
        [TestCase(-5)]
        public void NoDamageAmount_IsRejected(int amount)
        {
            Combatant target = Add(1, 10);

            DamageOutcome outcome = combat.Apply(Hit(target, amount));

            Assert.AreEqual(DamageResult.RejectedNoDamage, outcome.Result);
            Assert.AreEqual(10, target.Health);
        }

        [Test]
        public void UnknownTarget_IsRejected()
        {
            DamageOutcome outcome = combat.Apply(new DamageRequest(new EntityId(404), 5));

            Assert.AreEqual(DamageResult.RejectedUnknownTarget, outcome.Result);
        }

        [Test]
        public void Provenance_ReachesTheDamageResolvedEvent()
        {
            Combatant target = Add(1, 100);
            DamageProvenance seen = DamageProvenance.None;
            events.Subscribe<DamageResolvedEvent>(e => seen = e.Request.Provenance);

            combat.Apply(Hit(target, 5, provenance: DamageProvenance.DirectWeaponHit));

            Assert.AreEqual(DamageProvenance.DirectWeaponHit, seen);
        }

        [Test]
        public void Registry_Unregister_IgnoresAStaleHolderOfTheSameId()
        {
            Combatant old = new Combatant(1, 10);
            Combatant replacement = new Combatant(1, 10);
            registry.Register(old);
            registry.Register(replacement);

            registry.Unregister(old);

            Assert.IsTrue(registry.TryGet(new EntityId(1), out ICombatant found));
            Assert.AreSame(replacement, found);
        }
    }
}
