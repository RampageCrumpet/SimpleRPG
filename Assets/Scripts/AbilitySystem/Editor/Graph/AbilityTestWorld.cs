using SimpleRPG.Gameplay;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using EntityId = SimpleRPG.Gameplay.EntityId;

namespace SimpleRPG.Abilities.Editor
{
    /// <summary>
    /// What a scripted projectile does when it is launched.
    /// </summary>
    public enum ProjectileScript
    {
        /// <summary>It hits the first target in the world.</summary>
        HitFirstTarget = 0,

        /// <summary>It hits the ground and nothing else.</summary>
        HitWorld = 1,

        /// <summary>It flies on and ends without hitting anything.</summary>
        Expire = 2,
    }

    /// <summary>
    /// A combatant that lives only in the test world.
    /// </summary>
    public sealed class TestWorldCombatant : ICombatant
    {
        public TestWorldCombatant(ulong id, string name, int health, Position3 position)
        {
            Id = new EntityId(id);
            Name = name;
            StartingHealth = health;
            Health = health;
            IsAlive = true;
            LifeId = 1;
            Position = position;
        }

        public string Name { get; }

        public int StartingHealth { get; }

        public EntityId Id { get; }

        public int Health { get; private set; }

        public bool IsAlive { get; private set; }

        public int LifeId { get; private set; }

        public Position3 Position { get; set; }

        public void CommitHealth(int health) => Health = health;

        public void CommitDeath() => IsAlive = false;

        public void CommitRevive(int health)
        {
            Health = health;
            IsAlive = true;
            LifeId++;
        }
    }

    /// <summary>
    /// What happened when an ability was run in the test world.
    /// </summary>
    public sealed class TestWorldRun
    {
        internal TestWorldRun(AbilityDefinition ability, CompileResult compiled)
        {
            Ability = ability;
            Compiled = compiled;
        }

        public AbilityDefinition Ability { get; }

        public CompileResult Compiled { get; }

        /// <summary>True if the ability compiled and was started.</summary>
        public bool Ran => Compiled.Success;

        /// <summary>The run's ID in the world's trace, valid only if the ability ran.</summary>
        public ExecutionId Execution { get; internal set; }

        /// <summary>False if the run was stopped by a limit.</summary>
        public bool Completed { get; internal set; }

        /// <summary>Health each combatant lost during the run, by name. Healing shows as a negative number.</summary>
        public Dictionary<string, int> HealthLost { get; } = new Dictionary<string, int>();

        /// <summary>Entities the ability spawned.</summary>
        public int Spawned { get; internal set; }

        /// <summary>Projectiles launched.</summary>
        public int ProjectilesLaunched { get; internal set; }

        /// <summary>Melee swings started.</summary>
        public int SwingsStarted { get; internal set; }

        /// <summary>What the executor reported when something could not be done.</summary>
        public List<string> Problems { get; } = new List<string>();

        /// <summary>A one-line description for the editor.</summary>
        public string Summary
        {
            get
            {
                if (!Ran) return "Not run: " + Compiled.Report.Errors.Count() + (Compiled.Report.Errors.Count() == 1 ? " error" : " errors") + " to fix first.";

                List<string> parts = new List<string>();
                foreach (KeyValuePair<string, int> pair in HealthLost.Where(p => p.Value != 0))
                {
                    parts.Add(pair.Key + (pair.Value > 0 ? " lost " : " gained ") + System.Math.Abs(pair.Value) + " health");
                }

                if (Spawned > 0) parts.Add(Spawned + " spawned");
                if (parts.Count == 0) parts.Add("nothing happened to anyone");
                if (!Completed) parts.Add("stopped by a limit");
                if (Problems.Count > 0) parts.Add(Problems.Count + (Problems.Count == 1 ? " problem" : " problems"));
                return "Test world: " + string.Join(", ", parts) + ".";
            }
        }
    }

    /// <summary>
    /// A small scripted world for running an ability without starting the game. It is the real thing apart from the world itself: the real
    /// executor, combat resolver, attribute sets and event dispatcher, with combatants, projectiles and swings that live in memory. It
    /// reports to a <see cref="RecordingTrace"/> that the editor shows on the canvas. Randomness is seeded, so a run repeats exactly.
    /// </summary>
    public sealed class AbilityTestWorld
    {
        /// <summary>The caster's entity ID. Targets follow it: 2, 3, 4 ...</summary>
        public const ulong CasterId = 1;

        private readonly CombatantRegistry combatants = new CombatantRegistry();

        private readonly AttributeSetRegistry attributes = new AttributeSetRegistry();

        private readonly List<TestWorldCombatant> targets = new List<TestWorldCombatant>();

        private readonly Queue<(ProjectileLaunch launch, IProjectileEvents events)> launches = new Queue<(ProjectileLaunch, IProjectileEvents)>();

        private readonly Queue<(MeleeSwingLaunch launch, IMeleeEvents events)> swings = new Queue<(MeleeSwingLaunch, IMeleeEvents)>();

        private readonly AttributeSet casterAttributes = new AttributeSet();

        private readonly ExecutionServices services;

        private readonly AbilityExecutor executor;

        private TestWorldRun current;

        /// <param name="targetCount">How many dummies stand in front of the caster, one unit apart.</param>
        /// <param name="targetHealth">Each dummy's health, high enough to survive most tests of an ability.</param>
        /// <param name="casterHealth">The caster's health.</param>
        /// <param name="seed">Seed for chance rolls.</param>
        public AbilityTestWorld(int targetCount = 1, int targetHealth = 500, int casterHealth = 100, int seed = 1)
        {
            GameplayEventDispatcher events = new GameplayEventDispatcher();
            CombatResolver combat = new CombatResolver(events, combatants);

            Caster = new TestWorldCombatant(CasterId, "Caster", casterHealth, new Position3(0f, 0f, 0f));
            combatants.Register(Caster);
            for (int i = 0; i < targetCount; i++)
            {
                TestWorldCombatant dummy = new TestWorldCombatant(CasterId + 1 + (ulong)i, "Target " + (i + 1), targetHealth, new Position3(i, 0f, 5f));
                targets.Add(dummy);
                combatants.Register(dummy);
            }

            foreach (AttributeId primary in PrimaryAttributes.All)
            {
                casterAttributes.SetBase(primary, 10d);
            }

            attributes.Register(SourceHandle.FromEntity(Caster.Id), casterAttributes);

            services = new ExecutionServices(attributes, combat, new ScriptedProjectileDelivery(this), new ScriptedMeleeDelivery(this))
            {
                Targets = new CombatantTargetQuery(combatants),
                Random = new SeededRandom(seed),
                Spawner = new CountingSpawner(this),
                Trace = Trace,
            };
            services.Diagnostic += message => current?.Problems.Add(message);
            executor = new AbilityExecutor(services);
        }

        /// <summary>Everything the executor does in this world, for the canvas to show.</summary>
        public RecordingTrace Trace { get; } = new RecordingTrace();

        public TestWorldCombatant Caster { get; }

        public IReadOnlyList<TestWorldCombatant> Targets => targets;

        /// <summary>What projectiles do. Defaults to hitting the first target.</summary>
        public ProjectileScript Projectiles { get; set; } = ProjectileScript.HitFirstTarget;

        /// <summary>Whether a melee swing hits the first target. A miss still completes the swing.</summary>
        public bool SwingsHit { get; set; } = true;

        /// <summary>The weapon's base damage, as a melee hit reports it.</summary>
        public float WeaponDamage { get; set; } = 10f;

        /// <summary>The combo attack number a melee hit reports.</summary>
        public int AttackNumber { get; set; } = 1;

        /// <summary>The combo multiplier a melee hit reports.</summary>
        public float ComboScalar { get; set; } = 1f;

        /// <summary>
        /// Gives the caster an attribute, for abilities that read one. The six primary attributes start at 10.
        /// </summary>
        public void SetCasterAttribute(string key, double value) => casterAttributes.SetBase(new AttributeId(key), value);

        /// <summary>
        /// Compiles an ability and runs it as the caster, then plays out every projectile and swing it started. A graph with errors is not run.
        /// </summary>
        public TestWorldRun Run(AbilityDefinition ability)
        {
            CompileResult compiled = ability.Compile();
            TestWorldRun run = new TestWorldRun(ability, compiled);
            if (!compiled.Success)
            {
                return run;
            }

            Dictionary<string, int> before = Everyone().ToDictionary(c => c.Name, c => c.Health);
            current = run;

            ExecutionRequest request = new ExecutionRequest(
                SourceHandle.FromEntity(Caster.Id),
                Caster.Id,
                new Position3(0f, 1f, 0f),
                new Position3(0f, 0f, 1f));
            ExecutionResult result = executor.Execute(compiled.Ability, request);
            run.Execution = result.Id;
            run.Completed = result.Completed;

            // Deliveries report after the executor has finished its step, never in the middle of it. One delivery's report can start another.
            for (int guard = 0; guard < 64 && (launches.Count > 0 || swings.Count > 0); guard++)
            {
                if (launches.Count > 0)
                {
                    PlayOutProjectile(launches.Dequeue());
                }

                if (swings.Count > 0)
                {
                    PlayOutSwing(swings.Dequeue());
                }
            }

            foreach (TestWorldCombatant combatant in Everyone())
            {
                run.HealthLost[combatant.Name] = before[combatant.Name] - combatant.Health;
            }

            current = null;
            return run;
        }

        private IEnumerable<TestWorldCombatant> Everyone() => new[] { Caster }.Concat(targets);

        private void PlayOutProjectile((ProjectileLaunch launch, IProjectileEvents events) item)
        {
            switch (Projectiles)
            {
                case ProjectileScript.HitFirstTarget when targets.Count > 0:
                    TestWorldCombatant victim = targets[0];
                    item.events.Impact(item.launch.Token, new ProjectileImpact(victim.Id, 0, victim.Position, hitWorld: false));
                    break;
                case ProjectileScript.Expire:
                    item.events.Expired(item.launch.Token);
                    break;
                default:
                    item.events.Impact(item.launch.Token, new ProjectileImpact(default, 0, new Position3(0f, 0f, 5f), hitWorld: true));
                    break;
            }
        }

        private void PlayOutSwing((MeleeSwingLaunch launch, IMeleeEvents events) item)
        {
            if (SwingsHit && targets.Count > 0)
            {
                item.events.Hit(item.launch.Token, new MeleeHit(targets[0].Id, 1, AttackNumber, ComboScalar, WeaponDamage));
            }

            item.events.Completed(item.launch.Token);
        }

        private sealed class ScriptedProjectileDelivery : IProjectileDelivery
        {
            private readonly AbilityTestWorld world;

            public ScriptedProjectileDelivery(AbilityTestWorld world) => this.world = world;

            public bool Launch(in ProjectileLaunch launch, IProjectileEvents events)
            {
                world.launches.Enqueue((launch, events));
                if (world.current != null) world.current.ProjectilesLaunched++;
                return true;
            }
        }

        private sealed class ScriptedMeleeDelivery : IMeleeDelivery
        {
            private readonly AbilityTestWorld world;

            public ScriptedMeleeDelivery(AbilityTestWorld world) => this.world = world;

            public bool Begin(in MeleeSwingLaunch launch, IMeleeEvents events)
            {
                world.swings.Enqueue((launch, events));
                if (world.current != null) world.current.SwingsStarted++;
                return true;
            }
        }

        private sealed class CountingSpawner : ISpawnService
        {
            private readonly AbilityTestWorld world;

            public CountingSpawner(AbilityTestWorld world) => this.world = world;

            public bool Spawn(in SpawnRequest request)
            {
                if (world.current != null) world.current.Spawned++;
                return true;
            }
        }
    }
}
