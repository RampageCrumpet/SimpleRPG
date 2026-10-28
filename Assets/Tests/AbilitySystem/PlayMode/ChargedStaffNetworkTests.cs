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

namespace SimpleRPG.Abilities.Tests.PlayMode
{
    /// <summary>
    /// The charged staff on a real host: it holds three Firebolt charges for a wielder who has never learned Firebolt. Charges belong to each staff,
    /// are spent only by the server, are shown to the owner, and are never refilled.
    /// </summary>
    public class ChargedStaffNetworkTests
    {
        private const string Firebolt = "Assets/Abilities/Abilities/Firebolt.asset";
        private const string PlayerPrefab = "Assets/Prefabs/Characters/Player/TestCharacter.prefab";
        private const string TargetPrefab = "Assets/Tests/PlayMode/Prefabs/TestTarget.prefab";
        private const string MagicPower = "test.attr.magicpower";
        private const int StartingHealth = 100;

        private GameObject managerObject;
        private NetworkManager manager;
        private readonly List<GameObject> spawned = new List<GameObject>();
        private readonly List<Object> created = new List<Object>();
        private GameplayServices services;
        private DefinitionRegistry previousRegistry;
        private AbilityDefinition staffBolt;
        private Item staff;
        private Item ring;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            services = GameplayServices.Reset();
            AbilityServices.Reset();

            staffBolt = StaffBolt();
            staff = Staff(startingCharges: 3, new AttributeRequirement[0]);

            // A ring that makes its wearer cleverer, for the eligibility tests.
            ring = ScriptableObject.CreateInstance<Item>();
            created.Add(ring);
            Set(ring, "grants", new List<GrantSpec> { new GrantSpec { AttributeKey = PrimaryAttributes.Intelligence.Key, Layer = ModifierLayer.Add, Value = 3 } });

            previousRegistry = DefinitionRegistry.Instance;
            DefinitionRegistry.Instance = ItemTestRegistry.With(new[] { staff, ring });

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

        private static void Set(Item item, string field, object value) =>
            typeof(Item).GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(item, value);

        /// <summary>
        /// A Firebolt with a short cooldown, reading its strength from the source's magic power: 12 + 0.8 x that.
        /// </summary>
        private AbilityDefinition StaffBolt()
        {
            GameObject projectile = ((LaunchProjectileConfig)AssetDatabase.LoadAssetAtPath<AbilityDefinition>(Firebolt).Nodes.Single(n => n.TypeId == NodeTypes.LaunchProjectile).Config).Projectile;
            AbilityDefinition ability = ScriptableObject.CreateInstance<AbilityDefinition>();
            created.Add(ability);
            ability.name = "StaffBolt";
            ability.SetDetails("Staff Bolt", 0.2f);
            if (ability.Nodes.Count == 0) ability.AddNode(NodeTypes.Activate, null, "activate");
            string launch = ability.AddNode(NodeTypes.LaunchProjectile, new LaunchProjectileConfig { Projectile = projectile }, "launch");
            string damage = ability.AddNode(NodeTypes.Damage, null, "damage");
            string victim = ability.AddNode(NodeTypes.EventVictim, null, "victim");
            string power = ability.AddNode(NodeTypes.Attribute, new AttributeConfig { Kind = AttributeBindingKind.Source, AttributeKey = MagicPower }, "power");
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

        private Item Staff(int startingCharges, AttributeRequirement[] requirements)
        {
            Item item = ScriptableObject.CreateInstance<Item>();
            created.Add(item);
            ItemAbilityGrant grant = new ItemAbilityGrant { Ability = staffBolt, ChargeCost = 1 };
            grant.Requirements.AddRange(requirements);
            Set(item, "startingCharges", startingCharges);
            Set(item, "grantedAbilities", new List<ItemAbilityGrant> { grant });
            Set(item, "fixedAttributes", new List<AttributeValueSpec> { new AttributeValueSpec { AttributeKey = MagicPower, Value = 20 } });
            return item;
        }

        private Character Spawn(string path, Vector3 position)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            Assert.IsNotNull(prefab, $"Could not load test prefab {path}.");
            GameObject instance = Object.Instantiate(prefab, position, Quaternion.identity);
            spawned.Add(instance);

            foreach (System.Type type in new[] { typeof(RegisterWithUI), typeof(FirstPersonCharacterController), typeof(AnimatableCharacter) })
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

        private static AbilityUser UserOf(Character character) => character.GetComponent<AbilityUser>();

        private static List<Projectile> LiveProjectiles() => Object.FindObjectsByType<Projectile>().ToList();

        private static List<Equipment.ChargeEntry> ReplicatedCharges(Equipment equipment)
        {
            NetworkList<Equipment.ChargeEntry> list = (NetworkList<Equipment.ChargeEntry>)typeof(Equipment).GetField("charges", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(equipment);
            List<Equipment.ChargeEntry> entries = new List<Equipment.ChargeEntry>();
            for (int i = 0; i < list.Count; i++) entries.Add(list[i]);
            return entries;
        }

        // ----- Charges -----

        [UnityTest]
        public IEnumerator EquippingAStaff_GivesItsStartingCharges_AndTheOwnerIsToldThem()
        {
            Character wielder = Spawn(PlayerPrefab, Vector3.zero);
            yield return null;

            EquipmentOf(wielder).Equip(EquipmentSlot.mainHand, staff, 800);

            Assert.AreEqual(3, EquipmentOf(wielder).GetCharges(EquipmentSlot.mainHand));
            List<Equipment.ChargeEntry> replicated = ReplicatedCharges(EquipmentOf(wielder));
            Assert.AreEqual(1, replicated.Count);
            Assert.AreEqual(800u, replicated[0].InstanceId);
            Assert.AreEqual(3, replicated[0].Charges);
        }

        [UnityTest]
        public IEnumerator TheStaffCastsThreeTimes_ThenIsRefused_AndTheOwnersCountFollows()
        {
            Character wielder = Spawn(PlayerPrefab, Vector3.zero);
            yield return null;
            EquipmentOf(wielder).Equip(EquipmentSlot.mainHand, staff, 800);
            List<ItemCastResult> results = new List<ItemCastResult>();

            for (int i = 0; i < 4; i++)
            {
                results.Add(UserOf(wielder).TryActivateItemOnServer(EquipmentSlot.mainHand, 0));
                yield return new WaitForSeconds(0.25f);
            }

            CollectionAssert.AreEqual(new[] { ItemCastResult.Cast, ItemCastResult.Cast, ItemCastResult.Cast, ItemCastResult.NotEnoughCharges }, results);
            Assert.AreEqual(0, EquipmentOf(wielder).GetCharges(EquipmentSlot.mainHand));
            Assert.AreEqual(0, ReplicatedCharges(EquipmentOf(wielder)).Single().Charges, "The owner sees the staff run dry.");
            Assert.AreEqual(3, AbilityServices.Current.Executor.PendingContinuations, "Three real Firebolts were launched, not four.");
        }

        [UnityTest]
        public IEnumerator TheWielder_NeverNeededToKnowFirebolt()
        {
            Character wielder = Spawn(PlayerPrefab, Vector3.zero);
            yield return null;
            EquipmentOf(wielder).Equip(EquipmentSlot.mainHand, staff, 800);

            Assert.IsFalse(wielder.GraphAbilities.Contains(staffBolt), "The staff's spell is not on the wielder's own bar.");
            Assert.AreEqual(ItemCastResult.Cast, UserOf(wielder).TryActivateItemOnServer(EquipmentSlot.mainHand, 0));
        }

        [UnityTest]
        public IEnumerator TheBolt_UsesTheStaffsOwnMagicPower()
        {
            Character wielder = Spawn(PlayerPrefab, Vector3.zero);
            Character victim = Spawn(TargetPrefab, new Vector3(0f, 0f, 5f));
            yield return null;
            EquipmentOf(wielder).Equip(EquipmentSlot.mainHand, staff, 800);

            UserOf(wielder).TryActivateItemOnServer(EquipmentSlot.mainHand, 0);
            yield return null;
            LiveProjectiles().Single().TargetHit(victim, BodyLocation.Torso);
            yield return null;

            Assert.AreEqual(StartingHealth - 28, victim.Health, "12 + 0.8 x the staff's 20; the wielder has no magic power of their own.");
        }

        [UnityTest]
        public IEnumerator TwoStaffsOfTheSameKind_HaveSeparateCharges()
        {
            Character first = Spawn(PlayerPrefab, Vector3.zero);
            Character second = Spawn(PlayerPrefab, new Vector3(10f, 0f, 0f));
            yield return null;
            EquipmentOf(first).Equip(EquipmentSlot.mainHand, staff, 800);
            EquipmentOf(second).Equip(EquipmentSlot.mainHand, staff, 801);

            UserOf(first).TryActivateItemOnServer(EquipmentSlot.mainHand, 0);

            Assert.AreEqual(2, EquipmentOf(first).GetCharges(EquipmentSlot.mainHand));
            Assert.AreEqual(3, EquipmentOf(second).GetCharges(EquipmentSlot.mainHand), "Casting from one staff does not touch the other.");
        }

        // ----- No recharging -----

        [UnityTest]
        public IEnumerator TakingTheStaffOffAndPuttingItBackOn_DoesNotRefillIt()
        {
            Character wielder = Spawn(PlayerPrefab, Vector3.zero);
            yield return null;
            EquipmentOf(wielder).Equip(EquipmentSlot.mainHand, staff, 800);
            UserOf(wielder).TryActivateItemOnServer(EquipmentSlot.mainHand, 0);
            Assert.AreEqual(2, EquipmentOf(wielder).GetCharges(EquipmentSlot.mainHand));

            EquipmentOf(wielder).Unequip(EquipmentSlot.mainHand);
            Assert.AreEqual(-1, EquipmentOf(wielder).GetCharges(EquipmentSlot.mainHand), "Nothing is equipped to ask.");
            EquipmentOf(wielder).Equip(EquipmentSlot.mainHand, staff, 800);

            Assert.AreEqual(2, EquipmentOf(wielder).GetCharges(EquipmentSlot.mainHand));
            Assert.AreEqual(2, ReplicatedCharges(EquipmentOf(wielder)).Single().Charges);
        }

        [UnityTest]
        public IEnumerator HandingTheStaffToAnotherCharacter_KeepsItsChargesLeft()
        {
            Character first = Spawn(PlayerPrefab, Vector3.zero);
            Character second = Spawn(PlayerPrefab, new Vector3(10f, 0f, 0f));
            yield return null;
            EquipmentOf(first).Equip(EquipmentSlot.mainHand, staff, 800);
            UserOf(first).TryActivateItemOnServer(EquipmentSlot.mainHand, 0);
            EquipmentOf(first).Unequip(EquipmentSlot.mainHand);

            EquipmentOf(second).Equip(EquipmentSlot.mainHand, staff, 800);

            Assert.AreEqual(2, EquipmentOf(second).GetCharges(EquipmentSlot.mainHand), "The charges belong to the staff, not to whoever holds it.");
        }

        [UnityTest]
        public IEnumerator ASpentStaff_StaysSpent_NoMatterWhatTheWielderDoes()
        {
            Character wielder = Spawn(PlayerPrefab, Vector3.zero);
            yield return null;
            EquipmentOf(wielder).Equip(EquipmentSlot.mainHand, staff, 800);
            for (int i = 0; i < 3; i++)
            {
                UserOf(wielder).TryActivateItemOnServer(EquipmentSlot.mainHand, 0);
                yield return new WaitForSeconds(0.2f);
            }

            EquipmentOf(wielder).Unequip(EquipmentSlot.mainHand);
            EquipmentOf(wielder).Equip(EquipmentSlot.mainHand, staff, 800);
            yield return new WaitForSeconds(0.2f);

            Assert.AreEqual(0, EquipmentOf(wielder).GetCharges(EquipmentSlot.mainHand));
            Assert.AreEqual(ItemCastResult.NotEnoughCharges, UserOf(wielder).TryActivateItemOnServer(EquipmentSlot.mainHand, 0));
        }

        // ----- Eligibility -----

        [UnityTest]
        public IEnumerator AStaffThatNeedsIntelligence_RefusesAnUnderqualifiedWielder_UntilARingMakesThemQualified()
        {
            Item demanding = Staff(3, new[] { new AttributeRequirement { AttributeKey = PrimaryAttributes.Intelligence.Key, Minimum = 12 } });
            DefinitionRegistry.Instance = ItemTestRegistry.With(new[] { staff, ring, demanding });
            Character wielder = Spawn(PlayerPrefab, Vector3.zero);
            yield return null;
            EquipmentOf(wielder).Equip(EquipmentSlot.mainHand, demanding, 802);

            Assert.AreEqual(ItemCastResult.NotEligible, UserOf(wielder).TryActivateItemOnServer(EquipmentSlot.mainHand, 0), "Intelligence 10 is below 12.");
            Assert.AreEqual(3, EquipmentOf(wielder).GetCharges(EquipmentSlot.mainHand), "A refusal costs nothing.");

            EquipmentOf(wielder).Equip(EquipmentSlot.neck, ring, 803);
            Assert.AreEqual(13d, wielder.Attributes.Get(PrimaryAttributes.Intelligence));

            Assert.AreEqual(ItemCastResult.Cast, UserOf(wielder).TryActivateItemOnServer(EquipmentSlot.mainHand, 0));
            Assert.AreEqual(2, EquipmentOf(wielder).GetCharges(EquipmentSlot.mainHand));
        }

        // ----- Refusals -----

        [UnityTest]
        public IEnumerator ADeadWielder_CannotCast()
        {
            Character wielder = Spawn(PlayerPrefab, Vector3.zero);
            yield return null;
            EquipmentOf(wielder).Equip(EquipmentSlot.mainHand, staff, 800);
            wielder.TakeDamage(new DamageInfo { Damage = StartingHealth * 2 }, BodyLocation.Torso);
            yield return null;

            Assert.AreEqual(ItemCastResult.UserDead, UserOf(wielder).TryActivateItemOnServer(EquipmentSlot.mainHand, 0));
            Assert.AreEqual(3, EquipmentOf(wielder).GetCharges(EquipmentSlot.mainHand));
        }

        [UnityTest]
        public IEnumerator NothingEquippedInTheSlot_OrNoSuchAbility_IsRefused()
        {
            Character wielder = Spawn(PlayerPrefab, Vector3.zero);
            yield return null;
            EquipmentOf(wielder).Equip(EquipmentSlot.mainHand, staff, 800);

            Assert.AreEqual(ItemCastResult.NoItem, UserOf(wielder).TryActivateItemOnServer(EquipmentSlot.offhand, 0));
            Assert.AreEqual(ItemCastResult.NoSuchAbility, UserOf(wielder).TryActivateItemOnServer(EquipmentSlot.mainHand, 1));
            Assert.AreEqual(ItemCastResult.NoSuchAbility, UserOf(wielder).TryActivateItemOnServer(EquipmentSlot.mainHand, -1));
            Assert.AreEqual(3, EquipmentOf(wielder).GetCharges(EquipmentSlot.mainHand));
        }

        [UnityTest]
        public IEnumerator ACastInsideTheCooldown_IsRefused_AndNotCharged()
        {
            Character wielder = Spawn(PlayerPrefab, Vector3.zero);
            yield return null;
            EquipmentOf(wielder).Equip(EquipmentSlot.mainHand, staff, 800);

            ItemCastResult first = UserOf(wielder).TryActivateItemOnServer(EquipmentSlot.mainHand, 0);
            ItemCastResult second = UserOf(wielder).TryActivateItemOnServer(EquipmentSlot.mainHand, 0);

            Assert.AreEqual(ItemCastResult.Cast, first);
            Assert.AreEqual(ItemCastResult.OnCooldown, second);
            Assert.AreEqual(2, EquipmentOf(wielder).GetCharges(EquipmentSlot.mainHand));
        }

        [UnityTest]
        public IEnumerator ADespawnedWielder_CannotCast()
        {
            Character wielder = Spawn(PlayerPrefab, Vector3.zero);
            yield return null;
            EquipmentOf(wielder).Equip(EquipmentSlot.mainHand, staff, 800);
            AbilityUser user = UserOf(wielder);
            wielder.GetComponent<NetworkObject>().Despawn(false);
            yield return null;

            Assert.AreEqual(ItemCastResult.NotAuthority, user.TryActivateItemOnServer(EquipmentSlot.mainHand, 0));
        }

        // ----- Authority -----

        [UnityTest]
        public IEnumerator TheChargesAreWrittenByTheServerAndReadOnlyByTheOwner()
        {
            Character wielder = Spawn(PlayerPrefab, Vector3.zero);
            yield return null;
            NetworkList<Equipment.ChargeEntry> list = (NetworkList<Equipment.ChargeEntry>)typeof(Equipment).GetField("charges", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(EquipmentOf(wielder));

            Assert.AreEqual(NetworkVariableReadPermission.Owner, list.ReadPerm, "Other players are not told how many charges your staff has.");
            Assert.AreEqual(NetworkVariableWritePermission.Server, list.WritePerm);
        }

        [Test]
        public void NothingOnTheEquipmentOrTheUser_LetsAClientSetOrRaiseCharges()
        {
            // A client's request names a slot and a position and nothing more. The only public ways in are those, and a read.
            string[] chargeMethods = typeof(Equipment).GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly)
                .Select(m => m.Name)
                .Where(n => n.Contains("Charge"))
                .ToArray();
            CollectionAssert.AreEqual(new[] { "GetCharges" }, chargeMethods);

            MethodInfo rpc = typeof(AbilityUser).GetMethod("ActivateItemServerRpc", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(rpc);
            CollectionAssert.AreEqual(new[] { typeof(int), typeof(int) }, rpc.GetParameters().Select(p => p.ParameterType).ToArray(), "Only a slot and an ability position cross the network.");
        }

        [UnityTest]
        public IEnumerator RequestingAnItemCast_OnTheServer_RunsTheSameChecks()
        {
            Character wielder = Spawn(PlayerPrefab, Vector3.zero);
            yield return null;
            EquipmentOf(wielder).Equip(EquipmentSlot.mainHand, staff, 800);

            Assert.IsTrue(UserOf(wielder).RequestActivateItem(EquipmentSlot.mainHand, 0));
            Assert.IsFalse(UserOf(wielder).RequestActivateItem(EquipmentSlot.mainHand, 0), "Still cooling down.");
            Assert.AreEqual(2, EquipmentOf(wielder).GetCharges(EquipmentSlot.mainHand));
        }
    }
}
