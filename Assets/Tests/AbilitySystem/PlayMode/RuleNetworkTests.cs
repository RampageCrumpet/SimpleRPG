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
    /// Runs the flaming-sword rule on a real host: a sword that has a chance to cast Fireball when its wielder lands a blow. The rule, the
    /// Fireball, the equipment, the melee swing, the projectile and the combat pipeline are all the real ones.
    /// </summary>
    public class RuleNetworkTests
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
        private WeaponData sword;
        private WeaponData dagger;
        private GameplayRuleDefinition rule;
        private AbilityDefinition meleeAbility;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            services = GameplayServices.Reset();
            AbilityServices.Reset();

            meleeAbility = AssetDatabase.LoadAssetAtPath<AbilityDefinition>(MeleeAttack);
            Assert.IsNotNull(meleeAbility);

            rule = ScriptableObject.CreateInstance<GameplayRuleDefinition>();
            rule.name = "FlamingSwordRule";
            rule.Configure(FireballAbility(), 1f);
            created.Add(rule);

            sword = Weapon(10, rule);
            dagger = Weapon(6);
            previousRegistry = DefinitionRegistry.Instance;
            DefinitionRegistry.Instance = ItemTestRegistry.With(new Item[] { sword, dagger });

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
            DefinitionRegistry.Instance = previousRegistry;
            foreach (Object o in created)
            {
                if (o != null) Object.DestroyImmediate(o);
            }

            created.Clear();
            GameplayServices.Reset();
            AbilityServices.Reset();
            yield return null;
        }

        private WeaponData Weapon(int damage, params GameplayRuleDefinition[] rules)
        {
            WeaponData weapon = ScriptableObject.CreateInstance<WeaponData>();
            weapon.damage = new DamageInfo { Damage = damage };
            typeof(Item).GetField("rules", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(weapon, rules.ToList());
            ComboDefinition combo = ComboTestSupport.CreateCombo();
            ComboTestSupport.GiveCombo(weapon, combo);
            created.Add(combo);
            created.Add(weapon);
            return weapon;
        }

        /// <summary>
        /// Fireball: launch the shipped Firebolt's projectile, and on impact damage everything within 3 units of where it landed except the caster.
        /// The same definition is what a player would cast.
        /// </summary>
        private AbilityDefinition FireballAbility()
        {
            GameObject projectile = ((LaunchProjectileConfig)AssetDatabase.LoadAssetAtPath<AbilityDefinition>(Firebolt).Nodes.Single(n => n.TypeId == NodeTypes.LaunchProjectile).Config).Projectile;
            AbilityDefinition ability = ScriptableObject.CreateInstance<AbilityDefinition>();
            created.Add(ability);
            ability.name = "Fireball";
            ability.SetDetails("Fireball", 1f);
            if (ability.Nodes.Count == 0) ability.AddNode(NodeTypes.Activate, null, "activate");
            string launch = ability.AddNode(NodeTypes.LaunchProjectile, new LaunchProjectileConfig { Projectile = projectile }, "launch");
            string area = ability.AddNode(NodeTypes.EntitiesInRadius, new EntitiesInRadiusConfig { Radius = 3f, ExcludeInstigator = true }, "area");
            string each = ability.AddNode(NodeTypes.ForEachTarget, null, "each");
            string current = ability.AddNode(NodeTypes.CurrentTarget, null, "current");
            string amount = ability.AddNode(NodeTypes.Constant, new ConstantConfig { Value = 15f }, "amount");
            string damage = ability.AddNode(NodeTypes.Damage, null, "damage");
            ability.Connect("activate", BuiltInNodes.Next, launch, BuiltInNodes.In);
            ability.Connect(launch, BuiltInNodes.OnImpact, each, BuiltInNodes.In);
            ability.Connect(area, BuiltInNodes.Value, each, BuiltInNodes.Targets);
            ability.Connect(each, BuiltInNodes.Each, damage, BuiltInNodes.In);
            ability.Connect(current, BuiltInNodes.Value, damage, BuiltInNodes.Target);
            ability.Connect(amount, BuiltInNodes.Value, damage, BuiltInNodes.Amount);
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

        private static Equipment EquipmentOf(Character character) => character.GetComponent<Equipment>();

        private static int Procs() => AbilityServices.Current.Executor.PendingContinuations;

        private static List<Projectile> LiveProjectiles() => Object.FindObjectsByType<Projectile>().ToList();

        /// <summary>
        /// Lands a real blow: starts the melee ability, opens its hit window and touches the victim with the weapon.
        /// </summary>
        private static void SwingAt(Character attacker, Character victim)
        {
            if (!(attacker.GetComponent<ComboMeleeDelivery>().Clock is ManualGameplayClock))
            {
                ComboTestSupport.UseManualClock(attacker);
            }

            Assert.IsTrue(attacker.GetComponent<AbilityUser>().TryActivateOnServer(AssetDatabase.LoadAssetAtPath<AbilityDefinition>(MeleeAttack)));
            ComboTestSupport.Advance(attacker, ComboTestSupport.InsideHitWindow);
            attacker.GetComponentInChildren<SimpleRPG.Weapon>().TargetHit(victim, BodyLocation.Torso);
        }

        // ----- The flaming sword -----

        [UnityTest]
        public IEnumerator ASwordBlow_CastsFireball_AtTheVictim()
        {
            Character wielder = Spawn(PlayerPrefab, Vector3.zero);
            Character victim = Spawn(TargetPrefab, new Vector3(0f, 0f, 2f));
            Character neighbour = Spawn(TargetPrefab, new Vector3(1f, 0f, 2f));
            Character farAway = Spawn(TargetPrefab, new Vector3(20f, 0f, 20f));
            yield return null;
            wielder.Loadout.DefaultWeapon = sword;
            Assert.IsTrue(EquipmentOf(wielder).Equip(EquipmentSlot.mainHand, sword, 500));

            SwingAt(wielder, victim);
            yield return null;

            Assert.AreEqual(StartingHealth - 15, victim.Health, "The blow itself (10 x Melee Power 1.5).");
            List<Projectile> fireballs = LiveProjectiles();
            Assert.AreEqual(1, fireballs.Count, "The sword's rule cast one Fireball.");

            fireballs[0].TargetHit(victim, BodyLocation.Torso);
            yield return null;

            Assert.AreEqual(StartingHealth - 15 - 15, victim.Health, "The blow, then the explosion.");
            Assert.AreEqual(StartingHealth - 15, neighbour.Health, "Everything near the explosion is hurt.");
            Assert.AreEqual(StartingHealth, farAway.Health);
            Assert.AreEqual(StartingHealth, wielder.Health, "The wielder is not hurt by their own sword's Fireball.");
        }

        [UnityTest]
        public IEnumerator TheExplosion_IsNotAnotherWeaponHit_SoTheSwordDoesNotCastAgain()
        {
            Character wielder = Spawn(PlayerPrefab, Vector3.zero);
            Character victim = Spawn(TargetPrefab, new Vector3(0f, 0f, 2f));
            yield return null;
            wielder.Loadout.DefaultWeapon = sword;
            EquipmentOf(wielder).Equip(EquipmentSlot.mainHand, sword, 500);
            int weaponHits = 0;
            services.Events.Subscribe<WeaponHitConfirmedEvent>(e => weaponHits++);

            SwingAt(wielder, victim);
            yield return null;
            LiveProjectiles().Single().TargetHit(victim, BodyLocation.Torso);
            yield return null;

            Assert.AreEqual(1, weaponHits);
            yield return null;
            Assert.IsEmpty(LiveProjectiles(), "The first Fireball is spent and the explosion did not cast a second one.");
        }

        [UnityTest]
        public IEnumerator TheProcsKill_IsCreditedToTheWielder()
        {
            Character wielder = Spawn(PlayerPrefab, Vector3.zero);
            Character victim = Spawn(TargetPrefab, new Vector3(0f, 0f, 2f));
            yield return null;
            wielder.Loadout.DefaultWeapon = sword;
            EquipmentOf(wielder).Equip(EquipmentSlot.mainHand, sword, 500);
            victim.TakeDamage(new DamageInfo { Damage = StartingHealth - 20 }, BodyLocation.Torso);
            DeathContext death = null;
            services.Events.Subscribe<EntityDiedEvent>(e => death = e.Context);

            SwingAt(wielder, victim);
            yield return null;
            LiveProjectiles().Single().TargetHit(victim, BodyLocation.Torso);
            yield return null;

            Assert.IsNotNull(death, "10 from the blow and 15 from the explosion finish a target with 20 left.");
            Assert.AreEqual(wielder.Id, death.Killer);
            Assert.AreEqual(DamageProvenance.Spell, death.KillingProvenance);
        }

        [UnityTest]
        public IEnumerator AnItemWithoutRules_CastsNothing()
        {
            Character wielder = Spawn(PlayerPrefab, Vector3.zero);
            Character victim = Spawn(TargetPrefab, new Vector3(0f, 0f, 2f));
            yield return null;
            wielder.Loadout.DefaultWeapon = dagger;
            EquipmentOf(wielder).Equip(EquipmentSlot.mainHand, dagger, 501);

            SwingAt(wielder, victim);
            yield return null;

            Assert.AreEqual(StartingHealth - 9, victim.Health); // 6 x Melee Power 1.5
            Assert.IsEmpty(LiveProjectiles());
        }

        [UnityTest]
        public IEnumerator TakingTheSwordOff_StopsTheProcs()
        {
            Character wielder = Spawn(PlayerPrefab, Vector3.zero);
            Character victim = Spawn(TargetPrefab, new Vector3(0f, 0f, 2f));
            yield return null;
            wielder.Loadout.DefaultWeapon = sword;
            EquipmentOf(wielder).Equip(EquipmentSlot.mainHand, sword, 500);
            EquipmentOf(wielder).Unequip(EquipmentSlot.mainHand);
            Assert.AreEqual(0, AbilityServices.Current.Rules.ActiveRuleCount(wielder.Id));

            SwingAt(wielder, victim);
            yield return null;

            Assert.IsEmpty(LiveProjectiles(), "The sword is off, so its rule is gone.");
        }

        [UnityTest]
        public IEnumerator SwappingTheSwordForADagger_ChangesWhichRulesApply()
        {
            Character wielder = Spawn(PlayerPrefab, Vector3.zero);
            yield return null;
            EquipmentOf(wielder).Equip(EquipmentSlot.mainHand, sword, 500);
            Assert.AreEqual(1, AbilityServices.Current.Rules.ActiveRuleCount(wielder.Id));

            EquipmentOf(wielder).Equip(EquipmentSlot.mainHand, dagger, 501);

            Assert.AreEqual(0, AbilityServices.Current.Rules.ActiveRuleCount(wielder.Id));
        }

        [UnityTest]
        public IEnumerator OnlyTheWielderSwordProcs_NotAnotherCharactersBlow()
        {
            Character wielder = Spawn(PlayerPrefab, Vector3.zero);
            Character other = Spawn(PlayerPrefab, new Vector3(10f, 0f, 0f));
            Character victim = Spawn(TargetPrefab, new Vector3(10f, 0f, 2f));
            yield return null;
            EquipmentOf(wielder).Equip(EquipmentSlot.mainHand, sword, 500);
            other.Loadout.DefaultWeapon = dagger;

            SwingAt(other, victim);
            yield return null;

            Assert.IsEmpty(LiveProjectiles(), "The sword belongs to the other character.");
        }

        [UnityTest]
        public IEnumerator ACharacterThatLeaves_TakesItsRulesWithIt()
        {
            Character wielder = Spawn(PlayerPrefab, Vector3.zero);
            yield return null;
            EquipmentOf(wielder).Equip(EquipmentSlot.mainHand, sword, 500);
            EntityId id = wielder.Id;
            Assert.AreEqual(1, AbilityServices.Current.Rules.ActiveRuleCount(id));

            wielder.GetComponent<NetworkObject>().Despawn(false);
            yield return null;

            Assert.AreEqual(0, AbilityServices.Current.Rules.ActiveRuleCount(id));
        }

        [UnityTest]
        public IEnumerator ARuleEngineStartedLate_StillKnowsWhatIsAlreadyWorn()
        {
            Character wielder = Spawn(PlayerPrefab, Vector3.zero);
            yield return null;
            EquipmentOf(wielder).Equip(EquipmentSlot.mainHand, sword, 500);

            // Throw the ability services away and let them be rebuilt, as happens when a game starts after its characters exist.
            AbilityServices.Reset();

            Assert.AreEqual(1, AbilityServices.Current.Rules.ActiveRuleCount(wielder.Id));
        }

        // ----- The seeded proc rate -----

        [UnityTest]
        public IEnumerator ATenPercentSword_ProcsOnTheBlowsTheSeededStreamPicks()
        {
            rule.Configure(rule.Ability, 0.1f);
            services.Random = new SeededRandom(2025);
            Character wielder = Spawn(PlayerPrefab, Vector3.zero);
            Character victim = Spawn(TargetPrefab, new Vector3(0f, 0f, 2f));
            yield return null;
            EquipmentOf(wielder).Equip(EquipmentSlot.mainHand, sword, 500);

            // One real blow, then confirmed blows raised directly, so the rule, the equipment and the real projectile delivery are exercised
            // without waiting out the melee ability's cooldown for every swing.
            SwingAt(wielder, victim);
            ComboTestSupport.Advance(wielder, 0.3); // the hit window closes, so only Fireballs are left waiting on the executor
            for (int i = 0; i < 199; i++)
            {
                services.Events.Enqueue(new WeaponHitConfirmedEvent(wielder.Id, victim.Id, 1, 1, default));
                services.Events.Drain();
            }

            SeededRandom replay = new SeededRandom(2025);
            int expected = 0;
            for (int i = 0; i < 200; i++)
            {
                if (replay.NextDouble() < 0.1) expected++;
            }

            Assert.AreEqual(expected, Procs(), "The blows that proc are exactly the ones the seed picks.");
            Assert.AreEqual(20, Procs(), 12, "About one blow in ten.");
        }

        [UnityTest]
        public IEnumerator TheShippedFireboltAndMelee_StillWorkAlongsideRules()
        {
            Character wielder = Spawn(PlayerPrefab, Vector3.zero);
            Character victim = Spawn(TargetPrefab, new Vector3(0f, 0f, 2f));
            yield return null;
            wielder.Loadout.DefaultWeapon = dagger;
            EquipmentOf(wielder).Equip(EquipmentSlot.mainHand, dagger, 501);

            Assert.IsTrue(wielder.GetComponent<AbilityUser>().TryActivateOnServer(AssetDatabase.LoadAssetAtPath<AbilityDefinition>(Firebolt)));
            yield return null;
            LiveProjectiles().Single().TargetHit(victim, BodyLocation.Torso);
            yield return null;

            Assert.AreEqual(StartingHealth - 20, victim.Health);
        }
    }
}
