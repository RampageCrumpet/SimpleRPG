using NUnit.Framework;
using SimpleRPG.Gameplay;
using SimpleRPG.InventorySystem;
using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace SimpleRPG.Abilities.Tests.EditMode
{
    public class ItemStateStoreTests
    {
        private static readonly DefinitionId Charges = DefinitionId.NewId();
        private static readonly DefinitionId Fuel = DefinitionId.NewId();
        private static readonly ItemInstanceId StaffA = new ItemInstanceId(1);
        private static readonly ItemInstanceId StaffB = new ItemInstanceId(2);

        private ItemStateStore store;

        [SetUp]
        public void SetUp()
        {
            store = new ItemStateStore();
        }

        [Test]
        public void EnsureCounter_CreatesItWithItsStartingValue()
        {
            Assert.IsTrue(store.EnsureCounter(StaffA, Charges, 3));

            Assert.IsTrue(store.TryGetCounter(StaffA, Charges, out int value));
            Assert.AreEqual(3, value);
        }

        [Test]
        public void EnsureCounter_NeverResetsAnExistingCounter()
        {
            store.EnsureCounter(StaffA, Charges, 3);
            store.TrySpend(StaffA, Charges, 2);

            Assert.IsFalse(store.EnsureCounter(StaffA, Charges, 3));

            store.TryGetCounter(StaffA, Charges, out int value);
            Assert.AreEqual(1, value);
        }

        [Test]
        public void TwoInstancesOfOneDefinition_HaveIndependentState()
        {
            store.EnsureCounter(StaffA, Charges, 3);
            store.EnsureCounter(StaffB, Charges, 3);

            Assert.IsTrue(store.TrySpend(StaffA, Charges, 2));

            store.TryGetCounter(StaffA, Charges, out int a);
            store.TryGetCounter(StaffB, Charges, out int b);
            Assert.AreEqual(1, a);
            Assert.AreEqual(3, b);
        }

        [Test]
        public void CountersOnOneInstance_AreIndependent()
        {
            store.EnsureCounter(StaffA, Charges, 3);
            store.EnsureCounter(StaffA, Fuel, 10);

            store.TrySpend(StaffA, Charges, 1);

            store.TryGetCounter(StaffA, Fuel, out int fuel);
            Assert.AreEqual(10, fuel);
        }

        [Test]
        public void Spend_IsAllOrNothing()
        {
            store.EnsureCounter(StaffA, Charges, 2);

            Assert.IsFalse(store.TrySpend(StaffA, Charges, 3));

            store.TryGetCounter(StaffA, Charges, out int value);
            Assert.AreEqual(2, value, "A refused spend must take nothing.");
        }

        [Test]
        public void SpendingTheLastCharge_ReachesZero_ThenFurtherSpendIsRefused()
        {
            store.EnsureCounter(StaffA, Charges, 1);

            Assert.IsTrue(store.TrySpend(StaffA, Charges, 1));
            Assert.IsFalse(store.TrySpend(StaffA, Charges, 1));

            store.TryGetCounter(StaffA, Charges, out int value);
            Assert.AreEqual(0, value);
        }

        [Test]
        public void SpendingFromAnUnknownInstanceOrCounter_IsRefused()
        {
            store.EnsureCounter(StaffA, Charges, 3);

            Assert.IsFalse(store.TrySpend(StaffB, Charges, 1));
            Assert.IsFalse(store.TrySpend(StaffA, Fuel, 1));
            Assert.IsFalse(store.TryGetCounter(StaffB, Charges, out _));
        }

        [Test]
        public void Remove_ForgetsAnInstancesState()
        {
            store.EnsureCounter(StaffA, Charges, 3);
            store.EnsureCounter(StaffB, Charges, 3);

            Assert.IsTrue(store.Remove(StaffA));
            Assert.IsFalse(store.Remove(StaffA));

            Assert.IsFalse(store.TryGetCounter(StaffA, Charges, out _));
            Assert.IsTrue(store.TryGetCounter(StaffB, Charges, out _));
            Assert.AreEqual(1, store.InstanceCount);
        }

        [Test]
        public void Changed_ReportsEachCreateAndSpend_ButNotARefusedSpend()
        {
            List<string> seen = new List<string>();
            store.Changed += (item, counter, value) => seen.Add(item.Value + "=" + value);

            store.EnsureCounter(StaffA, Charges, 2);
            store.EnsureCounter(StaffA, Charges, 2);
            store.TrySpend(StaffA, Charges, 1);
            store.TrySpend(StaffA, Charges, 5);

            CollectionAssert.AreEqual(new[] { "1=2", "1=1" }, seen);
        }

        [Test]
        public void InvalidArguments_AreRejected()
        {
            Assert.Throws<ArgumentException>(() => store.EnsureCounter(default, Charges, 1));
            Assert.Throws<ArgumentException>(() => store.EnsureCounter(StaffA, DefinitionId.None, 1));
            Assert.Throws<ArgumentOutOfRangeException>(() => store.EnsureCounter(StaffA, Charges, -1));
            store.EnsureCounter(StaffA, Charges, 1);
            Assert.Throws<ArgumentOutOfRangeException>(() => store.TrySpend(StaffA, Charges, 0));
        }
    }

    /// <summary>
    /// Checks item instance identity and state against real inventories and the equipment-facing accessors: an instance keeps its ID, and so its
    /// state, as it moves between inventories, and two items made from one definition are different instances.
    /// </summary>
    public class ItemInstanceIdentityTests
    {
        private static readonly DefinitionId Charges = DefinitionId.NewId();

        private GameObject host;

        [TearDown]
        public void TearDown()
        {
            if (host != null) UnityEngine.Object.DestroyImmediate(host);
        }

        private Inventory MakeInventory(Vector2Int size)
        {
            if (host == null) host = new GameObject("InventoryHost");

            Inventory inventory = host.AddComponent<Inventory>();
            typeof(Inventory).GetField("<InventorySize>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(inventory, size);
            inventory.Awake();
            return inventory;
        }

        private static Item MakeItem()
        {
            Item item = ScriptableObject.CreateInstance<Item>();
            typeof(Item).GetField("<ItemSize>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(item, Vector2Int.one);
            return item;
        }

        [Test]
        public void AnItemInAnInventory_HasAValidInstanceId()
        {
            Inventory inventory = MakeInventory(new Vector2Int(4, 4));
            Item staff = MakeItem();
            inventory.AddItem(staff, Vector2Int.zero);

            Assert.IsTrue(inventory.GetInstanceId(staff).IsValid);
        }

        [Test]
        public void AnItemNotInTheInventory_HasNoInstanceId()
        {
            Inventory inventory = MakeInventory(new Vector2Int(4, 4));

            Assert.IsFalse(inventory.GetInstanceId(MakeItem()).IsValid);
            Assert.IsFalse(inventory.GetInstanceId(null).IsValid);
        }

        [Test]
        public void TwoItemsMadeFromOneAsset_AreDifferentInstances()
        {
            Inventory inventory = MakeInventory(new Vector2Int(4, 4));
            Item asset = MakeItem();
            inventory.AddItemInstance(asset);
            inventory.AddItemInstance(asset);

            List<ItemInstanceId> ids = new List<ItemInstanceId>();
            foreach ((Item item, Vector2Int position) in inventory.GetItems())
            {
                ids.Add(inventory.GetInstanceId(item));
            }

            Assert.AreEqual(2, ids.Count);
            Assert.AreNotEqual(ids[0], ids[1]);
        }

        [Test]
        public void MovingAnItem_KeepsItsInstanceId_AndSoItsState()
        {
            ItemStateStore states = new ItemStateStore();
            Inventory source = MakeInventory(new Vector2Int(4, 4));
            Inventory destination = MakeInventory(new Vector2Int(4, 4));
            Item staff = MakeItem();
            source.AddItem(staff, Vector2Int.zero);
            ItemInstanceId before = source.GetInstanceId(staff);
            states.EnsureCounter(before, Charges, 3);
            states.TrySpend(before, Charges, 1);

            Assert.IsTrue(source.TryMoveItem(staff, destination, new Vector2Int(2, 2)));

            ItemInstanceId after = destination.GetInstanceId(staff);
            Assert.AreEqual(before, after);
            Assert.IsFalse(source.GetInstanceId(staff).IsValid);
            states.TryGetCounter(after, Charges, out int charges);
            Assert.AreEqual(2, charges, "The state belongs to the instance, not to the inventory that holds it.");
        }

        [Test]
        public void StateOfTwoInstances_StaysSeparateAcrossMoves()
        {
            ItemStateStore states = new ItemStateStore();
            Inventory a = MakeInventory(new Vector2Int(4, 4));
            Inventory b = MakeInventory(new Vector2Int(4, 4));
            Item asset = MakeItem();
            a.AddItemInstance(asset);
            a.AddItemInstance(asset);

            List<Item> items = new List<Item>();
            foreach ((Item item, Vector2Int position) in a.GetItems()) items.Add(item);
            states.EnsureCounter(a.GetInstanceId(items[0]), Charges, 3);
            states.EnsureCounter(a.GetInstanceId(items[1]), Charges, 3);

            ItemInstanceId first = a.GetInstanceId(items[0]);
            ItemInstanceId second = a.GetInstanceId(items[1]);
            states.TrySpend(first, Charges, 3);
            a.TryMoveItem(items[0], b, Vector2Int.zero);

            states.TryGetCounter(b.GetInstanceId(items[0]), Charges, out int movedCharges);
            states.TryGetCounter(second, Charges, out int otherCharges);
            Assert.AreEqual(0, movedCharges);
            Assert.AreEqual(3, otherCharges);
        }

        [Test]
        public void GameplayServices_ProvidesTheItemStateStore()
        {
            GameplayServices services = GameplayServices.Reset();

            Assert.IsNotNull(services.ItemStates);
            Assert.AreSame(services.ItemStates, GameplayServices.Current.ItemStates);
            GameplayServices.Reset();
        }
    }
}
