using HitDetection;
using NUnit.Framework;
using SimpleRPG.Gameplay;
using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;
using UnityEngine.TestTools;

namespace SimpleRPG.Abilities.Tests.PlayMode
{
    /// <summary>
    /// Checks the damage and death pipeline against real networked characters on a host: one death per life, the captured context,
    /// the replicated alive flag and revival. Characters are spawned from the test target prefab.
    /// </summary>
    public class CharacterDeathNetworkTests
    {
        private const int StartingHealth = 100;
        private const string TargetPrefab = "Assets/Tests/PlayMode/Prefabs/TestTarget.prefab";

        private GameObject managerObject;
        private NetworkManager manager;
        private readonly List<GameObject> spawned = new List<GameObject>();
        private GameplayServices services;
        private List<DeathContext> deaths;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            services = GameplayServices.Reset();
            deaths = new List<DeathContext>();
            services.Events.Subscribe<EntityDiedEvent>(e => deaths.Add(e.Context));

            managerObject = new GameObject("TestNetworkManager");
            UnityTransport transport = managerObject.AddComponent<UnityTransport>();
            manager = managerObject.AddComponent<NetworkManager>();
            manager.NetworkConfig = new NetworkConfig { NetworkTransport = transport };

            Assert.IsTrue(manager.StartHost(), "The test host should start.");
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (manager != null && manager.IsListening)
            {
                manager.Shutdown();
                yield return new WaitUntil(() => !manager.IsListening);
            }

            if (managerObject != null) Object.Destroy(managerObject);
            foreach (GameObject spawnedObject in spawned)
            {
                if (spawnedObject != null) Object.Destroy(spawnedObject);
            }

            spawned.Clear();
            GameplayServices.Reset();
            yield return null;
        }

        private Character SpawnCharacter(Vector3 position)
        {
#if UNITY_EDITOR
            GameObject prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(TargetPrefab);
            Assert.IsNotNull(prefab, $"Could not load test prefab {TargetPrefab}.");
            GameObject instance = Object.Instantiate(prefab, position, Quaternion.identity);
            spawned.Add(instance);
            Character character = instance.GetComponent<Character>();
            character.Initialize();
            instance.GetComponent<NetworkObject>().Spawn();
            return character;
#else
            Assert.Ignore("The test prefab can only be loaded in the editor.");
            return null;
#endif
        }

        private static void Hit(Character target, int damage, Character by = null, DamageProvenance provenance = DamageProvenance.None)
        {
            target.TakeDamage(new DamageInfo { Damage = damage }, BodyLocation.Torso, by, provenance);
        }

        [UnityTest]
        public IEnumerator NonLethalDamage_LeavesTheCharacterAlive_AndRaisesNoDeath()
        {
            Character target = SpawnCharacter(Vector3.zero);
            yield return null;

            Hit(target, 30);

            Assert.AreEqual(StartingHealth - 30, target.Health);
            Assert.IsTrue(target.IsAlive);
            Assert.IsEmpty(deaths);
        }

        [UnityTest]
        public IEnumerator LethalDamage_KillsOnce_AndCapturesTheContext()
        {
            Character killer = SpawnCharacter(Vector3.zero);
            Character victim = SpawnCharacter(new Vector3(3f, 0f, 4f));
            yield return null;
            int onDeathRaised = 0;
            victim.OnDeath += () => onDeathRaised++;

            Hit(victim, StartingHealth, killer, DamageProvenance.Spell);
            yield return null;

            Assert.IsFalse(victim.IsAlive);
            Assert.AreEqual(0, victim.Health);
            Assert.AreEqual(1, deaths.Count, "One lethal hit raises exactly one EntityDied.");
            Assert.AreEqual(new DeathTransitionId(victim.Id, 1), deaths[0].Transition);
            Assert.AreEqual(killer.Id, deaths[0].Killer);
            Assert.AreEqual(DamageProvenance.Spell, deaths[0].KillingProvenance);
            Assert.AreEqual(3f, deaths[0].Position.X, 0.01f);
            Assert.AreEqual(4f, deaths[0].Position.Z, 0.01f);
            Assert.AreEqual(1, onDeathRaised, "The presentation OnDeath event fires once.");
        }

        [UnityTest]
        public IEnumerator DamageToACorpse_NeverRaisesASecondDeath()
        {
            Character victim = SpawnCharacter(Vector3.zero);
            yield return null;

            Hit(victim, StartingHealth);
            for (int i = 0; i < 5; i++)
            {
                Hit(victim, 10);
                yield return null;
            }

            Assert.AreEqual(1, deaths.Count);
            Assert.AreEqual(0, victim.Health, "A corpse takes no further damage.");
        }

        [UnityTest]
        public IEnumerator Overkill_ClampsHealthToZero()
        {
            Character victim = SpawnCharacter(Vector3.zero);
            yield return null;

            Hit(victim, StartingHealth * 5);

            Assert.AreEqual(0, victim.Health);
        }

        [UnityTest]
        public IEnumerator Revival_StartsANewLife_ThatCanDieAgain()
        {
            Character victim = SpawnCharacter(Vector3.zero);
            yield return null;

            Hit(victim, StartingHealth);
            Assert.IsTrue(services.Combat.Revive(victim.Id, 50));
            yield return null;

            Assert.IsTrue(victim.IsAlive);
            Assert.AreEqual(50, victim.Health);
            Assert.AreEqual(2, victim.LifeId);

            Hit(victim, 50);

            Assert.AreEqual(2, deaths.Count);
            Assert.AreNotEqual(deaths[0].Transition, deaths[1].Transition);
        }

        [UnityTest]
        public IEnumerator DespawnedCharacter_IsNoLongerACombatant_AndTakesNoDamage()
        {
            Character target = SpawnCharacter(Vector3.zero);
            yield return null;
            target.GetComponent<NetworkObject>().Despawn(false);
            yield return null;

            Hit(target, 10);

            Assert.IsEmpty(deaths);
            Assert.AreEqual(0, services.Combatants.Count, "A despawned character is no longer a combatant.");
        }

        [UnityTest]
        public IEnumerator SpawnedCharacters_RegisterAsCombatants()
        {
            Character a = SpawnCharacter(Vector3.zero);
            Character b = SpawnCharacter(Vector3.one);
            yield return null;

            Assert.AreEqual(2, services.Combatants.Count);
            Assert.AreNotEqual(a.Id, b.Id);
            Assert.IsTrue(a.Id.IsValid);
        }
    }
}
