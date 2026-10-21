using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using HitDetection;
using NUnit.Framework;
using SimpleRPG;
using SimpleRPG.Abilities;
using SimpleRPG.UI;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

namespace SimpleRPG.Tests.PlayMode
{
    /// <summary>
    /// Checks the server side of Firebolt and Icebolt: the projectile is spawned and launched by the server, its damage is
    /// applied once to a target, it never hurts its caster, and the server's cooldown stops a second shot.
    /// Each test starts a host NetworkManager and tears everything down afterwards.
    /// </summary>
    public class SpellNetworkTests
    {
        private const string Firebolt = "Assets/Abilitities/Abillities/Firebolt.asset";
        private const string Icebolt = "Assets/Abilitities/Abillities/Icebolt.asset";
        private const string ShooterPrefab = "Assets/Prefabs/Characters/Player/TestCharacter.prefab";
        private const string TargetPrefab = "Assets/Tests/PlayMode/Prefabs/TestTarget.prefab";
        private const int StartingHealth = 100;

        private GameObject managerObject;
        private NetworkManager manager;
        private readonly List<GameObject> spawned = new List<GameObject>();

        [UnitySetUp]
        public IEnumerator SetUp()
        {
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
            foreach (Projectile projectile in Object.FindObjectsByType<Projectile>(FindObjectsSortMode.None))
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

            yield return null;
        }

        /// <summary>
        /// Instantiates a prefab, places it, and spawns it on the test host. Health is set before spawning, as in the game.
        /// </summary>
        private Character SpawnCharacter(string path, Vector3 position)
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

            ThirdPersonCharacterController thirdPerson = instance.GetComponent<ThirdPersonCharacterController>();
            if (thirdPerson != null) Object.DestroyImmediate(thirdPerson);

            Character character = instance.GetComponent<Character>();
            character.Initialize();
            instance.GetComponent<NetworkObject>().Spawn();
            return character;
        }

        private static FireProjectileAbility LoadSpell(string path)
        {
            FireProjectileAbility spell = AssetDatabase.LoadAssetAtPath<FireProjectileAbility>(path);
            Assert.IsNotNull(spell, $"Could not load {path}.");
            return spell;
        }

        /// <summary>
        /// Runs the caster's behaviour the way the ability button does, bypassing the client-side cooldown.
        /// </summary>
        private static void CastOnServer(Character caster, string spellName)
        {
            AbilityInstance instance = caster.PersonalAbilities.First(x => x.Ability.AbillityName == spellName);
            IInvokeableAbilityBehaviour behaviour = caster.GetComponent<FireProjectileAbillityBehaviour>();
            behaviour.Invoke(instance);
        }

        private static List<Projectile> LiveProjectiles()
        {
            return Object.FindObjectsByType<Projectile>(FindObjectsSortMode.None).ToList();
        }

        [UnityTest]
        public IEnumerator Firebolt_ServerCast_SpawnsAProjectileThatDamagesTheTargetByItsConfiguredAmount()
        {
            Character shooter = SpawnCharacter(ShooterPrefab, Vector3.zero);
            Character target = SpawnCharacter(TargetPrefab, new Vector3(0f, 0f, 5f));
            yield return null;

            CastOnServer(shooter, "Firebolt");
            yield return null;

            List<Projectile> projectiles = LiveProjectiles();
            Assert.AreEqual(1, projectiles.Count, "One Firebolt should spawn exactly one projectile.");

            projectiles[0].TargetHit(target, BodyLocation.Torso);
            yield return null;

            Assert.AreEqual(StartingHealth - LoadSpell(Firebolt).Damage.Damage, target.Health,
                "The server should apply the Firebolt's configured damage, not the projectile's default.");
        }

        [UnityTest]
        public IEnumerator Icebolt_ServerCast_SpawnsAProjectileThatDamagesTheTargetByItsConfiguredAmount()
        {
            Character shooter = SpawnCharacter(ShooterPrefab, Vector3.zero);
            Character target = SpawnCharacter(TargetPrefab, new Vector3(0f, 0f, 5f));
            yield return null;

            CastOnServer(shooter, "Icebolt");
            yield return null;

            List<Projectile> projectiles = LiveProjectiles();
            Assert.AreEqual(1, projectiles.Count, "One Icebolt should spawn exactly one projectile.");

            projectiles[0].TargetHit(target, BodyLocation.Torso);
            yield return null;

            Assert.AreEqual(StartingHealth - LoadSpell(Icebolt).Damage.Damage, target.Health);
        }

        [UnityTest]
        public IEnumerator Firebolt_ServerCast_LaunchesTheProjectileForwardOnTheServer()
        {
            Character shooter = SpawnCharacter(ShooterPrefab, Vector3.zero);
            yield return null;

            CastOnServer(shooter, "Firebolt");
            yield return null;

            Projectile projectile = LiveProjectiles().Single();
            Vector3 velocity = projectile.GetComponent<Rigidbody>().linearVelocity;

            // The server sets the velocity itself, so it must not depend on a client running its ClientRpc body.
            Assert.Greater(velocity.z, 0f, "The projectile should travel along the caster's forward direction.");
            Assert.Greater(velocity.magnitude, 0.1f, "The projectile should be moving.");
        }

        [UnityTest]
        public IEnumerator Firebolt_HitOnItsCaster_DoesNotDamageTheCaster()
        {
            Character shooter = SpawnCharacter(ShooterPrefab, Vector3.zero);
            yield return null;

            CastOnServer(shooter, "Firebolt");
            yield return null;

            LiveProjectiles().Single().TargetHit(shooter, BodyLocation.Torso);
            yield return null;

            Assert.AreEqual(StartingHealth, shooter.Health, "A caster must not be hurt by its own spell.");
        }

        [UnityTest]
        public IEnumerator Firebolt_TouchingTheSameTargetTwice_DamagesItOnce()
        {
            Character shooter = SpawnCharacter(ShooterPrefab, Vector3.zero);
            Character target = SpawnCharacter(TargetPrefab, new Vector3(0f, 0f, 5f));
            yield return null;

            CastOnServer(shooter, "Firebolt");
            yield return null;

            Projectile projectile = LiveProjectiles().Single();
            projectile.TargetHit(target, BodyLocation.Torso);
            projectile.TargetHit(target, BodyLocation.Head);
            yield return null;

            Assert.AreEqual(StartingHealth - LoadSpell(Firebolt).Damage.Damage, target.Health);
        }

        [UnityTest]
        public IEnumerator Firebolt_CastTwiceInsideItsCooldown_SpawnsOneProjectile()
        {
            Character shooter = SpawnCharacter(ShooterPrefab, Vector3.zero);
            yield return null;

            CastOnServer(shooter, "Firebolt");
            CastOnServer(shooter, "Firebolt");
            yield return null;

            Assert.AreEqual(1, LiveProjectiles().Count, "The server's cooldown should refuse the second shot.");
        }

        [UnityTest]
        public IEnumerator AbilityWithoutItsBehaviour_Activate_DoesNotThrowOrStartACooldown()
        {
            FireProjectileAbility spell = LoadSpell(Firebolt);

            // A character with no fire behaviour, so the ability has nothing to invoke.
            GameObject bare = new GameObject("NoBehaviourCharacter");
            spawned.Add(bare);
            Character character = bare.AddComponent<Character>();

            LogAssert.Expect(LogType.Error, new Regex("has no InvokableAbility"));
            AbilityInstance instance = new AbilityInstance(spell, character);

            bool activated = false;
            instance.OnAbilityActivated += _ => activated = true;

            // Wait past the cooldown so the activation check itself passes and only the missing behaviour stops it.
            yield return new WaitForSeconds(spell.CooldownTime + 0.1f);

            Assert.DoesNotThrow(() => instance.Activate());
            Assert.IsFalse(activated, "An ability with no behaviour should not report that it was activated.");
        }
    }
}
