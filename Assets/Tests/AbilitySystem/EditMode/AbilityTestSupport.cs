using SimpleRPG.Gameplay;
using System.Collections.Generic;
using UnityEngine;
using EntityId = SimpleRPG.Gameplay.EntityId;

namespace SimpleRPG.Abilities.Tests.EditMode
{
    /// <summary>
    /// A plain in-memory combatant, standing in only for the networked character.
    /// </summary>
    public sealed class TestCombatant : ICombatant
    {
        public TestCombatant(ulong id, int health)
        {
            Id = new EntityId(id);
            Health = health;
            IsAlive = true;
            LifeId = 1;
        }

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
    /// A projectile delivery that launches nothing in the world: it records each launch and lets a test decide how it ends. Everything the
    /// executor does around it (continuations, damage, attribution) is the real thing.
    /// </summary>
    public sealed class ScriptedProjectiles : IProjectileDelivery
    {
        public sealed class Launched
        {
            public ProjectileLaunch Launch;
            public IProjectileEvents Events;
        }

        public List<Launched> Launches { get; } = new List<Launched>();

        /// <summary>When false, <see cref="Launch"/> refuses.</summary>
        public bool Accepts { get; set; } = true;

        public bool Launch(in ProjectileLaunch launch, IProjectileEvents events)
        {
            if (!Accepts) return false;

            Launches.Add(new Launched { Launch = launch, Events = events });
            return true;
        }

        public void Hit(int index, EntityId victim, int bodyLocation = 0, Position3? at = null)
        {
            Launched launched = Launches[index];
            launched.Events.Impact(launched.Launch.Token, new ProjectileImpact(victim, bodyLocation, at ?? new Position3(1, 2, 3), hitWorld: false));
        }

        public void HitWorld(int index, Position3? at = null)
        {
            Launched launched = Launches[index];
            launched.Events.Impact(launched.Launch.Token, new ProjectileImpact(default, 0, at ?? new Position3(1, 2, 3), hitWorld: true));
        }

        public void Expire(int index)
        {
            Launched launched = Launches[index];
            launched.Events.Expired(launched.Launch.Token);
        }
    }

    /// <summary>
    /// A spawner that puts nothing in the world: it records each request. Everything the executor does around it is the real thing.
    /// </summary>
    public sealed class ScriptedSpawner : ISpawnService
    {
        public List<SpawnRequest> Spawns { get; } = new List<SpawnRequest>();

        /// <summary>When false, <see cref="Spawn"/> refuses.</summary>
        public bool Accepts { get; set; } = true;

        public bool Spawn(in SpawnRequest request)
        {
            if (!Accepts) return false;

            Spawns.Add(request);
            return true;
        }
    }

    /// <summary>
    /// A melee delivery with no weapon or animation: it records each swing and lets a test decide what the swing hits and when it ends.
    /// Everything the executor does around it (continuations, damage, attribution) is the real thing.
    /// </summary>
    public sealed class ScriptedMelee : IMeleeDelivery
    {
        public sealed class Swing
        {
            public MeleeSwingLaunch Launch;
            public IMeleeEvents Events;
        }

        public List<Swing> Swings { get; } = new List<Swing>();

        /// <summary>When false, <see cref="Begin"/> refuses.</summary>
        public bool Accepts { get; set; } = true;

        /// <summary>Runs as each swing begins, so a test can make a swing hit straight away.</summary>
        public System.Action<MeleeSwingLaunch, IMeleeEvents> OnBegin { get; set; }

        public bool Begin(in MeleeSwingLaunch launch, IMeleeEvents events)
        {
            if (!Accepts) return false;

            Swings.Add(new Swing { Launch = launch, Events = events });
            OnBegin?.Invoke(launch, events);
            return true;
        }

        public void Hit(int swing, EntityId victim, int attackNumber = 1, float comboScalar = 1f, float weaponDamage = 10f, int bodyLocation = 1)
        {
            Swing s = Swings[swing];
            s.Events.Hit(s.Launch.Token, new MeleeHit(victim, bodyLocation, attackNumber, comboScalar, weaponDamage));
        }

        public void Complete(int swing)
        {
            Swing s = Swings[swing];
            s.Events.Completed(s.Launch.Token);
        }
    }

    /// <summary>
    /// Builds abilities through the same document operations the editor uses.
    /// </summary>
    public static class AbilityBuilder
    {
        public const string MagicPower = "test.attr.magicpower";

        /// <summary>
        /// Creates an empty ability that has only its Activate node (named "activate").
        /// </summary>
        public static AbilityDefinition Empty(string name)
        {
            AbilityDefinition definition = ScriptableObject.CreateInstance<AbilityDefinition>();
            definition.name = name;
            definition.SetDetails(name, 1f);

            // Creating an ability in the editor already gives it an Activate node; add one only if that did not happen.
            if (definition.Nodes.Count == 0)
            {
                definition.AddNode(NodeTypes.Activate, null, "activate");
            }

            return definition;
        }

        /// <summary>
        /// A melee attack: swing, and on each hit damage the victim by the weapon's damage x the combo attack's multiplier, as a direct weapon hit.
        /// </summary>
        public static AbilityDefinition MeleeAttack()
        {
            AbilityDefinition ability = Empty("MeleeAttack");
            string swing = ability.AddNode(NodeTypes.MeleeSwing, null, "swing");
            string victim = ability.AddNode(NodeTypes.EventVictim, null, "victim");
            string weaponDamage = ability.AddNode(NodeTypes.EventWeaponDamage, null, "weaponDamage");
            string comboScalar = ability.AddNode(NodeTypes.EventComboScalar, null, "comboScalar");
            string scaled = ability.AddNode(NodeTypes.Multiply, null, "scaledDamage");
            string damage = ability.AddNode(NodeTypes.Damage, new DamageConfig { Provenance = DamageProvenance.DirectWeaponHit }, "damage");

            ability.Connect("activate", BuiltInNodes.Next, swing, BuiltInNodes.In);
            ability.Connect(swing, BuiltInNodes.OnHit, damage, BuiltInNodes.In);
            ability.Connect(victim, BuiltInNodes.Value, damage, BuiltInNodes.Target);
            ability.Connect(weaponDamage, BuiltInNodes.Value, scaled, BuiltInNodes.A);
            ability.Connect(comboScalar, BuiltInNodes.Value, scaled, BuiltInNodes.B);
            ability.Connect(scaled, BuiltInNodes.Result, damage, BuiltInNodes.Amount);
            return ability;
        }

        /// <summary>
        /// Summon: spawn a creature where the ability started.
        /// </summary>
        public static AbilityDefinition Summon(GameObject prefab)
        {
            AbilityDefinition ability = Empty("Summon");
            string spawn = ability.AddNode(NodeTypes.SpawnEntity, new SpawnEntityConfig { Prefab = prefab }, "spawn");
            ability.Connect("activate", BuiltInNodes.Next, spawn, BuiltInNodes.In);
            return ability;
        }

        /// <summary>
        /// A status's tick ability: starts from a Status Tick node and damages the holder by the status's strength, as periodic damage.
        /// </summary>
        public static AbilityDefinition StatusTick(string name = "Tick")
        {
            AbilityDefinition ability = Empty(name);
            ability.RemoveNode("activate");
            string tick = ability.AddNode(NodeTypes.StatusTick, null, "tick");
            string holder = ability.AddNode(NodeTypes.StatusHolder, null, "holder");
            string potency = ability.AddNode(NodeTypes.StatusPotency, null, "potency");
            string damage = ability.AddNode(NodeTypes.Damage, new DamageConfig { Provenance = DamageProvenance.Periodic }, "damage");
            ability.Connect(tick, BuiltInNodes.Next, damage, BuiltInNodes.In);
            ability.Connect(holder, BuiltInNodes.Value, damage, BuiltInNodes.Target);
            ability.Connect(potency, BuiltInNodes.Value, damage, BuiltInNodes.Amount);
            return ability;
        }

        /// <summary>
        /// Ignite: launch a projectile, and on impact put the status on the victim with the given strength read from an attribute of the source.
        /// </summary>
        public static AbilityDefinition Ignite(GameObject prefab, StatusDefinition status, float potency, bool fromAttribute = false)
        {
            AbilityDefinition ability = Empty("Ignite");
            string launch = ability.AddNode(NodeTypes.LaunchProjectile, new LaunchProjectileConfig { Projectile = prefab }, "launch");
            string apply = ability.AddNode(NodeTypes.ApplyStatus, new ApplyStatusConfig { Status = status }, "apply");
            string victim = ability.AddNode(NodeTypes.EventVictim, null, "victim");
            string strength = fromAttribute
                ? ability.AddNode(NodeTypes.Attribute, new AttributeConfig { Kind = AttributeBindingKind.Instigator, AttributeKey = MagicPower, Policy = EvaluationPolicy.OnExecution }, "strength")
                : ability.AddNode(NodeTypes.Constant, new ConstantConfig { Value = potency }, "strength");
            ability.Connect("activate", BuiltInNodes.Next, launch, BuiltInNodes.In);
            ability.Connect(launch, BuiltInNodes.OnImpact, apply, BuiltInNodes.In);
            ability.Connect(victim, BuiltInNodes.Value, apply, BuiltInNodes.Target);
            ability.Connect(strength, BuiltInNodes.Value, apply, BuiltInNodes.PotencyPort);
            return ability;
        }

        /// <summary>
        /// Fireball: launch a projectile, and on impact damage everything within a radius of where it landed.
        /// </summary>
        public static AbilityDefinition Fireball(GameObject prefab, float radius = 3f, float damageAmount = 15f, bool excludeInstigator = true, bool excludeVictim = false)
        {
            AbilityDefinition ability = Empty("Fireball");
            string launch = ability.AddNode(NodeTypes.LaunchProjectile, new LaunchProjectileConfig { Projectile = prefab }, "launch");
            string area = ability.AddNode(NodeTypes.EntitiesInRadius, new EntitiesInRadiusConfig { Radius = radius, ExcludeInstigator = excludeInstigator, ExcludeVictim = excludeVictim }, "area");
            string each = ability.AddNode(NodeTypes.ForEachTarget, null, "each");
            string current = ability.AddNode(NodeTypes.CurrentTarget, null, "current");
            string amount = ability.AddNode(NodeTypes.Constant, new ConstantConfig { Value = damageAmount }, "amount");
            string damage = ability.AddNode(NodeTypes.Damage, null, "damage");

            ability.Connect("activate", BuiltInNodes.Next, launch, BuiltInNodes.In);
            ability.Connect(launch, BuiltInNodes.OnImpact, each, BuiltInNodes.In);
            ability.Connect(area, BuiltInNodes.Value, each, BuiltInNodes.Targets);
            ability.Connect(each, BuiltInNodes.Each, damage, BuiltInNodes.In);
            ability.Connect(current, BuiltInNodes.Value, damage, BuiltInNodes.Target);
            ability.Connect(amount, BuiltInNodes.Value, damage, BuiltInNodes.Amount);
            return ability;
        }

        /// <summary>
        /// Firebolt: launch a projectile, and on impact damage the victim by base + scale x the attribute.
        /// </summary>
        public static AbilityDefinition Firebolt(GameObject prefab, float baseDamage = 12f, float scale = 0.8f, AttributeBindingKind kind = AttributeBindingKind.Source, EvaluationPolicy policy = EvaluationPolicy.OnActivation, float fallback = 0f)
        {
            AbilityDefinition ability = Empty("Firebolt");
            string launch = ability.AddNode(NodeTypes.LaunchProjectile, new LaunchProjectileConfig { Projectile = prefab }, "launch");
            string damage = ability.AddNode(NodeTypes.Damage, null, "damage");
            string victim = ability.AddNode(NodeTypes.EventVictim, null, "victim");
            string attribute = ability.AddNode(NodeTypes.Attribute, new AttributeConfig { Kind = kind, AttributeKey = MagicPower, Fallback = fallback, Policy = policy }, "magicPower");
            string scaleNode = ability.AddNode(NodeTypes.Constant, new ConstantConfig { Value = scale }, "scale");
            string baseNode = ability.AddNode(NodeTypes.Constant, new ConstantConfig { Value = baseDamage }, "base");
            string scaled = ability.AddNode(NodeTypes.Multiply, null, "scaled");
            string total = ability.AddNode(NodeTypes.Add, null, "total");

            ability.Connect("activate", BuiltInNodes.Next, launch, BuiltInNodes.In);
            ability.Connect(launch, BuiltInNodes.OnImpact, damage, BuiltInNodes.In);
            ability.Connect(victim, BuiltInNodes.Value, damage, BuiltInNodes.Target);
            ability.Connect(attribute, BuiltInNodes.Value, scaled, BuiltInNodes.A);
            ability.Connect(scaleNode, BuiltInNodes.Value, scaled, BuiltInNodes.B);
            ability.Connect(baseNode, BuiltInNodes.Value, total, BuiltInNodes.A);
            ability.Connect(scaled, BuiltInNodes.Result, total, BuiltInNodes.B);
            ability.Connect(total, BuiltInNodes.Result, damage, BuiltInNodes.Amount);
            return ability;
        }
    }
}
