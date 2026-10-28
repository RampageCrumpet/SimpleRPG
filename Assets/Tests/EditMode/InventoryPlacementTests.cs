using System.Reflection;
using NUnit.Framework;
using SimpleRPG.InventorySystem;
using UnityEngine;

namespace SimpleRPG.Tests.EditMode
{
    /// <summary>
    /// Checks direct placement into an offline inventory: first free slot, refusals, and that an added instance is a copy.
    /// </summary>
    public class InventoryPlacementTests
    {
        private GameObject host;
        private readonly System.Collections.Generic.List<Object> created = new System.Collections.Generic.List<Object>();

        [TearDown]
        public void TearDown()
        {
            if (host != null) Object.DestroyImmediate(host);
            foreach (Object o in created) Object.DestroyImmediate(o);
            created.Clear();
        }

        [Test]
        public void AddItem_UsesTheFirstFreeSlot_AndRefusesOnceFull()
        {
            Inventory inventory = MakeInventory(new Vector2Int(2, 1));
            Item first = MakeItem(Vector2Int.one);
            Item second = MakeItem(Vector2Int.one);
            Item third = MakeItem(Vector2Int.one);

            Assert.IsTrue(inventory.AddItem(first));
            Assert.IsTrue(inventory.AddItem(second));
            Assert.IsFalse(inventory.AddItem(third), "a full inventory refuses the item");

            CollectionAssert.AreEquivalent(new[] { (first, new Vector2Int(0, 0)), (second, new Vector2Int(1, 0)) }, inventory.GetItems());
            Assert.IsFalse(inventory.Contains(third));
        }

        [Test]
        public void AddItem_AtAnInvalidLocation_IsRefusedAndChangesNothing()
        {
            Inventory inventory = MakeInventory(new Vector2Int(3, 3));
            Item box = MakeItem(new Vector2Int(2, 2));
            Item other = MakeItem(Vector2Int.one);
            Assert.IsTrue(inventory.AddItem(box, Vector2Int.zero));

            Assert.IsFalse(inventory.AddItem(other, new Vector2Int(1, 1)), "overlaps the box");
            Assert.IsFalse(inventory.AddItem(other, new Vector2Int(3, 0)), "outside the grid");
            Assert.IsFalse(inventory.AddItem(MakeItem(new Vector2Int(2, 2)), new Vector2Int(2, 2)), "hangs over the edge");

            CollectionAssert.AreEqual(new[] { (box, Vector2Int.zero) }, inventory.GetItems());
        }

        [Test]
        public void AddItemInstance_PlacesACopy_NotTheAssetItself()
        {
            Inventory inventory = MakeInventory(new Vector2Int(2, 2));
            Item asset = MakeItem(Vector2Int.one);

            Assert.IsTrue(inventory.AddItemInstance(asset));

            Assert.IsFalse(inventory.Contains(asset));
            var items = new System.Collections.Generic.List<(Item item, Vector2Int position)>(inventory.GetItems());
            Assert.AreEqual(1, items.Count);
            Assert.AreNotSame(asset, items[0].item);
            created.Add(items[0].item);
        }

        private Inventory MakeInventory(Vector2Int size)
        {
            if (host == null) host = new GameObject("InventoryHost");
            Inventory inventory = host.AddComponent<Inventory>();
            typeof(Inventory).GetField("<InventorySize>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(inventory, size);
            inventory.Awake();
            return inventory;
        }

        private Item MakeItem(Vector2Int size)
        {
            Item item = ScriptableObject.CreateInstance<Item>();
            typeof(Item).GetField("<ItemSize>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(item, size);
            created.Add(item);
            return item;
        }
    }
}
