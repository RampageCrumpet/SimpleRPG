using SimpleRPG.AI;
using NUnit.Framework;
using SimpleRPG.Animations;
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
using UnityEngine.AI;
using UnityEngine.TestTools;

namespace SimpleRPG.Abilities.Tests.PlayMode
{
    /// <summary>
    /// An AI controller that sits in the melee attack state, so the test can watch the enemy's real attack logic use its melee ability
    /// without the chasing and pathfinding that need a baked navigation mesh.
    /// </summary>
    public sealed class AttackOnlyAIController : AIController
    {
        private sealed class DoNothingState : IState
        {
            public void OnStateEnter() { }

            public void OnStateUpdate() { }

            public void OnStateExit() { }
        }

        protected override StateMachine CreateStateMachine()
        {
            var resting = new DoNothingState();
            MeleeAttackState attack = new MeleeAttackState(this, () => ChangeState(resting));

            StateMachine machine = new StateMachine();
            machine.InitializeStateMachine(attack);
            return machine;
        }
    }

    /// <summary>
    /// Drives an enemy's real melee attack state against a real target: the state picks its melee ability from the ability bar, the server runs it,
    /// and the swing's animation opens and closes its hit window.
    /// </summary>
    public class AiMeleeNetworkTests
    {
        private const string MonsterPrefab = "Assets/Prefabs/Characters/Enemies/SkeletonFootman.prefab";
        private const string PlayerPrefab = "Assets/Prefabs/Characters/Player/TestCharacter.prefab";

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

        private Character SpawnPlayerTarget(Vector3 position)
        {
            GameObject instance = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefab), position, Quaternion.identity);
            spawned.Add(instance);
            foreach (System.Type type in new[] { typeof(LocalPlayerControls), typeof(FirstPersonCharacterController), typeof(Animations.AnimatableCharacter) })
            {
                Component component = instance.GetComponent(type);
                if (component != null) Object.DestroyImmediate(component);
            }

            instance.tag = "Player";
            Character character = instance.GetComponent<Character>();
            character.Initialize();
            instance.GetComponent<NetworkObject>().Spawn();
            return character;
        }

        private Character SpawnMonster(Vector3 position)
        {
            GameObject instance = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(MonsterPrefab), position, Quaternion.identity);
            spawned.Add(instance);

            // Swap the enemy's full AI (chasing needs a navigation mesh) for one that stays in the attack state.
            Object.DestroyImmediate(instance.GetComponent<AIController>());
            NavigationGrounding grounding = instance.GetComponent<NavigationGrounding>();
            if (grounding != null) Object.DestroyImmediate(grounding);
            NavMeshAgent agent = instance.GetComponent<NavMeshAgent>();
            if (agent != null) Object.DestroyImmediate(agent);

            AttackOnlyAIController controller = instance.AddComponent<AttackOnlyAIController>();
            controller.AttackRange = 10f;

            foreach (Blindsense sense in instance.GetComponents<Blindsense>())
            {
                sense.Range = 10f;
                typeof(Blindsense).GetField("sensableLayers", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public)?.SetValue(sense, (LayerMask)~0);
            }

            Character character = instance.GetComponent<Character>();
            character.Initialize();
            instance.GetComponent<NetworkObject>().Spawn();
            return character;
        }

        [UnityTest]
        public IEnumerator AnEnemyInRangeOfATarget_SwingsItsMeleeAbility_AndTheSwingEnds()
        {
            Character target = SpawnPlayerTarget(new Vector3(0f, 0f, 1f));
            yield return null;
            Character monster = SpawnMonster(Vector3.zero);
            int attacks = 0;
            monster.GetComponent<MeleeDelivery>().MeleeAttackStarted += (_, __) => attacks++;

            float deadline = Time.time + 3f;
            while (attacks == 0 && Time.time < deadline) yield return null;

            Assert.GreaterOrEqual(attacks, 1, "The attack state should use the enemy's melee ability from its ability bar.");
            GraphAbilitySlot meleeSlot = monster.Loadout.Slots.OfType<GraphAbilitySlot>().Single(x => x.Definition.HasNode(NodeTypes.MeleeSwing));
            Assert.Greater(meleeSlot.CooldownTimeLeft, -1f);

            // The clip's own events close the hit window, which ends the swing and releases the ability.
            float swingDeadline = Time.time + 6f;
            while (AbilityServices.Current.Executor.PendingContinuations > 0 && Time.time < swingDeadline) yield return null;
            Assert.AreEqual(0, AbilityServices.Current.Executor.PendingContinuations);
            Assert.IsTrue(target.IsAlive);
        }

        [UnityTest]
        public IEnumerator AnEnemyOnCooldown_DoesNotSwingEveryFrame()
        {
            SpawnPlayerTarget(new Vector3(0f, 0f, 1f));
            yield return null;
            Character monster = SpawnMonster(Vector3.zero);
            int attacks = 0;
            monster.GetComponent<MeleeDelivery>().MeleeAttackStarted += (_, __) => attacks++;

            yield return new WaitForSeconds(0.8f);

            Assert.AreEqual(1, attacks, "Its melee ability has a one second cooldown, so it swings once in the first 0.8 seconds.");
        }
    }
}
