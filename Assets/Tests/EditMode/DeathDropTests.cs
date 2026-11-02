using NUnit.Framework;
using SimpleRPG.Delve;
using SimpleRPG.InventorySystem;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace SimpleRPG.Tests.EditMode
{
    /// <summary>
    /// Dying costs everything: whatever was carried moves, unchanged, into the loot bag.
    /// </summary>
    public class DeathDropTests
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

        [Test]
        public void EveryItemMovesToTheBag_AndTheInventoryIsLeftEmpty()
        {
            Inventory player = MakeInventory(new Vector2Int(4, 4));
            Inventory bag = MakeInventory(new Vector2Int(8, 8));
            Item sword = MakeItem(new Vector2Int(1, 3));
            Item potion = MakeItem(new Vector2Int(1, 1));
            player.AddItem(sword, Vector2Int.zero);
            player.AddItem(potion, new Vector2Int(2, 2));

            DeathDrop.Counts counts = DeathDrop.MoveInventory(player, bag);

            Assert.AreEqual(2, counts.Moved);
            Assert.AreEqual(0, counts.Left);
            CollectionAssert.IsEmpty(player.GetItems());
            Assert.IsTrue(bag.Contains(sword));
            Assert.IsTrue(bag.Contains(potion));
            Assert.IsFalse(player.Contains(sword));
        }

        [Test]
        public void TheSameItemsMove_NotCopies_AndKeepTheirInstanceIds()
        {
            Inventory player = MakeInventory(new Vector2Int(4, 4));
            Inventory bag = MakeInventory(new Vector2Int(8, 8));
            Item sword = MakeItem(new Vector2Int(1, 3));
            player.AddItem(sword, Vector2Int.zero);
            var before = player.GetInstanceId(sword);

            DeathDrop.MoveInventory(player, bag);

            Assert.AreEqual(before, bag.GetInstanceId(sword));
        }

        [Test]
        public void AnItemTheBagHasNoRoomFor_StaysWithTheOwner_AndIsCounted()
        {
            Inventory player = MakeInventory(new Vector2Int(4, 4));
            Inventory bag = MakeInventory(new Vector2Int(1, 1));
            Item small = MakeItem(new Vector2Int(1, 1));
            Item large = MakeItem(new Vector2Int(2, 2));
            player.AddItem(small, Vector2Int.zero);
            player.AddItem(large, new Vector2Int(2, 2));

            DeathDrop.Counts counts = DeathDrop.MoveInventory(player, bag);

            Assert.AreEqual(1, counts.Moved);
            Assert.AreEqual(1, counts.Left);
            Assert.IsTrue(bag.Contains(small));
            Assert.IsTrue(player.Contains(large), "Nothing is destroyed because the bag was too small.");
        }

        [Test]
        public void AnEmptyInventoryDropsNothing()
        {
            Inventory player = MakeInventory(new Vector2Int(4, 4));
            Inventory bag = MakeInventory(new Vector2Int(4, 4));

            DeathDrop.Counts counts = DeathDrop.MoveInventory(player, bag);

            Assert.AreEqual(0, counts.Moved);
            Assert.AreEqual(0, counts.Left);
            Assert.IsFalse(DeathDrop.HasAnything(player, null));
        }

        [Test]
        public void HasAnything_SeesACarriedItem()
        {
            Inventory player = MakeInventory(new Vector2Int(4, 4));
            player.AddItem(MakeItem(new Vector2Int(1, 1)), Vector2Int.zero);

            Assert.IsTrue(DeathDrop.HasAnything(player, null));
        }

        [Test]
        public void MissingPartsDoNothing()
        {
            Assert.AreEqual(0, DeathDrop.MoveInventory(null, null).Moved);
            Assert.AreEqual(0, DeathDrop.MoveEquipment(null, null).Moved);
            Assert.IsFalse(DeathDrop.HasAnything(null, null));
        }

        private Inventory MakeInventory(Vector2Int size)
        {
            var host = new GameObject("InventoryHost");
            created.Add(host);
            Inventory inventory = host.AddComponent<Inventory>();
            typeof(Inventory).GetField("<InventorySize>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(inventory, size);
            inventory.Awake();
            return inventory;
        }

        private Item MakeItem(Vector2Int size)
        {
            Item item = ScriptableObject.CreateInstance<Item>();
            created.Add(item);
            typeof(Item).GetField("<ItemSize>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(item, size);
            return item;
        }
    }
}
