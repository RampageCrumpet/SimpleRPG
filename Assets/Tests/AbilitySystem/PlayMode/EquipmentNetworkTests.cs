using NUnit.Framework;
using SimpleRPG.Gameplay;
using SimpleRPG.InventorySystem;
using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;
using UnityEngine.TestTools;

namespace SimpleRPG.Abilities.Tests.PlayMode
{
    /// <summary>
    /// Checks server-authoritative equipment on a real host: equipping, replacing, unequipping, the events each raises, and that the
    /// main hand weapon becomes the character's weapon. Characters are spawned from the test target prefab, which has an Equipment component.
    /// </summary>
    public class EquipmentNetworkTests
    {
        private const string TargetPrefab = "Assets/Tests/PlayMode/Prefabs/TestTarget.prefab";

        private GameObject managerObject;
        private NetworkManager manager;
        private GameObject characterObject;
        private Character character;
        private Equipment equipment;
        private GameplayServices services;
        private List<string> log;
        private DefinitionRegistry previousRegistry;
        private WeaponData sword;
        private WeaponData dagger;
        private Item amulet;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            services = GameplayServices.Reset();
            log = new List<string>();
            services.Events.Subscribe<ItemEquippedEvent>(e => log.Add("equipped " + e.Item.Value + " in " + e.Slot));
            services.Events.Subscribe<ItemUnequippedEvent>(e => log.Add("unequipped " + e.Item.Value + " from " + e.Slot + " (" + e.Reason + ")"));

            sword = CreateWeapon(25);
            dagger = CreateWeapon(8);
            amulet = ScriptableObject.CreateInstance<Item>();
            previousRegistry = DefinitionRegistry.Instance;
            DefinitionRegistry.Instance = ItemTestRegistry.With(new Item[] { sword, dagger, amulet });

            managerObject = new GameObject("TestNetworkManager");
            UnityTransport transport = managerObject.AddComponent<UnityTransport>();
            manager = managerObject.AddComponent<NetworkManager>();
            manager.NetworkConfig = new NetworkConfig { NetworkTransport = transport };

            Assert.IsTrue(manager.StartHost(), "The test host should start.");
            yield return null;

#if UNITY_EDITOR
            GameObject prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(TargetPrefab);
            Assert.IsNotNull(prefab, $"Could not load test prefab {TargetPrefab}.");
            characterObject = Object.Instantiate(prefab);
            character = characterObject.GetComponent<Character>();
            equipment = characterObject.GetComponent<Equipment>();
            Assert.IsNotNull(equipment, "The test target prefab needs an Equipment component.");
            character.Initialize();
            characterObject.GetComponent<NetworkObject>().Spawn();
#else
            Assert.Ignore("The test prefab can only be loaded in the editor.");
#endif
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
            if (characterObject != null) Object.Destroy(characterObject);
            DefinitionRegistry.Instance = previousRegistry;
            Object.DestroyImmediate(sword);
            Object.DestroyImmediate(dagger);
            Object.DestroyImmediate(amulet);
            GameplayServices.Reset();

            yield return null;
        }

        private static WeaponData CreateWeapon(int damage)
        {
            WeaponData weapon = ScriptableObject.CreateInstance<WeaponData>();
            weapon.damage = new DamageInfo { Damage = damage };
            return weapon;
        }

        [Test]
        public void Equip_PutsTheItemInTheSlot_AndRaisesOneEvent()
        {
            Assert.IsTrue(equipment.Equip(EquipmentSlot.mainHand, sword, 100));

            Assert.AreSame(sword, equipment.Get(EquipmentSlot.mainHand));
            Assert.AreEqual(100u, equipment.GetInstanceId(EquipmentSlot.mainHand));
            CollectionAssert.AreEqual(new[] { "equipped 100 in " + (int)EquipmentSlot.mainHand }, log);
        }

        [Test]
        public void MainHandWeapon_BecomesTheCharactersWeapon()
        {
            Assert.IsNull(character.Loadout.Weapon, "The test target has no starting weapon.");

            equipment.Equip(EquipmentSlot.mainHand, sword, 100);

            Assert.AreSame(sword, character.Loadout.Weapon);
        }

        [Test]
        public void Unequip_ReturnsTheCharacterToItsStartingWeapon()
        {
            character.Loadout.DefaultWeapon = dagger;
            equipment.Equip(EquipmentSlot.mainHand, sword, 100);
            Assert.AreSame(sword, character.Loadout.Weapon);

            Assert.IsTrue(equipment.Unequip(EquipmentSlot.mainHand));

            Assert.AreSame(dagger, character.Loadout.Weapon);
            Assert.IsNull(equipment.Get(EquipmentSlot.mainHand));
        }

        [Test]
        public void ReplacingAnItem_RaisesUnequipThenEquip()
        {
            equipment.Equip(EquipmentSlot.mainHand, sword, 100);
            log.Clear();

            equipment.Equip(EquipmentSlot.mainHand, dagger, 101);

            string mainHand = ((int)EquipmentSlot.mainHand).ToString();
            CollectionAssert.AreEqual(new[] { "unequipped 100 from " + mainHand + " (Replaced)", "equipped 101 in " + mainHand }, log);
            Assert.AreSame(dagger, character.Loadout.Weapon);
        }

        [Test]
        public void ANonWeaponInTheMainHand_LeavesTheStartingWeaponInUse()
        {
            character.Loadout.DefaultWeapon = dagger;

            equipment.Equip(EquipmentSlot.mainHand, amulet, 100);

            Assert.AreSame(dagger, character.Loadout.Weapon);
        }

        [Test]
        public void AnItemInstance_CannotBeInTwoSlots()
        {
            equipment.Equip(EquipmentSlot.mainHand, sword, 100);
            log.Clear();

            Assert.IsFalse(equipment.Equip(EquipmentSlot.offhand, sword, 100));

            Assert.IsNull(equipment.Get(EquipmentSlot.offhand));
            Assert.IsEmpty(log);
        }

        [Test]
        public void SeveralSlots_AreHeldIndependently()
        {
            equipment.Equip(EquipmentSlot.mainHand, sword, 100);
            equipment.Equip(EquipmentSlot.neck, amulet, 101);

            Assert.AreSame(sword, equipment.Get(EquipmentSlot.mainHand));
            Assert.AreSame(amulet, equipment.Get(EquipmentSlot.neck));
        }

        [Test]
        public void TheConsumableSlot_HoldsNoEquipment()
        {
            Assert.IsFalse(equipment.Equip(EquipmentSlot.Consumable, amulet, 100));
            Assert.IsEmpty(log);
        }

        [Test]
        public void ZeroInstanceId_AndNullItem_AreRefused()
        {
            Assert.IsFalse(equipment.Equip(EquipmentSlot.mainHand, sword, 0));
            Assert.IsFalse(equipment.Equip(EquipmentSlot.mainHand, null, 100));
            Assert.IsEmpty(log);
        }

        [Test]
        public void AnItemMissingFromTheRegistry_IsRefused_BecauseClientsCouldNotFindIt()
        {
            WeaponData unregistered = CreateWeapon(5);
            LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex("DefinitionRegistry"));

            Assert.IsFalse(equipment.Equip(EquipmentSlot.mainHand, unregistered, 100));

            Assert.IsNull(equipment.Get(EquipmentSlot.mainHand));
            Object.DestroyImmediate(unregistered);
        }

        [Test]
        public void UnequippingAnEmptySlot_ReportsFalseAndRaisesNothing()
        {
            Assert.IsFalse(equipment.Unequip(EquipmentSlot.mainHand));
            Assert.IsEmpty(log);
        }

        [Test]
        public void Changed_IsRaisedForEachChange()
        {
            int changes = 0;
            equipment.Changed += () => changes++;

            equipment.Equip(EquipmentSlot.mainHand, sword, 100);
            equipment.Equip(EquipmentSlot.mainHand, dagger, 101);
            equipment.Unequip(EquipmentSlot.mainHand);

            Assert.AreEqual(3, changes);
        }

        [Test]
        public void TheEventsWearer_IsTheCharacter()
        {
            ItemEquippedEvent seen = null;
            services.Events.Subscribe<ItemEquippedEvent>(e => seen = e);

            equipment.Equip(EquipmentSlot.neck, amulet, 100);

            Assert.AreEqual(character.Id, seen.Wearer);
        }
    }
}
