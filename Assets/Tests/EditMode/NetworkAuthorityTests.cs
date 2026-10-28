using System.Reflection;
using NUnit.Framework;
using SimpleRPG.Abilities;
using SimpleRPG.InventorySystem;
using UnityEngine;

namespace SimpleRPG.Tests.EditMode
{
    /// <summary>
    /// Checks the pieces of the server-authority model that don't need a running network: the item ids and the inventory's validated moves.
    /// </summary>
    public class NetworkAuthorityTests
    {
        private GameObject host;
        private DefinitionRegistry previousRegistry;
        private DefinitionRegistry testRegistry;

        [TearDown]
        public void TearDown()
        {
            if (host != null)
            {
                Object.DestroyImmediate(host);
            }

            if (testRegistry != null)
            {
                DefinitionRegistry.Instance = previousRegistry;
                Object.DestroyImmediate(testRegistry);
                testRegistry = null;
            }
        }

        /// <summary>Makes a registry listing exactly these items, as the game's definition registry lists its items, and uses it.</summary>
        private void UseRegistryOf(params Item[] items)
        {
            foreach (Item item in items) item.EnsureId();
            testRegistry = ScriptableObject.CreateInstance<DefinitionRegistry>();
            Assert.IsTrue(testRegistry.Rebuild(items).Success);
            previousRegistry = DefinitionRegistry.Instance;
            DefinitionRegistry.Instance = testRegistry;
        }

        // --- Item ids ---

        [Test]
        public void ItemIds_ClonesShareTheirSourceAssetsId()
        {
            Item sword = MakeItem(new Vector2Int(1, 3));
            Item gem = MakeItem(new Vector2Int(1, 1));
            UseRegistryOf(sword, gem);

            uint swordId = ItemIds.GetId(sword);
            uint gemId = ItemIds.GetId(gem);
            Assert.AreNotEqual(0u, swordId);
            Assert.AreNotEqual(0u, gemId);
            Assert.AreNotEqual(swordId, gemId);
            Assert.AreEqual(gemId, ItemIds.GetId(gem.CloneInstance()));
            Assert.AreSame(gem, ItemIds.GetItem(gemId));
            Assert.AreSame(sword, ItemIds.GetItem(swordId));
        }

        [Test]
        public void ItemIds_UnknownItemsAndIdsAreRejected()
        {
            Item known = MakeItem(Vector2Int.one);
            UseRegistryOf(known);

            Assert.AreEqual(0u, ItemIds.GetId(MakeItem(Vector2Int.one)), "an item that is not in the registry has no id");
            Assert.AreEqual(0u, ItemIds.GetId(null));
            Assert.IsNull(ItemIds.GetItem(0));
            Assert.IsNull(ItemIds.GetItem(99999));
            Assert.IsFalse(ItemIds.IsReplicable(MakeItem(Vector2Int.one)));
            Assert.IsTrue(ItemIds.IsReplicable(known));
        }

        [Test]
        public void ItemIds_IdsAreNotReusedWhenAnItemIsRemoved()
        {
            Item first = MakeItem(Vector2Int.one);
            Item second = MakeItem(Vector2Int.one);
            UseRegistryOf(first, second);
            uint firstId = ItemIds.GetId(first);

            Item replacement = MakeItem(Vector2Int.one);
            replacement.EnsureId();
            Assert.IsTrue(testRegistry.Rebuild(new[] { second, replacement }).Success, "the first item is dropped, a new one added");

            Assert.AreEqual(0u, ItemIds.GetId(first));
            Assert.IsNull(ItemIds.GetItem(firstId), "a retired id finds nothing");
            Assert.AreNotEqual(firstId, ItemIds.GetId(replacement), "a retired id is never handed to another item, so saved data cannot be misread");
        }

        // --- Inventory ---

        [Test]
        public void Inventory_AddItem_RejectsOverlapAndOutOfBounds()
        {
            Inventory inventory = MakeInventory(new Vector2Int(4, 4));
            Item box = MakeItem(new Vector2Int(2, 2));

            Assert.IsTrue(inventory.AddItem(box, new Vector2Int(0, 0)));
            Assert.IsFalse(inventory.AddItem(MakeItem(new Vector2Int(2, 2)), new Vector2Int(1, 1)), "Overlapping item should be rejected.");
            Assert.IsFalse(inventory.AddItem(MakeItem(new Vector2Int(2, 2)), new Vector2Int(3, 3)), "Item hanging off the grid should be rejected.");
        }

        [Test]
        public void Inventory_TryMoveItem_MovesWithinTheSameInventory()
        {
            Inventory inventory = MakeInventory(new Vector2Int(4, 4));
            Item box = MakeItem(new Vector2Int(2, 2));
            inventory.AddItem(box, new Vector2Int(0, 0));

            Assert.IsTrue(inventory.TryMoveItem(box, inventory, new Vector2Int(1, 1)), "An item may overlap its own old cells.");
            CollectionAssert.AreEqual(new[] { (box, new Vector2Int(1, 1)) }, inventory.GetItems());
        }

        [Test]
        public void Inventory_TryMoveItem_MovesBetweenInventories()
        {
            Inventory source = MakeInventory(new Vector2Int(4, 4));
            Inventory destination = MakeInventory(new Vector2Int(4, 4));
            Item box = MakeItem(new Vector2Int(2, 2));
            source.AddItem(box, new Vector2Int(0, 0));

            Assert.IsTrue(source.TryMoveItem(box, destination, new Vector2Int(2, 2)));
            CollectionAssert.IsEmpty(source.GetItems());
            CollectionAssert.AreEqual(new[] { (box, new Vector2Int(2, 2)) }, destination.GetItems());
        }

        [Test]
        public void Inventory_TryMoveItem_RejectedMoveChangesNothing()
        {
            Inventory source = MakeInventory(new Vector2Int(4, 4));
            Inventory destination = MakeInventory(new Vector2Int(4, 4));
            Item box = MakeItem(new Vector2Int(2, 2));
            Item blocker = MakeItem(new Vector2Int(2, 2));
            source.AddItem(box, new Vector2Int(0, 0));
            destination.AddItem(blocker, new Vector2Int(2, 2));

            Assert.IsFalse(source.TryMoveItem(box, destination, new Vector2Int(3, 3)), "Hanging off the grid.");
            Assert.IsFalse(source.TryMoveItem(box, destination, new Vector2Int(1, 1)), "Overlapping the blocker.");
            CollectionAssert.AreEqual(new[] { (box, new Vector2Int(0, 0)) }, source.GetItems());
            CollectionAssert.AreEqual(new[] { (blocker, new Vector2Int(2, 2)) }, destination.GetItems());
        }

        [Test]
        public void Inventory_TryMoveItem_RejectsItemsNotInTheSource()
        {
            Inventory source = MakeInventory(new Vector2Int(4, 4));
            Inventory destination = MakeInventory(new Vector2Int(4, 4));

            Assert.IsFalse(source.TryMoveItem(MakeItem(Vector2Int.one), destination, Vector2Int.zero));
            Assert.IsFalse(source.TryMoveItem(null, destination, Vector2Int.zero));
        }

        [Test]
        public void Inventory_RaisesChangedWhenContentsChange()
        {
            Inventory source = MakeInventory(new Vector2Int(4, 4));
            Inventory destination = MakeInventory(new Vector2Int(4, 4));
            Item box = MakeItem(new Vector2Int(2, 2));
            source.AddItem(box, Vector2Int.zero);

            int sourceChanges = 0;
            int destinationChanges = 0;
            source.Changed += () => sourceChanges++;
            destination.Changed += () => destinationChanges++;

            source.TryMoveItem(box, destination, Vector2Int.zero);

            Assert.AreEqual(1, sourceChanges);
            Assert.AreEqual(1, destinationChanges);
        }

        [Test]
        public void Inventory_AddItemInstance_StoresACloneThatRemembersItsAsset()
        {
            Inventory inventory = MakeInventory(new Vector2Int(4, 4));
            Item asset = MakeItem(new Vector2Int(1, 1));

            Assert.IsTrue(inventory.AddItemInstance(asset));

            foreach (var (item, _) in inventory.GetItems())
            {
                Assert.AreNotSame(asset, item);
                Assert.AreSame(asset, item.SourceAsset);
            }
        }

        // --- Helpers ---

        private Inventory MakeInventory(Vector2Int size)
        {
            if (host == null)
            {
                host = new GameObject("InventoryHost");
            }

            Inventory inventory = host.AddComponent<Inventory>();
            typeof(Inventory).GetField("<InventorySize>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(inventory, size);
            inventory.Awake();
            return inventory;
        }

        private static Item MakeItem(Vector2Int size)
        {
            Item item = ScriptableObject.CreateInstance<Item>();
            typeof(Item).GetField("<ItemSize>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(item, size);
            return item;
        }
    }
}
