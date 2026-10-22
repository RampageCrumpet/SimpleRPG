using HitDetection;
using NUnit.Framework;
using SimpleRPG.Abilities.Integration;
using SimpleRPG.Gameplay;
using SimpleRPG.UI;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

namespace SimpleRPG.Abilities.Tests.PlayMode
{
    /// <summary>
    /// A trace attached to the running game's executor records what really happens on a real host: real characters, a real networked
    /// projectile and the real combat pipeline. This is the data a live viewer reads.
    /// </summary>
    public class LiveTraceNetworkTests
    {
        private const string Firebolt = "Assets/Abilitities/Abillities/Firebolt.asset";
        private const string PlayerPrefab = "Assets/Prefabs/Characters/Player/TestCharacter.prefab";
        private const string MonsterPrefab = "Assets/Tests/PlayMode/Prefabs/TestTarget.prefab";
        private const int StartingHealth = 100;

        private GameObject managerObject;
        private NetworkManager manager;
        private readonly List<GameObject> spawned = new List<GameObject>();

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            GameplayServices.Reset();
            AbilityServices.Reset();

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
            GameplayServices.Reset();
            AbilityServices.Reset();
            yield return null;
        }

        private Character Spawn(string path, Vector3 position)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            Assert.IsNotNull(prefab, $"Could not load test prefab {path}.");
            GameObject instance = Object.Instantiate(prefab, position, Quaternion.identity);
            spawned.Add(instance);

            foreach (System.Type type in new[] { typeof(RegisterWithUI), typeof(FirstPersonCharacterController), typeof(ThirdPersonCharacterController) })
            {
                Component component = instance.GetComponent(type);
                if (component != null) Object.DestroyImmediate(component);
            }

            Character character = instance.GetComponent<Character>();
            character.Initialize();
            instance.GetComponent<NetworkObject>().Spawn();
            return character;
        }

        [UnityTest]
        public IEnumerator ATraceOnTheRunningGame_RecordsARealCast_FromLaunchToDamage()
        {
            AbilityDefinition firebolt = AssetDatabase.LoadAssetAtPath<AbilityDefinition>(Firebolt);
            Character shooter = Spawn(PlayerPrefab, Vector3.zero);
            Character target = Spawn(MonsterPrefab, new Vector3(0f, 0f, 5f));
            yield return null;
            RecordingTrace trace = new RecordingTrace();
            AbilityServices.Current.Execution.Trace = trace;

            Assert.IsTrue(shooter.GetComponent<AbilityUser>().TryActivateOnServer(firebolt));
            yield return null;
            LiveProjectiles().Single().TargetHit(target, BodyLocation.Torso);
            yield return null;

            ExecutionId run = trace.Executions().Single();
            List<TraceEvent> events = trace.ForExecution(run);
            Assert.IsTrue(events.All(e => e.AbilityName == firebolt.name), "Every event names the ability that ran.");
            Assert.AreEqual(TraceEventKind.ExecutionStarted, events[0].Kind);
            Assert.IsTrue(events.Any(e => e.Kind == TraceEventKind.Suspended), "The run waited for the real projectile.");
            Assert.IsTrue(events.Any(e => e.Kind == TraceEventKind.Resumed), "And resumed when it hit.");

            TraceEvent damage = events.Single(e => e.Kind == TraceEventKind.Outcome && e.Text.StartsWith("Damage"));
            Assert.AreEqual(StartingHealth - target.Health, damage.Number, "The traced damage is the damage the server applied.");
            HashSet<string> graphNodes = new HashSet<string>(firebolt.Nodes.Select(n => n.NodeId));
            Assert.IsTrue(events.Where(e => e.NodeId != null).All(e => graphNodes.Contains(e.NodeId)), "Every event belongs to a node of the shipped graph.");
            Assert.IsTrue(events.Any(e => e.Kind == TraceEventKind.NodeRan && e.NodeId == firebolt.Nodes.Single(n => n.TypeId == NodeTypes.Damage).NodeId));
        }

        [UnityTest]
        public IEnumerator DetachingTheTrace_StopsTheRecording_AndTheGameCarriesOn()
        {
            AbilityDefinition firebolt = AssetDatabase.LoadAssetAtPath<AbilityDefinition>(Firebolt);
            Character shooter = Spawn(PlayerPrefab, Vector3.zero);
            Character target = Spawn(MonsterPrefab, new Vector3(0f, 0f, 5f));
            yield return null;
            RecordingTrace trace = new RecordingTrace();
            AbilityServices.Current.Execution.Trace = trace;
            shooter.GetComponent<AbilityUser>().TryActivateOnServer(firebolt);
            yield return null;
            int recorded = trace.Count;

            AbilityServices.Current.Execution.Trace = null;
            LiveProjectiles().Single().TargetHit(target, BodyLocation.Torso);
            yield return null;

            Assert.Greater(recorded, 0);
            Assert.AreEqual(recorded, trace.Count, "Nothing more is recorded once it is detached.");
            Assert.Less(target.Health, StartingHealth, "The projectile still did its damage.");
        }

        [UnityTest]
        public IEnumerator AProjectileRemovedBeforeItHits_ReleasesTheAbilityThatWasWaitingForIt()
        {
            AbilityDefinition firebolt = AssetDatabase.LoadAssetAtPath<AbilityDefinition>(Firebolt);
            Character shooter = Spawn(PlayerPrefab, Vector3.zero);
            yield return null;
            RecordingTrace trace = new RecordingTrace();
            AbilityServices.Current.Execution.Trace = trace;
            shooter.GetComponent<AbilityUser>().TryActivateOnServer(firebolt);
            yield return null;
            Assert.AreEqual(1, AbilityServices.Current.Executor.PendingContinuations, "The cast waits for its projectile.");

            // The projectile is removed some other way: a scene change or a shutdown, not a hit and not running out of time.
            LiveProjectiles().Single().GetComponent<NetworkObject>().Despawn(true);
            yield return null;

            Assert.AreEqual(0, AbilityServices.Current.Executor.PendingContinuations, "Nothing is left waiting for a projectile that no longer exists.");
            Assert.IsTrue(trace.Events.Any(e => e.Kind == TraceEventKind.Outcome && e.Text.Contains("without hitting")));
        }

        private static List<Projectile> LiveProjectiles() => Object.FindObjectsByType<Projectile>().ToList();
    }
}
