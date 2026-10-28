using HitDetection;
using NUnit.Framework;
using SimpleRPG.Abilities.Integration;
using SimpleRPG.Gameplay;
using SimpleRPG.UI;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using EntityId = SimpleRPG.Gameplay.EntityId;

namespace SimpleRPG.Abilities.Tests.PlayMode
{
    /// <summary>
    /// Runs Firebolt and Icebolt through the real ability system on a real host: real characters, real networked projectiles, the real combat
    /// pipeline. The same Firebolt definition is fired by a player, a monster and a world object, and damage always comes from the server.
    /// </summary>
    public class FireboltNetworkTests
    {
        private const string Firebolt = "Assets/Abilities/Abilities/Firebolt.asset";
        private const string Icebolt = "Assets/Abilities/Abilities/Icebolt.asset";
        private const string PlayerPrefab = "Assets/Prefabs/Characters/Player/TestCharacter.prefab";
        private const string MonsterPrefab = "Assets/Tests/PlayMode/Prefabs/TestTarget.prefab";
        private const int StartingHealth = 100;
        private const string MagicPower = "test.attr.magicpower";

        private GameObject managerObject;
        private NetworkManager manager;
        private readonly List<GameObject> spawned = new List<GameObject>();
        private readonly List<Object> created = new List<Object>();
        private GameplayServices services;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            services = GameplayServices.Reset();
            AbilityServices.Reset();

            managerObject = new GameObject("TestNetworkManager");
            UnityTransport transport = managerObject.AddComponent<UnityTransport>();
            manager = managerObject.AddComponent<NetworkManager>();
            manager.NetworkConfig = new NetworkConfig { NetworkTransport = transport };

            Assert.IsTrue(manager.StartHost(), "The test host should start.");
            yield return null;
            Assert.IsTrue(manager.IsServer, "The test NetworkManager should be running as a server.");
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            foreach (Projectile projectile in Object.FindObjectsByType<Projectile>())
            {
                Object.Destroy(projectile.gameObject);
            }

            foreach (GameObject instance in spawned)
            {
                if (instance != null) Object.Destroy(instance);
            }

            spawned.Clear();

            if (manager != null && manager.IsListening)
            {
                manager.Shutdown();
                yield return new WaitUntil(() => !manager.IsListening);
            }

            if (managerObject != null) Object.Destroy(managerObject);
            foreach (Object o in created)
            {
                if (o != null) Object.DestroyImmediate(o);
            }

            created.Clear();
            GameplayServices.Reset();
            AbilityServices.Reset();
            yield return null;
        }

        /// <summary>
        /// Instantiates a prefab, places it, and spawns it on the test host. Health is set before spawning, as in the game.
        /// </summary>
        private Character SpawnCharacter(string path, Vector3 position, params AbilityDefinition[] graphAbilities)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            Assert.IsNotNull(prefab, $"Could not load test prefab {path}.");

            GameObject instance = Object.Instantiate(prefab, position, Quaternion.identity);
            spawned.Add(instance);

            // Player-only components expect a camera, input and the game's UI, which these tests don't load. Remove them before spawning.
            RegisterWithUI hud = instance.GetComponent<RegisterWithUI>();
            if (hud != null) Object.DestroyImmediate(hud);

            FirstPersonCharacterController firstPerson = instance.GetComponent<FirstPersonCharacterController>();
            if (firstPerson != null) Object.DestroyImmediate(firstPerson);

            Character character = instance.GetComponent<Character>();
            if (graphAbilities.Length > 0)
            {
                // Give the character exactly these abilities, as its prefab would.
                typeof(Character).GetField("graphAbilities", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(character, graphAbilities.ToList());
            }

            character.Initialize();
            instance.GetComponent<NetworkObject>().Spawn();
            return character;
        }

        private static AbilityDefinition LoadSpell(string path)
        {
            AbilityDefinition spell = AssetDatabase.LoadAssetAtPath<AbilityDefinition>(path);
            Assert.IsNotNull(spell, $"Could not load {path}.");
            return spell;
        }

        private static int ConfiguredDamage(AbilityDefinition spell)
        {
            ConstantConfig constant = (ConstantConfig)spell.Nodes.Single(n => n.TypeId == NodeTypes.Constant).Config;
            return (int)constant.Value;
        }

        private static List<Projectile> LiveProjectiles() => Object.FindObjectsByType<Projectile>().ToList();

        /// <summary>
        /// Builds Firebolt in code with a formula: 12 + 0.8 x the source's magic power. The projectile is the shipped Firebolt's.
        /// </summary>
        private AbilityDefinition FormulaFirebolt()
        {
            GameObject prefab = ((LaunchProjectileConfig)LoadSpell(Firebolt).Nodes.Single(n => n.TypeId == NodeTypes.LaunchProjectile).Config).Projectile;
            AbilityDefinition ability = ScriptableObject.CreateInstance<AbilityDefinition>();
            created.Add(ability);
            ability.name = "FormulaFirebolt";
            ability.SetDetails("Formula Firebolt", 0.5f);
            if (ability.Nodes.Count == 0)
            {
                ability.AddNode(NodeTypes.Activate, null, "activate");
            }

            string launch = ability.AddNode(NodeTypes.LaunchProjectile, new LaunchProjectileConfig { Projectile = prefab }, "launch");
            string damage = ability.AddNode(NodeTypes.Damage, null, "damage");
            string victim = ability.AddNode(NodeTypes.EventVictim, null, "victim");
            string power = ability.AddNode(NodeTypes.Attribute, new AttributeConfig { Kind = SimpleRPG.Gameplay.AttributeBindingKind.Source, AttributeKey = MagicPower }, "power");
            string scale = ability.AddNode(NodeTypes.Constant, new ConstantConfig { Value = 0.8f }, "scale");
            string baseDamage = ability.AddNode(NodeTypes.Constant, new ConstantConfig { Value = 12f }, "base");
            string scaled = ability.AddNode(NodeTypes.Multiply, null, "scaled");
            string total = ability.AddNode(NodeTypes.Add, null, "total");
            ability.Connect("activate", BuiltInNodes.Next, launch, BuiltInNodes.In);
            ability.Connect(launch, BuiltInNodes.OnImpact, damage, BuiltInNodes.In);
            ability.Connect(victim, BuiltInNodes.Value, damage, BuiltInNodes.Target);
            ability.Connect(power, BuiltInNodes.Value, scaled, BuiltInNodes.A);
            ability.Connect(scale, BuiltInNodes.Value, scaled, BuiltInNodes.B);
            ability.Connect(baseDamage, BuiltInNodes.Value, total, BuiltInNodes.A);
            ability.Connect(scaled, BuiltInNodes.Result, total, BuiltInNodes.B);
            ability.Connect(total, BuiltInNodes.Result, damage, BuiltInNodes.Amount);
            Assert.IsTrue(ability.Compile().Success, ability.Compile().Report.ToString());
            return ability;
        }

        private static void GiveMagicPower(SourceHandle source, double value)
        {
            AttributeSet set = new AttributeSet();
            set.SetBase(new AttributeId(MagicPower), value);
            GameplayServices.Current.Attributes.Register(source, set);
        }

        // ----- The shipped spells -----

        [UnityTest]
        public IEnumerator Firebolt_Cast_SpawnsAProjectileThatDamagesTheTargetByItsConfiguredAmount()
        {
            AbilityDefinition firebolt = LoadSpell(Firebolt);
            Character shooter = SpawnCharacter(PlayerPrefab, Vector3.zero);
            Character target = SpawnCharacter(MonsterPrefab, new Vector3(0f, 0f, 5f));
            yield return null;

            Assert.IsTrue(shooter.GetComponent<AbilityUser>().TryActivateOnServer(firebolt));
            yield return null;

            List<Projectile> projectiles = LiveProjectiles();
            Assert.AreEqual(1, projectiles.Count, "One Firebolt should spawn exactly one projectile.");

            projectiles[0].TargetHit(target, BodyLocation.Torso);
            yield return null;

            Assert.AreEqual(StartingHealth - ConfiguredDamage(firebolt), target.Health, "The server applies the damage the ability's graph says.");
        }

        [UnityTest]
        public IEnumerator Icebolt_Cast_SpawnsAProjectileThatDamagesTheTargetByItsConfiguredAmount()
        {
            AbilityDefinition icebolt = LoadSpell(Icebolt);
            Character shooter = SpawnCharacter(PlayerPrefab, Vector3.zero);
            Character target = SpawnCharacter(MonsterPrefab, new Vector3(0f, 0f, 5f));
            yield return null;

            shooter.GetComponent<AbilityUser>().TryActivateOnServer(icebolt);
            yield return null;

            LiveProjectiles().Single().TargetHit(target, BodyLocation.Torso);
            yield return null;

            Assert.AreEqual(StartingHealth - ConfiguredDamage(icebolt), target.Health);
        }

        [UnityTest]
        public IEnumerator Firebolt_LaunchesTheProjectileForwardOnTheServer()
        {
            Character shooter = SpawnCharacter(PlayerPrefab, Vector3.zero);
            yield return null;

            shooter.GetComponent<AbilityUser>().TryActivateOnServer(LoadSpell(Firebolt));
            yield return null;

            Vector3 velocity = LiveProjectiles().Single().GetComponent<Rigidbody>().linearVelocity;

            // The server sets the velocity itself, so it must not depend on a client running its ClientRpc body.
            Assert.Greater(velocity.z, 0f, "The projectile should travel along the caster's forward direction.");
            Assert.Greater(velocity.magnitude, 0.1f, "The projectile should be moving.");
        }

        [UnityTest]
        public IEnumerator Firebolt_HitOnItsCaster_DoesNotDamageTheCaster()
        {
            Character shooter = SpawnCharacter(PlayerPrefab, Vector3.zero);
            yield return null;

            shooter.GetComponent<AbilityUser>().TryActivateOnServer(LoadSpell(Firebolt));
            yield return null;

            LiveProjectiles().Single().TargetHit(shooter, BodyLocation.Torso);
            yield return null;

            Assert.AreEqual(StartingHealth, shooter.Health, "A caster must not be hurt by its own spell.");
        }

        [UnityTest]
        public IEnumerator Firebolt_TouchingTheSameTargetTwice_DamagesItOnce()
        {
            AbilityDefinition firebolt = LoadSpell(Firebolt);
            Character shooter = SpawnCharacter(PlayerPrefab, Vector3.zero);
            Character target = SpawnCharacter(MonsterPrefab, new Vector3(0f, 0f, 5f));
            yield return null;

            shooter.GetComponent<AbilityUser>().TryActivateOnServer(firebolt);
            yield return null;

            Projectile projectile = LiveProjectiles().Single();
            projectile.TargetHit(target, BodyLocation.Torso);
            projectile.TargetHit(target, BodyLocation.Head);
            yield return null;

            Assert.AreEqual(StartingHealth - ConfiguredDamage(firebolt), target.Health);
        }

        [UnityTest]
        public IEnumerator Firebolt_UsedTwiceInsideItsCooldown_SpawnsOneProjectile()
        {
            AbilityDefinition firebolt = LoadSpell(Firebolt);
            Character shooter = SpawnCharacter(PlayerPrefab, Vector3.zero);
            yield return null;
            AbilityUser user = shooter.GetComponent<AbilityUser>();

            bool first = user.TryActivateOnServer(firebolt);
            bool second = user.TryActivateOnServer(firebolt);
            yield return null;

            Assert.IsTrue(first);
            Assert.IsFalse(second, "The server's cooldown should refuse the second use.");
            Assert.AreEqual(1, LiveProjectiles().Count);
        }

        [UnityTest]
        public IEnumerator Firebolt_CanBeUsedAgainOnceItsCooldownPasses()
        {
            AbilityDefinition firebolt = LoadSpell(Firebolt);
            Character shooter = SpawnCharacter(PlayerPrefab, Vector3.zero);
            yield return null;
            AbilityUser user = shooter.GetComponent<AbilityUser>();
            user.TryActivateOnServer(firebolt);

            yield return new WaitForSeconds(firebolt.CooldownSeconds + 0.2f);

            Assert.IsTrue(user.TryActivateOnServer(firebolt));
        }

        [UnityTest]
        public IEnumerator Cooldowns_AreTrackedPerAbility()
        {
            Character shooter = SpawnCharacter(PlayerPrefab, Vector3.zero);
            yield return null;
            AbilityUser user = shooter.GetComponent<AbilityUser>();

            Assert.IsTrue(user.TryActivateOnServer(LoadSpell(Firebolt)));
            Assert.IsTrue(user.TryActivateOnServer(LoadSpell(Icebolt)), "Icebolt has its own cooldown.");
            yield return null;
        }

        // ----- Authority: a client can only ask -----

        [UnityTest]
        public IEnumerator AnAbilityTheCharacterDoesNotHave_IsRefused()
        {
            AbilityDefinition notTheirs = FormulaFirebolt();
            Character shooter = SpawnCharacter(PlayerPrefab, Vector3.zero);
            yield return null;

            LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex("which it does not have"));
            bool ran = shooter.GetComponent<AbilityUser>().TryActivateOnServer(notTheirs);
            yield return null;

            Assert.IsFalse(ran);
            Assert.IsEmpty(LiveProjectiles());
        }

        [UnityTest]
        public IEnumerator ADeadCharacter_CannotUseAbilities()
        {
            Character shooter = SpawnCharacter(PlayerPrefab, Vector3.zero);
            yield return null;
            shooter.TakeDamage(new DamageInfo { Damage = StartingHealth * 2 }, BodyLocation.Torso);
            yield return null;

            Assert.IsFalse(shooter.IsAlive);
            Assert.IsFalse(shooter.GetComponent<AbilityUser>().TryActivateOnServer(LoadSpell(Firebolt)));
            Assert.IsEmpty(LiveProjectiles());
        }

        [UnityTest]
        public IEnumerator ADespawnedCharacter_CannotUseAbilities()
        {
            // Only the host machine is available here, so this checks the guard rather than a real remote client: a client's request travels by
            // RPC and reaches the same server-side checks that the tests above exercise.
            Character shooter = SpawnCharacter(PlayerPrefab, Vector3.zero);
            yield return null;
            AbilityUser user = shooter.GetComponent<AbilityUser>();
            shooter.GetComponent<NetworkObject>().Despawn(false);
            yield return null;

            Assert.IsFalse(user.TryActivateOnServer(LoadSpell(Firebolt)), "A character that is not in the world cannot cast.");
            Assert.IsEmpty(LiveProjectiles());
        }

        // ----- One definition, many sources -----

        [UnityTest]
        public IEnumerator SameFirebolt_FromAPlayerAndAMonster_UsesEachOnesOwnMagicPower()
        {
            AbilityDefinition firebolt = FormulaFirebolt();
            Character player = SpawnCharacter(PlayerPrefab, new Vector3(-10f, 0f, 0f), firebolt);
            Character monster = SpawnCharacter(MonsterPrefab, new Vector3(10f, 0f, 0f), firebolt);
            Character playerVictim = SpawnCharacter(MonsterPrefab, new Vector3(0f, 0f, 20f));
            Character monsterVictim = SpawnCharacter(MonsterPrefab, new Vector3(0f, 0f, 30f));
            yield return null;
            GiveMagicPower(SourceHandle.FromEntity(player.Id), 30);
            GiveMagicPower(SourceHandle.FromEntity(monster.Id), 8);

            player.GetComponent<AbilityUser>().TryActivateOnServer(firebolt);
            monster.GetComponent<AbilityUser>().TryActivateOnServer(firebolt);
            yield return null;

            List<Projectile> projectiles = LiveProjectiles();
            Assert.AreEqual(2, projectiles.Count);
            Projectile fromPlayer = projectiles.OrderBy(p => p.transform.position.x).First();
            Projectile fromMonster = projectiles.OrderBy(p => p.transform.position.x).Last();
            fromPlayer.TargetHit(playerVictim, BodyLocation.Torso);
            fromMonster.TargetHit(monsterVictim, BodyLocation.Torso);
            yield return null;

            Assert.AreEqual(StartingHealth - 36, playerVictim.Health, "12 + 0.8 x 30");
            Assert.AreEqual(StartingHealth - 18, monsterVictim.Health, "12 + 0.8 x 8 = 18.4, rounded");
        }

        [UnityTest]
        public IEnumerator SameFirebolt_FromAWorldObject_NeedsNoCaster()
        {
            AbilityDefinition firebolt = FormulaFirebolt();
            Character target = SpawnCharacter(MonsterPrefab, new Vector3(0f, 0f, 20f));
            yield return null;
            SourceHandle brazier = SourceHandle.FromWorldObject(5);
            GiveMagicPower(brazier, 10);

            AbilityServices.Current.Executor.Execute(
                firebolt.Compile().Ability,
                new ExecutionRequest(brazier, default, new Position3(0f, 1f, 0f), new Position3(0f, 0f, 1f)));
            yield return null;

            Projectile projectile = LiveProjectiles().Single();
            projectile.TargetHit(target, BodyLocation.Torso);
            yield return null;

            Assert.AreEqual(StartingHealth - 20, target.Health, "12 + 0.8 x 10, with no instigator at all");
        }

        [UnityTest]
        public IEnumerator TheKillingBlow_CreditsTheCaster()
        {
            AbilityDefinition firebolt = LoadSpell(Firebolt);
            Character shooter = SpawnCharacter(PlayerPrefab, Vector3.zero);
            Character target = SpawnCharacter(MonsterPrefab, new Vector3(0f, 0f, 20f));
            yield return null;
            target.TakeDamage(new DamageInfo { Damage = StartingHealth - 5 }, BodyLocation.Torso);
            DeathContext death = null;
            services.Events.Subscribe<EntityDiedEvent>(e => death = e.Context);

            shooter.GetComponent<AbilityUser>().TryActivateOnServer(firebolt);
            yield return null;
            LiveProjectiles().Single().TargetHit(target, BodyLocation.Head);
            yield return null;

            Assert.IsNotNull(death);
            Assert.AreEqual(shooter.Id, death.Killer);
            Assert.AreEqual(DamageProvenance.Spell, death.KillingProvenance);
        }

        // ----- The projectile in the world -----

        [UnityTest]
        public IEnumerator AProjectile_HittingTheWorld_StopsThere_AndDealsNoDamage()
        {
            GameObject wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            spawned.Add(wall);
            wall.transform.position = new Vector3(0f, 1.2f, 6f);
            wall.transform.localScale = new Vector3(10f, 10f, 1f);

            Character shooter = SpawnCharacter(PlayerPrefab, Vector3.zero);
            Character bystander = SpawnCharacter(MonsterPrefab, new Vector3(0f, 0f, 20f));
            yield return null;

            shooter.GetComponent<AbilityUser>().TryActivateOnServer(LoadSpell(Firebolt));

            float deadline = Time.time + 5f;
            while (LiveProjectiles().Count == 0 && Time.time < deadline) yield return null;
            Assert.AreEqual(1, LiveProjectiles().Count, "The projectile should be launched.");

            Projectile projectile = LiveProjectiles()[0];
            ProjectileImpact? impact = null;
            projectile.Impacted += i => impact = i;
            float launchedAt = Time.time;

            float wait = Time.time + 5f;
            while (LiveProjectiles().Count > 0 && Time.time < wait) yield return null;

            Assert.IsTrue(impact.HasValue, "The wall should report an impact, not let the projectile run out its lifetime.");
            Assert.IsTrue(impact.Value.HitWorld);
            Assert.IsFalse(impact.Value.Victim.IsValid);
            Assert.Less(Time.time - launchedAt, 2.5f, "It should stop at the wall (about 1 second away), well before its lifetime.");
            Assert.IsEmpty(LiveProjectiles(), "The projectile should stop and disappear at the wall.");
            Assert.AreEqual(StartingHealth, bystander.Health);
            Assert.AreEqual(StartingHealth, shooter.Health);
            Assert.AreEqual(0, AbilityServices.Current.Executor.PendingContinuations, "The continuation is released by the impact.");
        }

        [UnityTest]
        public IEnumerator AProjectile_ThatHitsNothing_ExpiresAndReleasesItsContinuation()
        {
            Character shooter = SpawnCharacter(PlayerPrefab, Vector3.zero);
            yield return null;

            shooter.GetComponent<AbilityUser>().TryActivateOnServer(LoadSpell(Firebolt));
            yield return null;
            Assert.AreEqual(1, AbilityServices.Current.Executor.PendingContinuations);

            // Nothing is in its way, so it flies until its lifetime runs out.
            float deadline = Time.time + 30f;
            while (LiveProjectiles().Count > 0 && Time.time < deadline) yield return null;

            Assert.IsEmpty(LiveProjectiles());
            Assert.AreEqual(0, AbilityServices.Current.Executor.PendingContinuations);
        }

        [UnityTest]
        public IEnumerator AProjectile_FlyingIntoACharacter_HitsItThroughRealPhysics()
        {
            AbilityDefinition firebolt = LoadSpell(Firebolt);
            // The test target has no colliders, so the player prefab (which has hurtboxes) stands in as the thing to hit.
            Character shooter = SpawnCharacter(PlayerPrefab, Vector3.zero);
            Character target = SpawnCharacter(PlayerPrefab, new Vector3(0f, 0f, 4f));
            yield return null;

            shooter.GetComponent<AbilityUser>().TryActivateOnServer(firebolt);

            float deadline = Time.time + 8f;
            while (target.Health == StartingHealth && Time.time < deadline) yield return null;

            Assert.AreEqual(StartingHealth - ConfiguredDamage(firebolt), target.Health, "The projectile should reach the target and damage it once.");
        }
    }
}
