using SimpleRPG.Gameplay;
using System;
using System.Collections.Generic;
using UnityEngine;
using EntityId = SimpleRPG.Gameplay.EntityId;

namespace SimpleRPG.Abilities
{
    /// <summary>
    /// When an attribute input is read: once at activation (a snapshot), or each time it is used.
    /// </summary>
    public enum EvaluationPolicy : byte
    {
        /// <summary>Read when the ability is activated. Later changes, including the caster's death, do not alter it.</summary>
        OnActivation = 0,

        /// <summary>Read each time it is used.</summary>
        OnExecution = 1,
    }

    [Serializable]
    public sealed class ActivateConfig : AbilityNodeConfig
    {
    }

    [Serializable]
    public sealed class ConstantConfig : AbilityNodeConfig
    {
        public float Value;
    }

    [Serializable]
    public sealed class AttributeConfig : AbilityNodeConfig
    {
        /// <summary>Whose attribute to read: the instigator's, or the source's. A constant is a Constant node instead.</summary>
        public AttributeBindingKind Kind = AttributeBindingKind.Instigator;

        /// <summary>The attribute's stable key, for example "simplerpg.attr.intelligence".</summary>
        public string AttributeKey = "";

        /// <summary>The value used when the instigator or source has no attributes, for example a hazard with no instigator.</summary>
        public float Fallback;

        public EvaluationPolicy Policy = EvaluationPolicy.OnActivation;
    }

    [Serializable]
    public sealed class MathConfig : AbilityNodeConfig
    {
    }

    [Serializable]
    public sealed class EventVictimConfig : AbilityNodeConfig
    {
    }

    [Serializable]
    public sealed class MeleeSwingConfig : AbilityNodeConfig
    {
    }

    [Serializable]
    public sealed class SpawnEntityConfig : AbilityNodeConfig
    {
        /// <summary>The creature to spawn. It needs a NetworkObject. It is spawned with its own AI, health and lifetime.</summary>
        public GameObject Prefab;
    }

    [Serializable]
    public sealed class StatusTickConfig : AbilityNodeConfig
    {
    }

    [Serializable]
    public sealed class StatusDataConfig : AbilityNodeConfig
    {
    }

    [Serializable]
    public sealed class ApplyStatusConfig : AbilityNodeConfig
    {
        /// <summary>The status to apply.</summary>
        public StatusDefinition Status;
    }

    [Serializable]
    public sealed class ChanceConfig : AbilityNodeConfig
    {
        /// <summary>The chance of the Success output, from 0 (never) to 1 (always).</summary>
        [UnityEngine.Range(0f, 1f)]
        public float Probability = 0.5f;
    }

    [Serializable]
    public sealed class ForEachTargetConfig : AbilityNodeConfig
    {
    }

    [Serializable]
    public sealed class CurrentTargetConfig : AbilityNodeConfig
    {
    }

    [Serializable]
    public sealed class EntitiesInRadiusConfig : AbilityNodeConfig
    {
        /// <summary>How far from the impact point to look, in world units.</summary>
        public float Radius = 3f;

        /// <summary>Leave out the entity that used the ability, so an explosion does not hurt its own caster.</summary>
        public bool ExcludeInstigator = true;

        /// <summary>Leave out the entity that was hit, if the effect is for everything around it.</summary>
        public bool ExcludeVictim;

        /// <summary>Leave out dead entities.</summary>
        public bool AliveOnly = true;
    }

    [Serializable]
    public sealed class LaunchProjectileConfig : AbilityNodeConfig
    {
        /// <summary>The projectile prefab. It needs a Projectile and a NetworkObject, and must be in the network prefab list.</summary>
        public GameObject Projectile;
    }

    [Serializable]
    public sealed class DamageConfig : AbilityNodeConfig
    {
        /// <summary>What kind of damage this is, so rules can tell a spell from a weapon blow.</summary>
        public DamageProvenance Provenance = DamageProvenance.Spell;
    }

    /// <summary>
    /// The type IDs of the built-in nodes.
    /// </summary>
    public static class NodeTypes
    {
        /// <summary>
        /// True for a node type an ability can start from: Activate for an ability that is used, Status Tick for one a status runs.
        /// </summary>
        public static bool IsEntry(string typeId) => typeId == Activate || typeId == StatusTick;

        public const string Activate = "simplerpg.flow.activate.v1";
        public const string Constant = "simplerpg.data.constant.v1";
        public const string Attribute = "simplerpg.data.attribute.v1";
        public const string Add = "simplerpg.data.add.v1";
        public const string Multiply = "simplerpg.data.multiply.v1";
        public const string EventVictim = "simplerpg.data.eventvictim.v1";
        public const string EventWeaponDamage = "simplerpg.data.eventweapondamage.v1";
        public const string EventComboScalar = "simplerpg.data.eventcomboscalar.v1";
        public const string SpawnEntity = "simplerpg.action.spawnentity.v1";
        public const string StatusTick = "simplerpg.flow.statustick.v1";
        public const string StatusHolder = "simplerpg.data.statusholder.v1";
        public const string StatusPotency = "simplerpg.data.statuspotency.v1";
        public const string StatusStacks = "simplerpg.data.statusstacks.v1";
        public const string ApplyStatus = "simplerpg.action.applystatus.v1";
        public const string Chance = "simplerpg.flow.chance.v1";
        public const string ForEachTarget = "simplerpg.flow.foreachtarget.v1";
        public const string CurrentTarget = "simplerpg.data.currenttarget.v1";
        public const string EntitiesInRadius = "simplerpg.data.entitiesinradius.v1";
        public const string MeleeSwing = "simplerpg.action.meleeswing.v1";
        public const string LaunchProjectile = "simplerpg.action.launchprojectile.v1";
        public const string Damage = "simplerpg.action.damage.v1";
    }

    /// <summary>
    /// The built-in node types: the small vocabulary needed to compose projectile spells and damage.
    /// </summary>
    public static class BuiltInNodes
    {
        // Port IDs.
        public const string Next = "Next";
        public const string In = "In";
        public const string Value = "Value";
        public const string Result = "Result";
        public const string A = "A";
        public const string B = "B";
        public const string Target = "Target";
        public const string Amount = "Amount";
        public const string OnImpact = "OnImpact";
        public const string OnHit = "OnHit";
        public const string PotencyPort = "Potency";
        public const string Targets = "Targets";
        public const string Each = "Each";
        public const string Success = "Success";
        public const string Failure = "Failure";
        public const string Completed = "Completed";

        public static void RegisterAll(AbilityNodeRegistry registry)
        {
            registry.Register(new NodeDescriptor<ActivateConfig>(
                NodeTypes.Activate, "Activate", "Entry", "Where the ability starts when it is used.",
                new[] { PortDescriptor.FlowOut(Next) },
                (config, context) => new ActivateOperation(context.FlowTarget(Next))));

            registry.Register(new NodeDescriptor<ConstantConfig>(
                NodeTypes.Constant, "Constant", "Data", "A fixed number.",
                new[] { PortDescriptor.DataOut(Value, DataType.Float) },
                (config, context) => new ConstantExpr(config.Value),
                (config, sink) =>
                {
                    if (!IsFinite(config.Value)) sink.Error("The value must be a finite number.");
                }));

            registry.Register(new NodeDescriptor<AttributeConfig>(
                NodeTypes.Attribute, "Attribute", "Data", "A number read from an attribute of the instigator or the source.",
                new[] { PortDescriptor.DataOut(Value, DataType.Float) },
                CompileAttribute,
                (config, sink) =>
                {
                    if (string.IsNullOrWhiteSpace(config.AttributeKey)) sink.Error("An attribute node needs an attribute key.");
                    if (config.Kind == AttributeBindingKind.Constant) sink.Error("Use a Constant node for a fixed number; an attribute node reads the instigator or the source.");
                    if (!IsFinite(config.Fallback)) sink.Error("The fallback must be a finite number.");
                }));

            registry.Register(new NodeDescriptor<MathConfig>(
                NodeTypes.Add, "Add", "Data", "Adds two numbers.",
                new[] { PortDescriptor.DataIn(A, DataType.Float), PortDescriptor.DataIn(B, DataType.Float), PortDescriptor.DataOut(Result, DataType.Float) },
                (config, context) => new BinaryExpr(context.FloatInput(A), context.FloatInput(B), isMultiply: false)));

            registry.Register(new NodeDescriptor<MathConfig>(
                NodeTypes.Multiply, "Multiply", "Data", "Multiplies two numbers.",
                new[] { PortDescriptor.DataIn(A, DataType.Float), PortDescriptor.DataIn(B, DataType.Float), PortDescriptor.DataOut(Result, DataType.Float) },
                (config, context) => new BinaryExpr(context.FloatInput(A), context.FloatInput(B), isMultiply: true)));

            registry.Register(new NodeDescriptor<EventVictimConfig>(
                NodeTypes.EventVictim, "Event Victim", "Targets", "The entity a projectile hit. Only available after an impact.",
                new[] { PortDescriptor.DataOut(Value, DataType.Entity) },
                (config, context) => new EventVictimExpr(),
                requiresImpactContext: true));

            registry.Register(new NodeDescriptor<EventVictimConfig>(
                NodeTypes.EventWeaponDamage, "Event Weapon Damage", "Targets", "The base damage of the weapon that landed a melee hit. Only available after a hit.",
                new[] { PortDescriptor.DataOut(Value, DataType.Float) },
                (config, context) => new EventWeaponDamageExpr(),
                requiresImpactContext: true));

            registry.Register(new NodeDescriptor<EventVictimConfig>(
                NodeTypes.EventComboScalar, "Event Combo Scalar", "Targets", "The damage multiplier of the combo attack that landed a melee hit. Only available after a hit.",
                new[] { PortDescriptor.DataOut(Value, DataType.Float) },
                (config, context) => new EventComboScalarExpr(),
                requiresImpactContext: true));

            registry.Register(new NodeDescriptor<SpawnEntityConfig>(
                NodeTypes.SpawnEntity, "Spawn Entity", "Actions", "Spawns a creature where the ability started (for a death rule, where the owner died). The creature is independent: it keeps living if the item that spawned it is taken off.",
                new[] { PortDescriptor.FlowIn(In), PortDescriptor.FlowOut(Next) },
                (config, context) => new SpawnEntityOperation(config.Prefab, context.FlowTarget(Next)),
                (config, sink) =>
                {
                    if (config.Prefab == null) sink.Error("A prefab to spawn is required.");
                }));

            registry.Register(new NodeDescriptor<StatusTickConfig>(
                NodeTypes.StatusTick, "Status Tick", "Entry", "Where an ability starts when a status ticks. Downstream of it, Status Holder, Status Potency and Status Stacks are available.",
                new[] { PortDescriptor.FlowOut(Next, providesContext: EventContext.Status) },
                (config, context) => new ActivateOperation(context.FlowTarget(Next))));

            registry.Register(new NodeDescriptor<StatusDataConfig>(
                NodeTypes.StatusHolder, "Status Holder", "Status", "The entity the status is on. Only available in a status tick ability.",
                new[] { PortDescriptor.DataOut(Value, DataType.Entity) },
                (config, context) => new StatusHolderExpr(),
                requiredContext: EventContext.Status));

            registry.Register(new NodeDescriptor<StatusDataConfig>(
                NodeTypes.StatusPotency, "Status Potency", "Status", "How strong the status is for this tick, combined from its stacks as the status says. Only available in a status tick ability.",
                new[] { PortDescriptor.DataOut(Value, DataType.Float) },
                (config, context) => new StatusPotencyExpr(),
                requiredContext: EventContext.Status));

            registry.Register(new NodeDescriptor<StatusDataConfig>(
                NodeTypes.StatusStacks, "Status Stacks", "Status", "How many stacks the status has for this tick. Only available in a status tick ability.",
                new[] { PortDescriptor.DataOut(Value, DataType.Float) },
                (config, context) => new StatusStacksExpr(),
                requiredContext: EventContext.Status));

            registry.Register(new NodeDescriptor<ApplyStatusConfig>(
                NodeTypes.ApplyStatus, "Apply Status", "Actions", "Puts a status on an entity with the given strength. The strength is fixed when it is applied.",
                new[] { PortDescriptor.FlowIn(In), PortDescriptor.DataIn(Target, DataType.Entity), PortDescriptor.DataIn(PotencyPort, DataType.Float), PortDescriptor.FlowOut(Next) },
                (config, context) => new ApplyStatusOperation(config.Status, context.EntityInput(Target), context.FloatInput(PotencyPort), context.FlowTarget(Next)),
                (config, sink) =>
                {
                    if (config.Status == null) sink.Error("A status is required.");
                }));

            registry.Register(new NodeDescriptor<ChanceConfig>(
                NodeTypes.Chance, "Chance", "Flow", "Continues from Success with the given probability, otherwise from Failure. Rolled from the gameplay random stream.",
                new[] { PortDescriptor.FlowIn(In), PortDescriptor.FlowOut(Success), PortDescriptor.FlowOut(Failure) },
                (config, context) => new ChanceOperation(config.Probability, context.FlowTarget(Success), context.FlowTarget(Failure)),
                (config, sink) =>
                {
                    if (!IsFinite(config.Probability) || config.Probability < 0f || config.Probability > 1f) sink.Error("The probability must be between 0 and 1.");
                }));

            registry.Register(new NodeDescriptor<EntitiesInRadiusConfig>(
                NodeTypes.EntitiesInRadius, "Entities In Radius", "Targets", "Everything within a radius of the impact point, in a fixed order. Only available after an impact or hit.",
                new[] { PortDescriptor.DataOut(Value, DataType.EntitySet) },
                (config, context) => new EntitiesInRadiusExpr(config.Radius, config.ExcludeInstigator, config.ExcludeVictim, config.AliveOnly),
                (config, sink) =>
                {
                    if (!IsFinite(config.Radius) || config.Radius <= 0f) sink.Error("The radius must be a positive number.");
                    else if (config.Radius > 50f) sink.Warning("A radius of " + config.Radius + " reaches very far; check it is intended.");
                },
                requiresImpactContext: true));

            registry.Register(new NodeDescriptor<ForEachTargetConfig>(
                NodeTypes.ForEachTarget, "For Each Target", "Flow", "Runs the Each output once for every entity in the set, in order. Each run knows its own Current Target.",
                new[] { PortDescriptor.FlowIn(In), PortDescriptor.DataIn(Targets, DataType.EntitySet), PortDescriptor.FlowOut(Each, providesContext: EventContext.Target) },
                (config, context) => new ForEachTargetOperation(context.EntitySetInput(Targets), context.FlowTarget(Each))));

            registry.Register(new NodeDescriptor<CurrentTargetConfig>(
                NodeTypes.CurrentTarget, "Current Target", "Targets", "The entity the current For Each Target run is for. Only available inside the Each branch.",
                new[] { PortDescriptor.DataOut(Value, DataType.Entity) },
                (config, context) => new CurrentTargetExpr(),
                requiredContext: EventContext.Target));

            registry.Register(new NodeDescriptor<MeleeSwingConfig>(
                NodeTypes.MeleeSwing, "Melee Swing", "Delivery", "Swings the user's weapon. The swing's animation, hit window and combo are the character's; each target hit continues from the OnHit output.",
                new[] { PortDescriptor.FlowIn(In), PortDescriptor.FlowOut(OnHit, providesImpact: true), PortDescriptor.FlowOut(Completed) },
                (config, context) => new MeleeSwingOperation(context.FlowTarget(OnHit), context.FlowTarget(Completed))));

            registry.Register(new NodeDescriptor<LaunchProjectileConfig>(
                NodeTypes.LaunchProjectile, "Launch Projectile", "Delivery", "Fires a projectile. What happens when it hits continues from the OnImpact output.",
                new[] { PortDescriptor.FlowIn(In), PortDescriptor.FlowOut(OnImpact, providesImpact: true) },
                (config, context) => new LaunchProjectileOperation(config.Projectile, context.FlowTarget(OnImpact)),
                (config, sink) =>
                {
                    if (config.Projectile == null) sink.Error("A projectile prefab is required.");
                }));

            registry.Register(new NodeDescriptor<DamageConfig>(
                NodeTypes.Damage, "Damage", "Actions", "Deals damage to an entity through the combat system.",
                new[]
                {
                    PortDescriptor.FlowIn(In),
                    PortDescriptor.DataIn(Target, DataType.Entity),
                    PortDescriptor.DataIn(Amount, DataType.Float),
                    PortDescriptor.FlowOut(Next),
                },
                (config, context) => new DamageOperation(context.EntityInput(Target), context.FloatInput(Amount), config.Provenance, context.FlowTarget(Next))));
        }

        private static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);

        private static CompiledNode CompileAttribute(AttributeConfig config, ICompilationContext context)
        {
            AttributeId attribute = new AttributeId(config.AttributeKey);
            AttributeBinding binding = config.Kind == AttributeBindingKind.Source
                ? AttributeBinding.FromSource(attribute, config.Fallback)
                : AttributeBinding.FromInstigator(attribute, config.Fallback);

            return config.Policy == EvaluationPolicy.OnActivation
                ? new SnapshotExpr(context.AllocateSnapshot(binding))
                : (CompiledNode)new LiveAttributeExpr(binding);
        }
    }

    // ----- Expressions -----

    internal sealed class ConstantExpr : FloatExpr
    {
        private readonly double value;

        public ConstantExpr(double value) => this.value = value;

        protected override double EvaluateCore(ExecutionContext context, in BranchFrame frame) => value;
    }

    internal sealed class SnapshotExpr : FloatExpr
    {
        private readonly int slot;

        public SnapshotExpr(int slot) => this.slot = slot;

        protected override double EvaluateCore(ExecutionContext context, in BranchFrame frame) => context.Snapshot[slot];
    }

    internal sealed class LiveAttributeExpr : FloatExpr
    {
        private readonly AttributeBinding binding;

        public LiveAttributeExpr(AttributeBinding binding) => this.binding = binding;

        protected override double EvaluateCore(ExecutionContext context, in BranchFrame frame)
        {
            binding.TryResolve(context.Services.Attributes, context.Source, context.Instigator, out double value);
            return value;
        }
    }

    internal sealed class BinaryExpr : FloatExpr
    {
        private readonly FloatExpr a;
        private readonly FloatExpr b;
        private readonly bool isMultiply;

        public BinaryExpr(FloatExpr a, FloatExpr b, bool isMultiply)
        {
            this.a = a;
            this.b = b;
            this.isMultiply = isMultiply;
        }

        protected override double EvaluateCore(ExecutionContext context, in BranchFrame frame)
        {
            double left = a.Evaluate(context, frame);
            double right = b.Evaluate(context, frame);
            double result = isMultiply ? left * right : left + right;
            return double.IsNaN(result) || double.IsInfinity(result) ? 0d : result;
        }
    }

    internal sealed class EventVictimExpr : EntityExpr
    {
        protected override EntityId EvaluateCore(ExecutionContext context, in BranchFrame frame) => frame.EventVictim;
    }

    internal sealed class StatusHolderExpr : EntityExpr
    {
        protected override EntityId EvaluateCore(ExecutionContext context, in BranchFrame frame) => frame.Status.Holder;
    }

    internal sealed class StatusPotencyExpr : FloatExpr
    {
        protected override double EvaluateCore(ExecutionContext context, in BranchFrame frame) => frame.Status.Potency;
    }

    internal sealed class StatusStacksExpr : FloatExpr
    {
        protected override double EvaluateCore(ExecutionContext context, in BranchFrame frame) => frame.Status.Stacks;
    }

    internal sealed class CurrentTargetExpr : EntityExpr
    {
        protected override EntityId EvaluateCore(ExecutionContext context, in BranchFrame frame) => frame.CurrentTarget;
    }

    internal sealed class EntitiesInRadiusExpr : EntitySetExpr
    {
        private readonly float radius;
        private readonly bool excludeInstigator;
        private readonly bool excludeVictim;
        private readonly bool aliveOnly;

        public EntitiesInRadiusExpr(float radius, bool excludeInstigator, bool excludeVictim, bool aliveOnly)
        {
            this.radius = radius;
            this.excludeInstigator = excludeInstigator;
            this.excludeVictim = excludeVictim;
            this.aliveOnly = aliveOnly;
        }

        protected override IReadOnlyList<EntityId> EvaluateCore(ExecutionContext context, in BranchFrame frame)
        {
            List<EntityId> found = new List<EntityId>();
            ITargetQuery query = context.Services.Targets;
            if (query == null)
            {
                context.Services.Report(context.Ability.Name + ": no target query is available, so the area selected nothing.", context);
                return found;
            }

            query.EntitiesInRadius(frame.EventPosition, radius, aliveOnly, found);
            EntityId instigator = context.Instigator;
            EntityId victim = frame.EventVictim;
            found.RemoveAll(x => (excludeInstigator && x == instigator) || (excludeVictim && x == victim));

            int max = context.Services.MaxTargetsPerQuery;
            if (found.Count > max)
            {
                context.Services.Report(context.Ability.Name + ": an area selected " + found.Count + " entities; only the first " + max + " are used.", context);
                found.RemoveRange(max, found.Count - max);
            }

            return found;
        }
    }

    internal sealed class EventWeaponDamageExpr : FloatExpr
    {
        protected override double EvaluateCore(ExecutionContext context, in BranchFrame frame) => frame.WeaponDamage;
    }

    internal sealed class EventComboScalarExpr : FloatExpr
    {
        protected override double EvaluateCore(ExecutionContext context, in BranchFrame frame) => frame.ComboScalar;
    }

    // ----- Operations -----

    internal sealed class SpawnEntityOperation : CompiledOperation
    {
        private readonly GameObject prefab;
        private readonly int next;

        public SpawnEntityOperation(GameObject prefab, int next)
        {
            this.prefab = prefab;
            this.next = next;
        }

        public override void Execute(ExecutionContext context, in BranchFrame frame)
        {
            ExecutionServices services = context.Services;
            if (services.Spawner == null)
            {
                services.Report(context.Ability.Describe(Index) + ": no spawn service is available.", context);
            }
            else if (context.EntitiesSpawned >= services.MaxSpawnsPerExecution)
            {
                services.Report(context.Ability.Describe(Index) + ": spawn limit of " + services.MaxSpawnsPerExecution + " reached.", context);
            }
            else if (services.Spawner.Spawn(new SpawnRequest(prefab, context.Origin, context.Source, context.Instigator, context.Id)))
            {
                context.EntitiesSpawned++;
                if (context.Tracing) context.TraceOutcome(context.EntitiesSpawned, "Entity spawned.");
            }
            else
            {
                services.Report(context.Ability.Describe(Index) + ": the entity could not be spawned.", context);
            }

            context.Continue(next, frame);
        }
    }

    internal sealed class ApplyStatusOperation : CompiledOperation
    {
        private readonly StatusDefinition status;
        private readonly EntityExpr target;
        private readonly FloatExpr potency;
        private readonly int next;

        public ApplyStatusOperation(StatusDefinition status, EntityExpr target, FloatExpr potency, int next)
        {
            this.status = status;
            this.target = target;
            this.potency = potency;
            this.next = next;
        }

        public override void Execute(ExecutionContext context, in BranchFrame frame)
        {
            EntityId victim = target.Evaluate(context, frame);
            if (victim.IsValid)
            {
                IStatusService statuses = context.Services.Statuses;
                if (statuses == null)
                {
                    context.Services.Report(context.Ability.Describe(Index) + ": no status service is available.", context);
                }
                else
                {
                    // The strength is read now and fixed for the life of the stack.
                    double strength = potency.Evaluate(context, frame);
                    statuses.Apply(new StatusApplication(victim, status, context.Source, context.Instigator, strength));
                    if (context.Tracing) context.TraceOutcome(strength, "Applied " + (status != null ? status.name : "a status") + " to " + victim + " at strength " + strength.ToString("0.###"));
                }
            }

            context.Continue(next, frame);
        }
    }

    internal sealed class ChanceOperation : CompiledOperation
    {
        private readonly float probability;
        private readonly int success;
        private readonly int failure;

        public ChanceOperation(float probability, int success, int failure)
        {
            this.probability = probability;
            this.success = success;
            this.failure = failure;
        }

        public override void Execute(ExecutionContext context, in BranchFrame frame)
        {
            bool succeeded;
            double roll = -1d;
            if (probability <= 0f)
            {
                succeeded = false;
            }
            else if (probability >= 1f)
            {
                succeeded = true;
            }
            else if (context.Services.Random == null)
            {
                context.Services.Report(context.Ability.Describe(Index) + ": no random stream is available, so the chance failed.", context);
                succeeded = false;
            }
            else
            {
                roll = context.Services.Random.NextDouble();
                succeeded = roll < probability;
            }

            if (context.Tracing) context.TraceOutcome(probability, (succeeded ? "Success" : "Failure") + (roll >= 0d ? ": rolled " + roll.ToString("0.###") + " against " + probability.ToString("0.###") : ": chance is " + probability.ToString("0.###")));
            context.Continue(succeeded ? success : failure, frame);
        }
    }

    internal sealed class ForEachTargetOperation : CompiledOperation
    {
        private readonly EntitySetExpr targets;
        private readonly int each;

        public ForEachTargetOperation(EntitySetExpr targets, int each)
        {
            this.targets = targets;
            this.each = each;
        }

        public override void Execute(ExecutionContext context, in BranchFrame frame)
        {
            // Each target gets its own branch, so one target's values never leak into another's.
            foreach (EntityId target in targets.Evaluate(context, frame))
            {
                context.Continue(each, frame.WithTarget(target));
            }
        }
    }

    internal sealed class MeleeSwingOperation : CompiledOperation
    {
        private readonly int onHit;
        private readonly int completed;

        public MeleeSwingOperation(int onHit, int completed)
        {
            this.onHit = onHit;
            this.completed = completed;
        }

        public override void Execute(ExecutionContext context, in BranchFrame frame)
        {
            ExecutionServices services = context.Services;
            if (services.Melee == null)
            {
                services.Report(context.Ability.Describe(Index) + ": no melee delivery is available.", context);
                return;
            }

            if (!context.Instigator.IsValid)
            {
                services.Report(context.Ability.Describe(Index) + ": a melee swing needs an instigator to swing.", context);
                return;
            }

            // The continuation is registered first, so a delivery that reports straight away still finds it.
            ContinuationToken token = context.Executor.Suspend(context, onHit, completed);
            MeleeSwingLaunch launch = new MeleeSwingLaunch(token, context.Id, context.Source, context.Instigator);
            if (!services.Melee.Begin(launch, context.Executor))
            {
                context.Executor.Cancel(token);
                services.Report(context.Ability.Describe(Index) + ": the swing could not start.", context);
                return;
            }

            if (context.Tracing) context.TraceOutcome(0d, "Swing started.");
        }
    }

    internal sealed class ActivateOperation : CompiledOperation
    {
        private readonly int next;

        public ActivateOperation(int next) => this.next = next;

        public override void Execute(ExecutionContext context, in BranchFrame frame) => context.Continue(next, frame);
    }

    internal sealed class LaunchProjectileOperation : CompiledOperation
    {
        private readonly GameObject prefab;
        private readonly int onImpact;

        public LaunchProjectileOperation(GameObject prefab, int onImpact)
        {
            this.prefab = prefab;
            this.onImpact = onImpact;
        }

        public override void Execute(ExecutionContext context, in BranchFrame frame)
        {
            ExecutionServices services = context.Services;
            if (services.Projectiles == null)
            {
                services.Report(context.Ability.Describe(Index) + ": no projectile delivery is available.", context);
                return;
            }

            if (context.ProjectilesLaunched >= services.MaxProjectilesPerExecution)
            {
                services.Report(context.Ability.Describe(Index) + ": projectile limit of " + services.MaxProjectilesPerExecution + " reached.", context);
                return;
            }

            // The continuation is registered first, so a delivery that reports straight away still finds it.
            ContinuationToken token = context.Executor.Suspend(context, onImpact);
            ProjectileLaunch launch = new ProjectileLaunch(token, context.Id, context.Source, context.Instigator, context.Origin, context.Direction, prefab);
            if (!services.Projectiles.Launch(launch, context.Executor))
            {
                context.Executor.Cancel(token);
                services.Report(context.Ability.Describe(Index) + ": the projectile could not be launched.", context);
                return;
            }

            context.ProjectilesLaunched++;
            if (context.Tracing) context.TraceOutcome(context.ProjectilesLaunched, "Projectile launched.");
        }
    }

    internal sealed class DamageOperation : CompiledOperation
    {
        private readonly EntityExpr target;
        private readonly FloatExpr amount;
        private readonly DamageProvenance provenance;
        private readonly int next;

        public DamageOperation(EntityExpr target, FloatExpr amount, DamageProvenance provenance, int next)
        {
            this.target = target;
            this.amount = amount;
            this.provenance = provenance;
            this.next = next;
        }

        public override void Execute(ExecutionContext context, in BranchFrame frame)
        {
            EntityId victim = target.Evaluate(context, frame);
            if (victim.IsValid)
            {
                double value = amount.Evaluate(context, frame);
                int rounded = (int)Math.Round(Math.Max(0d, Math.Min(value, int.MaxValue)), MidpointRounding.AwayFromZero);
                DamageOutcome outcome = context.Services.Combat.Apply(new DamageRequest(victim, rounded, context.Instigator, context.Source, frame.BodyLocation, provenance, context.Id));
                if (context.Tracing) context.TraceOutcome(rounded, "Damage " + rounded + " to " + victim + ": " + outcome.Result + ", health " + outcome.HealthBefore + " to " + outcome.HealthAfter);
            }
            else
            {
                if (context.Tracing) context.TraceOutcome(0d, "No damage: there is no target.");
            }

            context.Continue(next, frame);
        }
    }
}
