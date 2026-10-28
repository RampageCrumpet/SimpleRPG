using SimpleRPG.HitDetection;
using NUnit.Framework;
using SimpleRPG.Abilities.Integration;
using SimpleRPG.Animations;
using SimpleRPG.Gameplay;
using SimpleRPG.InventorySystem;
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
    /// Runs statuses on a real host: the status driver advances them on the network clock, a sword's rule ignites a target through a real
    /// projectile, and the burn's damage is credited to the wielder even after the wielder dies.
    /// </summary>
    public class StatusNetworkTests
    {
        private const string MeleeAttack = "Assets/Abilities/Abilities/MeleeAttack.asset";
        private const string Firebolt = "Assets/Abilities/Abilities/Firebolt.asset";
        private const string PlayerPrefab = "Assets/Prefabs/Characters/Player/TestCharacter.prefab";
        private const string TargetPrefab = "Assets/Tests/PlayMode/Prefabs/TestTarget.prefab";
        private const int StartingHealth = 100;

        private GameObject managerObject;
        private NetworkManager manager;
        private readonly List<GameObject> spawned = new List<GameObject>();
        private readonly List<Object> created = new List<Object>();
        private GameplayServices services;
        private DefinitionRegistry previousRegistry;
        private StatusDefinition burning;
        private AbilityDefinition tickAbility;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            services = GameplayServices.Reset();
            AbilityServices.Reset();

            tickAbility = TickAbility();

            // A quick burn so the tests do not wait: three stacks' worth of 0.1 s ticks over 0.6 s.
            burning = ScriptableObject.CreateInstance<StatusDefinition>();
            burning.name = "Burning";
            burning.AssignNewId();
            burning.Configure("Burning", 0.6f, 0.1f, tickAbility);
            created.Add(burning);

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
            if (previousRegistry != null) DefinitionRegistry.Instance = previousRegistry;
            foreach (Object o in created)
            {
                if (o != null) Object.DestroyImmediate(o);
            }

            created.Clear();
            GameplayServices.Reset();
            AbilityServices.Reset();
            yield return null;
        }

        private AbilityDefinition TickAbility()
        {
            AbilityDefinition ability = ScriptableObject.CreateInstance<AbilityDefinition>();
            created.Add(ability);
            ability.name = "BurnTick";
            ability.RemoveNode("activate");
            string tick = ability.AddNode(NodeTypes.StatusTick, null, "tick");
            string holder = ability.AddNode(NodeTypes.StatusHolder, null, "holder");
            string potency = ability.AddNode(NodeTypes.StatusPotency, null, "potency");
            string damage = ability.AddNode(NodeTypes.Damage, new DamageConfig { Provenance = DamageProvenance.Periodic }, "damage");
            ability.Connect(tick, BuiltInNodes.Next, damage, BuiltInNodes.In);
            ability.Connect(holder, BuiltInNodes.Value, damage, BuiltInNodes.Target);
            ability.Connect(potency, BuiltInNodes.Value, damage, BuiltInNodes.Amount);
            Assert.IsTrue(ability.Compile().Success, ability.Compile().Report.ToString());
            return ability;
        }

        private Character Spawn(string path, Vector3 position)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            Assert.IsNotNull(prefab, $"Could not load test prefab {path}.");
            GameObject instance = Object.Instantiate(prefab, position, Quaternion.identity);
            spawned.Add(instance);

            foreach (System.Type type in new[] { typeof(LocalPlayerControls), typeof(FirstPersonCharacterController), typeof(AnimatableCharacter) })
            {
                Component component = instance.GetComponent(type);
                if (component != null) Object.DestroyImmediate(component);
            }

            Character character = instance.GetComponent<Character>();
            character.Initialize();
            instance.GetComponent<NetworkObject>().Spawn();
            return character;
        }

        private static StatusManager Statuses => AbilityServices.Current.Statuses;

        private StatusApplyResult Ignite(Character target, Character from, double potency, SourceHandle? source = null) =>
            Statuses.Apply(new StatusApplication(target.Id, burning, source ?? SourceHandle.FromEntity(from.Id), from.Id, potency));

        // ----- The driver -----

        [UnityTest]
        public IEnumerator Burning_TicksOnTheRealClock_AndEndsOnItsOwn()
        {
            Character wielder = Spawn(PlayerPrefab, Vector3.zero);
            Character victim = Spawn(TargetPrefab, new Vector3(0f, 0f, 3f));
            yield return null;
            List<DamageResolvedEvent> hits = new List<DamageResolvedEvent>();
            services.Events.Subscribe<DamageResolvedEvent>(e => { if (e.Request.Target == victim.Id) hits.Add(e); });

            Assert.AreEqual(StatusApplyResult.Started, Ignite(victim, wielder, 2));
            Assert.IsTrue(Statuses.HasStatus(victim.Id, burning));

            yield return new WaitForSeconds(1.2f);

            Assert.IsFalse(Statuses.HasStatus(victim.Id, burning), "The burn ran out after 0.6 s.");
            Assert.AreEqual(6, hits.Count, 1, "A tick every 0.1 s for 0.6 s.");
            Assert.AreEqual(StartingHealth - hits.Sum(h => h.HealthBefore - h.HealthAfter), victim.Health);
            Assert.IsTrue(hits.All(h => h.Request.Provenance == DamageProvenance.Periodic));
        }

        [UnityTest]
        public IEnumerator Burning_StacksOnTheRealClock_AndTicksAllAddedUp()
        {
            Character wielder = Spawn(PlayerPrefab, Vector3.zero);
            Character victim = Spawn(TargetPrefab, new Vector3(0f, 0f, 3f));
            yield return null;
            List<int> taken = new List<int>();
            services.Events.Subscribe<DamageResolvedEvent>(e => { if (e.Request.Target == victim.Id) taken.Add(e.HealthBefore - e.HealthAfter); });

            Ignite(victim, wielder, 2);
            Ignite(victim, wielder, 2);
            Ignite(victim, wielder, 3);
            yield return new WaitForSeconds(0.25f);

            Assert.IsNotEmpty(taken);
            Assert.IsTrue(taken.All(t => t == 7), "2 + 2 + 3 in every tick.");
            Assert.AreEqual(3, Statuses.GetStatuses(victim.Id, burning).Single().StackCount);
        }

        [UnityTest]
        public IEnumerator TheDriver_IsRemoved_WhenTheAbilityServicesAre()
        {
            Character wielder = Spawn(PlayerPrefab, Vector3.zero);
            yield return null;
            Assert.IsNotEmpty(Resources.FindObjectsOfTypeAll<StatusDriver>());

            AbilityServices.Reset();
            yield return null;

            Assert.IsEmpty(Resources.FindObjectsOfTypeAll<StatusDriver>().Where(d => d != null && d.gameObject.scene.IsValid()), "No driver is left running.");
        }

        // ----- Death and leaving -----

        [UnityTest]
        public IEnumerator WhenTheBurningTargetDies_TheBurnEndsWithTargetDied()
        {
            Character wielder = Spawn(PlayerPrefab, Vector3.zero);
            Character victim = Spawn(TargetPrefab, new Vector3(0f, 0f, 3f));
            yield return null;
            RemovalReason reason = RemovalReason.Expired;
            int removed = 0;
            services.Events.Subscribe<StatusRemovedEvent>(e => { reason = e.Reason; removed++; });
            Ignite(victim, wielder, 1);

            victim.TakeDamage(new DamageInfo { Damage = StartingHealth * 2 }, BodyLocation.Torso);
            yield return null;

            Assert.IsFalse(victim.IsAlive);
            Assert.AreEqual(1, removed);
            Assert.AreEqual(RemovalReason.TargetDied, reason);
            Assert.IsFalse(Statuses.HasStatus(victim.Id, burning));
        }

        [UnityTest]
        public IEnumerator ADeadCharacter_CannotBeSetAlight()
        {
            Character wielder = Spawn(PlayerPrefab, Vector3.zero);
            Character victim = Spawn(TargetPrefab, new Vector3(0f, 0f, 3f));
            yield return null;
            victim.TakeDamage(new DamageInfo { Damage = StartingHealth * 2 }, BodyLocation.Torso);
            yield return null;

            Assert.AreEqual(StatusApplyResult.RejectedTarget, Ignite(victim, wielder, 1));
        }

        [UnityTest]
        public IEnumerator ACharacterThatLeaves_TakesItsStatusesWithIt()
        {
            Character wielder = Spawn(PlayerPrefab, Vector3.zero);
            Character victim = Spawn(TargetPrefab, new Vector3(0f, 0f, 3f));
            yield return null;
            EntityId id = victim.Id;
            Ignite(victim, wielder, 1);
            Assert.AreEqual(1, Statuses.GroupCount(id));

            victim.GetComponent<NetworkObject>().Despawn(false);
            yield return null;

            Assert.AreEqual(0, Statuses.GroupCount(id));
        }

        // ----- An ability that only a status can run -----

        [UnityTest]
        public IEnumerator ACharacter_CannotUseAStatusTickAbilityAsIfItWereASpell()
        {
            Character wielder = Spawn(PlayerPrefab, Vector3.zero);
            yield return null;
            typeof(CombatLoadout).GetField("abilities", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(wielder.Loadout, new List<AbilityDefinition> { tickAbility });

            LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex("only a status can run it"));
            bool ran = wielder.GetComponent<AbilityUser>().TryActivateOnServer(tickAbility);

            Assert.IsFalse(ran);
        }

        // ----- The whole chain -----

        [UnityTest]
        public IEnumerator ASwordThatIgnites_BurnsTheTarget_AndTheBurnIsCreditedToTheWielderEvenAfterTheyDie()
        {
            // The flaming sword: on a landed blow it casts an ability that launches a projectile and, where it lands, sets the victim alight.
            AbilityDefinition ignite = ScriptableObject.CreateInstance<AbilityDefinition>();
            created.Add(ignite);
            ignite.name = "Ignite";
            ignite.SetDetails("Ignite", 1f);
            if (ignite.Nodes.Count == 0) ignite.AddNode(NodeTypes.Activate, null, "activate");
            GameObject projectile = ((LaunchProjectileConfig)AssetDatabase.LoadAssetAtPath<AbilityDefinition>(Firebolt).Nodes.Single(n => n.TypeId == NodeTypes.LaunchProjectile).Config).Projectile;
            string launch = ignite.AddNode(NodeTypes.LaunchProjectile, new LaunchProjectileConfig { Projectile = projectile }, "launch");
            string apply = ignite.AddNode(NodeTypes.ApplyStatus, new ApplyStatusConfig { Status = burning }, "apply");
            string victimNode = ignite.AddNode(NodeTypes.EventVictim, null, "victim");
            string strength = ignite.AddNode(NodeTypes.Constant, new ConstantConfig { Value = 4f }, "strength");
            ignite.Connect("activate", BuiltInNodes.Next, launch, BuiltInNodes.In);
            ignite.Connect(launch, BuiltInNodes.OnImpact, apply, BuiltInNodes.In);
            ignite.Connect(victimNode, BuiltInNodes.Value, apply, BuiltInNodes.Target);
            ignite.Connect(strength, BuiltInNodes.Value, apply, BuiltInNodes.PotencyPort);
            Assert.IsTrue(ignite.Compile().Success, ignite.Compile().Report.ToString());

            GameplayRuleDefinition rule = ScriptableObject.CreateInstance<GameplayRuleDefinition>();
            created.Add(rule);
            rule.name = "FlamingRule";
            rule.Configure(ignite, 1f);
            WeaponData sword = ScriptableObject.CreateInstance<WeaponData>();
            created.Add(sword);
            sword.damage = new DamageInfo { Damage = 10 };
            typeof(Item).GetField("rules", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(sword, new List<GameplayRuleDefinition> { rule });
            ComboDefinition swordCombo = ComboTestSupport.CreateCombo();
            created.Add(swordCombo);
            ComboTestSupport.GiveCombo(sword, swordCombo);
            previousRegistry = DefinitionRegistry.Instance;
            DefinitionRegistry.Instance = ItemTestRegistry.With(new Item[] { sword });

            Character wielder = Spawn(PlayerPrefab, Vector3.zero);
            Character victim = Spawn(TargetPrefab, new Vector3(0f, 0f, 2f));
            yield return null;
            wielder.Loadout.DefaultWeapon = sword;
            wielder.GetComponent<Equipment>().Equip(EquipmentSlot.mainHand, sword, 700);
            List<DamageRequest> periodic = new List<DamageRequest>();
            services.Events.Subscribe<DamageResolvedEvent>(e => { if (e.Request.Provenance == DamageProvenance.Periodic) periodic.Add(e.Request); });

            // The blow, the sword's rule, the projectile and the ignition.
            ComboTestSupport.UseManualClock(wielder);
            Assert.IsTrue(wielder.GetComponent<AbilityUser>().TryActivateOnServer(AssetDatabase.LoadAssetAtPath<AbilityDefinition>(MeleeAttack)));
            ComboTestSupport.Advance(wielder, ComboTestSupport.InsideHitWindow);
            wielder.GetComponentInChildren<SimpleRPG.Weapon>().TargetHit(victim, BodyLocation.Torso);
            yield return null;
            Object.FindObjectsByType<Projectile>().Single().TargetHit(victim, BodyLocation.Torso);
            yield return null;

            Assert.IsTrue(Statuses.HasStatus(victim.Id, burning), "The sword set the victim alight.");
            StackSnapshot stack = Statuses.GetStatuses(victim.Id, burning).Single().Stacks[0];
            Assert.AreEqual(wielder.Id, stack.Instigator);
            Assert.AreEqual(SourceHandle.FromItem(new ItemInstanceId(700)), stack.Source, "The sword is the source of the burn; the wielder is credited with it.");
            Assert.AreEqual(4d, stack.Potency);

            // The wielder dies while the target burns.
            wielder.TakeDamage(new DamageInfo { Damage = StartingHealth * 2 }, BodyLocation.Torso);
            yield return new WaitForSeconds(0.9f);

            Assert.IsFalse(wielder.IsAlive);
            Assert.IsNotEmpty(periodic, "The target kept burning after the wielder died.");
            Assert.IsTrue(periodic.All(r => r.Instigator == wielder.Id), "The burn is still credited to the wielder.");
            Assert.IsFalse(Statuses.HasStatus(victim.Id, burning), "It burned out.");
        }
    }
}
