using SimpleRPG.Gameplay;
using System;
using System.Collections.Generic;
using UnityEngine;
using EntityId = SimpleRPG.Gameplay.EntityId;

namespace SimpleRPG.Abilities
{
    /// <summary>
    /// Identifies a suspended execution waiting for an event from a delivery, such as a projectile impact. Zero is not a token.
    /// </summary>
    public readonly struct ContinuationToken : IEquatable<ContinuationToken>
    {
        public ContinuationToken(uint value)
        {
            Value = value;
        }

        public uint Value { get; }

        public bool IsValid => Value != 0;

        public bool Equals(ContinuationToken other) => Value == other.Value;

        public override bool Equals(object obj) => obj is ContinuationToken other && Equals(other);

        public override int GetHashCode() => Value.GetHashCode();

        public override string ToString() => "Continuation#" + Value;
    }

    /// <summary>
    /// The state of one branch of an execution: what the event that started the branch carried. A branch started by a projectile impact knows
    /// who was hit. Branches do not share this, so one branch's target never leaks into another.
    /// </summary>
    public readonly struct BranchFrame
    {
        public BranchFrame(EntityId eventVictim, int bodyLocation, Position3 eventPosition, bool hasImpact, int attackNumber = 0, float comboScalar = 1f, float weaponDamage = 0f, EntityId currentTarget = default, StatusInvocation status = default)
        {
            CurrentTarget = currentTarget;
            Status = status;
            EventVictim = eventVictim;
            BodyLocation = bodyLocation;
            EventPosition = eventPosition;
            HasImpact = hasImpact;
            AttackNumber = attackNumber;
            ComboScalar = comboScalar;
            WeaponDamage = weaponDamage;
        }

        /// <summary>For a branch of an ability a status started: the status holder, its strength and its stacks.</summary>
        public StatusInvocation Status { get; }

        /// <summary>For a branch started for one target of a set: that target.</summary>
        public EntityId CurrentTarget { get; }

        /// <summary>The same branch, carrying what a status told the ability that started it.</summary>
        public BranchFrame WithStatus(StatusInvocation status) =>
            new BranchFrame(EventVictim, BodyLocation, EventPosition, HasImpact, AttackNumber, ComboScalar, WeaponDamage, CurrentTarget, status);

        /// <summary>The same branch, for one target of a set.</summary>
        public BranchFrame WithTarget(EntityId target) =>
            new BranchFrame(EventVictim, BodyLocation, EventPosition, HasImpact, AttackNumber, ComboScalar, WeaponDamage, target, Status);

        /// <summary>For a branch started by a melee hit: which attack in the combo chain landed it.</summary>
        public int AttackNumber { get; }

        /// <summary>For a branch started by a melee hit: the damage multiplier of that attack.</summary>
        public float ComboScalar { get; }

        /// <summary>For a branch started by a melee hit: the base damage of the weapon that swung.</summary>
        public float WeaponDamage { get; }

        public EntityId EventVictim { get; }

        public int BodyLocation { get; }

        public Position3 EventPosition { get; }

        /// <summary>True for a branch that began at an impact.</summary>
        public bool HasImpact { get; }

        public static BranchFrame ForImpact(in ProjectileImpact impact) => new BranchFrame(impact.Victim, impact.BodyLocation, impact.Position, true);

        public static BranchFrame ForMelee(in MeleeHit hit) =>
            new BranchFrame(hit.Victim, hit.BodyLocation, default, true, hit.AttackNumber, hit.ComboScalar, hit.WeaponDamage);
    }

    /// <summary>
    /// A request to launch a projectile, made by the LaunchProjectile operation.
    /// </summary>
    public readonly struct ProjectileLaunch
    {
        public ProjectileLaunch(ContinuationToken token, ExecutionId execution, SourceHandle source, EntityId instigator, Position3 origin, Position3 direction, GameObject prefab)
        {
            Token = token;
            Execution = execution;
            Source = source;
            Instigator = instigator;
            Origin = origin;
            Direction = direction;
            Prefab = prefab;
        }

        public ContinuationToken Token { get; }

        public ExecutionId Execution { get; }

        public SourceHandle Source { get; }

        /// <summary>The entity whose projectile this is, so the projectile never hurts it. None for a hazard.</summary>
        public EntityId Instigator { get; }

        public Position3 Origin { get; }

        /// <summary>The direction to fire in. It need not be normalized.</summary>
        public Position3 Direction { get; }

        public GameObject Prefab { get; }
    }

    /// <summary>
    /// Receives what happens to a launched projectile. The executor implements it; the delivery calls it. Server only.
    /// </summary>
    public interface IProjectileEvents
    {
        /// <summary>The projectile hit something. Raised at most once per projectile.</summary>
        void Impact(ContinuationToken token, in ProjectileImpact impact);

        /// <summary>The projectile ended without hitting anything, for example it ran out of lifetime.</summary>
        void Expired(ContinuationToken token);
    }

    /// <summary>
    /// Launches projectiles and reports what happens to them. The game integration implements it with real projectiles.
    /// </summary>
    public interface IProjectileDelivery
    {
        /// <summary>
        /// Launches a projectile and arranges for <paramref name="events"/> to be told how it ends.
        /// </summary>
        /// <returns>False if the projectile could not be launched; nothing will be reported for it.</returns>
        bool Launch(in ProjectileLaunch launch, IProjectileEvents events);
    }

    /// <summary>
    /// What a status tells the ability it starts: who holds the status, how strong it is and how many stacks it has.
    /// </summary>
    public readonly struct StatusInvocation
    {
        public StatusInvocation(EntityId holder, double potency, int stacks)
        {
            Holder = holder;
            Potency = potency;
            Stacks = stacks;
        }

        public EntityId Holder { get; }

        public double Potency { get; }

        public int Stacks { get; }

        public bool IsValid => Holder.IsValid;
    }

    /// <summary>
    /// What a melee swing hit: who, where on the body, which attack in the combo chain landed it, and the numbers damage is built from.
    /// </summary>
    public readonly struct MeleeHit
    {
        public MeleeHit(EntityId victim, int bodyLocation, int attackNumber, float comboScalar, float weaponDamage)
        {
            Victim = victim;
            BodyLocation = bodyLocation;
            AttackNumber = attackNumber;
            ComboScalar = comboScalar;
            WeaponDamage = weaponDamage;
        }

        public EntityId Victim { get; }

        /// <summary>The body location hit, as the integer value of the game's body location enum.</summary>
        public int BodyLocation { get; }

        /// <summary>The attack in the combo chain that landed the hit (1, 2, 3...).</summary>
        public int AttackNumber { get; }

        /// <summary>The damage multiplier of that attack.</summary>
        public float ComboScalar { get; }

        /// <summary>The base damage of the weapon that swung.</summary>
        public float WeaponDamage { get; }
    }

    /// <summary>
    /// A request to start a melee swing, made by the MeleeSwing operation.
    /// </summary>
    public readonly struct MeleeSwingLaunch
    {
        public MeleeSwingLaunch(ContinuationToken token, ExecutionId execution, SourceHandle source, EntityId instigator)
        {
            Token = token;
            Execution = execution;
            Source = source;
            Instigator = instigator;
        }

        public ContinuationToken Token { get; }

        public ExecutionId Execution { get; }

        public SourceHandle Source { get; }

        /// <summary>The entity swinging. A swing needs one: it is the entity's own weapon and animation.</summary>
        public EntityId Instigator { get; }
    }

    /// <summary>
    /// Receives what happens during a melee swing. The executor implements it; the delivery calls it. Server only.
    /// </summary>
    public interface IMeleeEvents
    {
        /// <summary>The swing hit something. Raised once per target per swing, as many times as the swing hits.</summary>
        void Hit(ContinuationToken token, in MeleeHit hit);

        /// <summary>The swing is over, whether or not it hit anything.</summary>
        void Completed(ContinuationToken token);
    }

    /// <summary>
    /// Starts melee swings and reports what they hit. The game integration implements it with the character's own weapon and animation.
    /// </summary>
    public interface IMeleeDelivery
    {
        /// <summary>
        /// Starts a swing and arranges for <paramref name="events"/> to be told how it goes.
        /// </summary>
        /// <returns>False if the swing could not start; nothing will be reported for it.</returns>
        bool Begin(in MeleeSwingLaunch launch, IMeleeEvents events);
    }

    /// <summary>
    /// A character's melee mechanics, as the activation layer sees them: the combo chain and the press that continues it.
    /// </summary>
    public interface IMeleeSwingHost : IMeleeDelivery
    {
        /// <summary>
        /// True if a fresh combo chain may start now. It is blocked only while a queued chain is still running.
        /// </summary>
        bool CanStartFreshChain { get; }

        /// <summary>
        /// A press while an attack is playing. If one is, the press is consumed here: on a client it asks the server to queue the next swing.
        /// </summary>
        /// <returns>True if the press was consumed and must not start a new use.</returns>
        bool TryQueue(AbilityDefinition ability);
    }

    /// <summary>
    /// Everything an ability execution needs from the game.
    /// </summary>
    public sealed class ExecutionServices
    {
        public ExecutionServices(IAttributeLookup attributes, CombatResolver combat, IProjectileDelivery projectiles, IMeleeDelivery melee = null)
        {
            Attributes = attributes;
            Combat = combat ?? throw new ArgumentNullException(nameof(combat));
            Projectiles = projectiles;
            Melee = melee;
        }

        public IAttributeLookup Attributes { get; }

        public CombatResolver Combat { get; }

        public IProjectileDelivery Projectiles { get; }

        public IMeleeDelivery Melee { get; }

        /// <summary>Spawns entities. Needed by abilities that summon.</summary>
        public ISpawnService Spawner { get; set; }

        /// <summary>Entities one execution may spawn.</summary>
        public int MaxSpawnsPerExecution { get; set; } = 4;

        /// <summary>Applies statuses. Needed by abilities that burn, poison or otherwise afflict a target.</summary>
        public IStatusService Statuses { get; set; }

        /// <summary>Finds entities near a point. Needed by abilities that affect an area.</summary>
        public ITargetQuery Targets { get; set; }

        /// <summary>The random stream chance nodes roll from. Gameplay uses this, never the engine's random.</summary>
        public IGameplayRandom Random { get; set; }

        /// <summary>Most entities one area selection may return. Beyond this the nearest-by-ID are kept and the rest are dropped.</summary>
        public int MaxTargetsPerQuery { get; set; } = 32;

        /// <summary>Hands out execution IDs.</summary>
        public IdCounter ExecutionIds { get; } = new IdCounter();

        /// <summary>Hands out continuation tokens.</summary>
        public IdCounter ContinuationIds { get; } = new IdCounter();

        /// <summary>Operations one chain of executions may run before it is stopped.</summary>
        public int MaxOperationsPerExecution { get; set; } = 256;

        /// <summary>Projectiles one execution may launch.</summary>
        public int MaxProjectilesPerExecution { get; set; } = 16;

        /// <summary>Raised with a description when an execution is cut short or an operation cannot do its job.</summary>
        public event Action<string> Diagnostic;

        /// <summary>
        /// Receives what abilities do as they run. Optional: with none, nothing is recorded. Viewers attach one to watch real execution.
        /// </summary>
        public IExecutionTrace Trace { get; set; }

        internal long NextTraceSequence => ++traceSequence;

        private long traceSequence;

        internal void Report(string message, ExecutionContext context = null)
        {
            Diagnostic?.Invoke(message);

            IExecutionTrace trace = Trace;
            if (trace != null)
            {
                if (context != null)
                {
                    context.Trace(TraceEventKind.Diagnostic, context.CurrentNodeId, null, 0d, message);
                }
                else
                {
                    trace.Record(new TraceEvent(NextTraceSequence, TraceEventKind.Diagnostic, default, default, null, null, null, 0d, message));
                }
            }
        }
    }

    /// <summary>
    /// A request to run an ability, made by the activation layer. The ability does not know whether the source is a player, a monster, an item or a trap.
    /// </summary>
    public readonly struct ExecutionRequest
    {
        public ExecutionRequest(SourceHandle source, EntityId instigator, Position3 origin, Position3 direction, StatusInvocation status = default)
        {
            Source = source;
            Instigator = instigator;
            Origin = origin;
            Direction = direction;
            Status = status;
        }

        /// <summary>For an ability started by a status: who holds it and how strong it is. Empty otherwise.</summary>
        public StatusInvocation Status { get; }

        /// <summary>Where the execution comes from.</summary>
        public SourceHandle Source { get; }

        /// <summary>The entity credited with the results, if any. Natural hazards have none.</summary>
        public EntityId Instigator { get; }

        /// <summary>Where the ability starts, for example a projectile's muzzle.</summary>
        public Position3 Origin { get; }

        /// <summary>The direction the ability is aimed.</summary>
        public Position3 Direction { get; }
    }

    /// <summary>
    /// The state shared by every branch of one execution.
    /// </summary>
    public sealed class ExecutionContext
    {
        private readonly Queue<(int operation, BranchFrame frame)> pending = new Queue<(int, BranchFrame)>();

        internal ExecutionContext(AbilityExecutor executor, CompiledAbility ability, ExecutionServices services, ExecutionId id, in ExecutionRequest request)
        {
            Executor = executor;
            Ability = ability;
            Services = services;
            Id = id;
            Source = request.Source;
            Instigator = request.Instigator;
            Origin = request.Origin;
            Direction = request.Direction;
            Status = request.Status;
            Snapshot = new double[ability.SnapshotCount];
        }

        internal AbilityExecutor Executor { get; }

        public CompiledAbility Ability { get; }

        public ExecutionServices Services { get; }

        public ExecutionId Id { get; }

        public SourceHandle Source { get; }

        public EntityId Instigator { get; }

        public Position3 Origin { get; }

        public Position3 Direction { get; }

        /// <summary>What the status that started this execution told it, or empty if a status did not.</summary>
        public StatusInvocation Status { get; }

        /// <summary>Values captured when the ability was activated, for inputs authored to be read then.</summary>
        internal double[] Snapshot { get; }

        /// <summary>True if a trace is attached. Callers build trace text only when this is true, so an ability costs nothing extra without one.</summary>
        internal bool Tracing => Services.Trace != null;

        /// <summary>The node of the operation being run, for traces.</summary>
        internal string CurrentNodeId { get; set; }

        /// <summary>
        /// Tells the trace, if there is one, that a data node produced a value.
        /// </summary>
        internal void TraceValue(CompiledNode node, double number, string text)
        {
            if (Services.Trace != null)
            {
                Trace(TraceEventKind.Value, node.NodeId, null, number, text);
            }
        }

        /// <summary>
        /// Tells the trace, if there is one, what the running operation did.
        /// </summary>
        internal void TraceOutcome(double number, string text)
        {
            if (Services.Trace != null)
            {
                Trace(TraceEventKind.Outcome, CurrentNodeId, null, number, text);
            }
        }

        internal void Trace(TraceEventKind kind, string nodeId, string portId, double number, string text)
        {
            IExecutionTrace trace = Services.Trace;
            if (trace != null)
            {
                trace.Record(new TraceEvent(Services.NextTraceSequence, kind, Id, Ability.Id, Ability.Name, nodeId, portId, number, text));
            }
        }

        public int OperationsRun { get; internal set; }

        public int ProjectilesLaunched { get; internal set; }

        public int EntitiesSpawned { get; internal set; }

        /// <summary>True once the execution was stopped for exceeding a budget.</summary>
        public bool Aborted { get; internal set; }

        /// <summary>
        /// Schedules an operation to run next. Operations run in the order they are scheduled.
        /// </summary>
        public void Continue(int operation, in BranchFrame frame)
        {
            if (operation >= 0)
            {
                pending.Enqueue((operation, frame));
            }
        }

        internal bool TryTakeNext(out int operation, out BranchFrame frame)
        {
            if (pending.Count == 0)
            {
                operation = -1;
                frame = default;
                return false;
            }

            (operation, frame) = pending.Dequeue();
            return true;
        }

        internal void Clear() => pending.Clear();
    }
}
