using NUnit.Framework;
using SimpleRPG.InventorySystem;
using SimpleRPG.ObjectInteractions;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace SimpleRPG.Tests.EditMode
{
    /// <summary>
    /// Dropping an item on the ground: who may, what is refused and why, and that the item that lands is the very instance that left.
    /// </summary>
    public class ItemDropTests
    {
        private readonly List<Object> created = new List<Object>();

        [TearDown]
        public void TearDown()
        {
            foreach (Object item in created)
            {
                Object.DestroyImmediate(item);
            }

            created.Clear();
        }

        private Inventory MakeInventory(bool carriedByACharacter = true)
        {
            var host = new GameObject("Carrier");
            created.Add(host);
            Inventory inventory = host.AddComponent<Inventory>();
            if (carriedByACharacter)
            {
                host.AddComponent<Character>();
            }

            typeof(Inventory).GetField("<InventorySize>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(inventory, new Vector2Int(6, 4));
            inventory.Awake();
            return inventory;
        }

        // An item whose world form is a plain object with a pickup on it, standing in for the prefab.
        private Item MakeItem(bool droppable = true)
        {
            Item item = ScriptableObject.CreateInstance<Item>();
            created.Add(item);
            typeof(Item).GetField("<ItemSize>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(item, Vector2Int.one);
            if (droppable)
            {
                var world = new GameObject("WorldForm");
                created.Add(world);
                world.AddComponent<WorldItemPickup>();
                item.WorldPrefab = world;
            }

            return item;
        }

        [Test]
        public void ADroppableItemInACharactersInventory_IsAccepted()
        {
            Inventory inventory = MakeInventory();
            Item sword = MakeItem();
            inventory.AddItem(sword, Vector2Int.zero);

            Assert.AreEqual(DropResult.Success, ItemDropController.Validate(inventory, sword, requester: 0));
        }

        [Test]
        public void AnItemNotInTheInventory_IsRefused()
        {
            Inventory inventory = MakeInventory();

            Assert.AreEqual(DropResult.ItemNotInInventory, ItemDropController.Validate(inventory, MakeItem(), 0));
            Assert.AreEqual(DropResult.ItemNotInInventory, ItemDropController.Validate(inventory, null, 0));
            Assert.AreEqual(DropResult.ItemNotInInventory, ItemDropController.Validate(null, MakeItem(), 0));
        }

        [Test]
        public void AnInventoryWithNoCharacter_IsNotSomewhereToDropFrom()
        {
            Inventory chest = MakeInventory(carriedByACharacter: false);
            Item item = MakeItem();
            chest.AddItem(item, Vector2Int.zero);

            Assert.AreEqual(DropResult.NotYourInventory, ItemDropController.Validate(chest, item, 0));
        }

        [Test]
        public void ANobodyElsesInventory_IsRefused()
        {
            Inventory inventory = MakeInventory();
            Item item = MakeItem();
            inventory.AddItem(item, Vector2Int.zero);

            // The inventory belongs to client 0; client 5 is asking.
            Assert.AreEqual(DropResult.NotYourInventory, ItemDropController.Validate(inventory, item, requester: 5));
        }

        [Test]
        public void AnItemWithNoWorldForm_CannotBeDropped_AndStaysWhereItIs()
        {
            Inventory inventory = MakeInventory();
            Item item = MakeItem(droppable: false);
            inventory.AddItem(item, Vector2Int.zero);

            Assert.AreEqual(DropResult.CannotBeDropped, ItemDropController.TryDrop(inventory, item, 0, out IDroppedItemForm dropped));

            Assert.IsNull(dropped);
            Assert.IsTrue(inventory.Contains(item), "A refused drop changes nothing.");
        }

        [Test]
        public void ADrop_TakesTheItemOutOfTheInventory_AndPutsTheSameInstanceOnTheGround()
        {
            Inventory inventory = MakeInventory();
            Item asset = MakeItem();
            ItemAffixes rolled = ItemAffixes.From(new[] { new AffixRoll(7, 2500), new AffixRoll(9, 400) });
            Item instance = asset.CloneInstance(rolled);
            created.Add(instance);
            inventory.AddItem(instance, new Vector2Int(2, 1));

            DropResult result = ItemDropController.TryDrop(inventory, instance, 0, out IDroppedItemForm form);
            WorldItemPickup dropped = form as WorldItemPickup;
            if (dropped != null) created.Add(dropped.gameObject);

            Assert.AreEqual(DropResult.Success, result);
            Assert.IsFalse(inventory.Contains(instance), "It left the inventory.");
            CollectionAssert.IsEmpty(inventory.GetItems());
            Assert.IsNotNull(dropped);
            Assert.AreEqual(rolled, dropped.Affixes, "The pickup shows what the dropped item rolled.");
        }

        [Test]
        public void ADroppedItemLandsInFrontOfTheCharacter()
        {
            Inventory inventory = MakeInventory();
            inventory.transform.position = new Vector3(10f, 0f, 10f);
            inventory.transform.rotation = Quaternion.identity;
            Item item = MakeItem();
            inventory.AddItem(item, Vector2Int.zero);

            ItemDropController.TryDrop(inventory, item, 0, out IDroppedItemForm form);
            WorldItemPickup dropped = form as WorldItemPickup;
            if (dropped != null) created.Add(dropped.gameObject);

            Assert.AreEqual(10f, dropped.transform.position.x, 0.01f);
            Assert.AreEqual(11f, dropped.transform.position.z, 0.01f, "One metre ahead of a character facing forward.");
        }
    }
}
