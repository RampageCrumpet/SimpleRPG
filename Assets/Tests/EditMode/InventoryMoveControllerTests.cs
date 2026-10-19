using System.Reflection;
using NUnit.Framework;
using SimpleRPG.InventorySystem;
using UnityEngine;

namespace SimpleRPG.Tests.EditMode
{
    /// <summary>
    /// Checks that inventory moves are all-or-nothing: every refused move reports why and leaves both inventories untouched.
    /// </summary>
    public class InventoryMoveControllerTests
    {
        private GameObject host;

        [TearDown]
        public void TearDown()
        {
            if (host != null)
            {
                Object.DestroyImmediate(host);
            }
        }

        [Test]
        public void ValidMove_Succeeds_AndMovesTheItemBetweenInventories()
        {
            Inventory source = MakeInventory(new Vector2Int(4, 4));
            Inventory destination = MakeInventory(new Vector2Int(4, 4));
            Item box = MakeItem(new Vector2Int(2, 2));
            source.AddItem(box, Vector2Int.zero);

            Assert.AreEqual(MoveResult.Success, InventoryMoveController.TryMove(source, box, destination, new Vector2Int(2, 2)));

            CollectionAssert.IsEmpty(source.GetItems());
            CollectionAssert.AreEqual(new[] { (box, new Vector2Int(2, 2)) }, destination.GetItems());
            Assert.IsFalse(source.Contains(box));
            Assert.IsTrue(destination.Contains(box));
        }

        [Test]
        public void MovedItem_CanBeMovedAgainFromItsNewOwner()
        {
            Inventory a = MakeInventory(new Vector2Int(4, 4));
            Inventory b = MakeInventory(new Vector2Int(4, 4));
            Item box = MakeItem(new Vector2Int(1, 1));
            a.AddItem(box, Vector2Int.zero);

            InventoryMoveController.TryMove(a, box, b, Vector2Int.zero);

            Assert.AreEqual(MoveResult.ItemNotInSource, InventoryMoveController.Validate(a, box, a, Vector2Int.one));
            Assert.AreEqual(MoveResult.Success, InventoryMoveController.TryMove(b, box, a, new Vector2Int(3, 3)));
            CollectionAssert.AreEqual(new[] { (box, new Vector2Int(3, 3)) }, a.GetItems());
        }

        [Test]
        public void MoveOntoAnotherItem_IsRefused_AndNothingSwaps()
        {
            Inventory source = MakeInventory(new Vector2Int(4, 4));
            Inventory destination = MakeInventory(new Vector2Int(4, 4));
            Item box = MakeItem(new Vector2Int(1, 1));
            Item other = MakeItem(new Vector2Int(1, 1));
            source.AddItem(box, Vector2Int.zero);
            destination.AddItem(other, new Vector2Int(2, 2));

            Assert.AreEqual(MoveResult.InvalidPlacement, InventoryMoveController.TryMove(source, box, destination, new Vector2Int(2, 2)));

            CollectionAssert.AreEqual(new[] { (box, Vector2Int.zero) }, source.GetItems());
            CollectionAssert.AreEqual(new[] { (other, new Vector2Int(2, 2)) }, destination.GetItems());
        }

        [Test]
        public void MoveIntoAFullInventory_IsRefused_AndNothingChanges()
        {
            Inventory source = MakeInventory(new Vector2Int(2, 2));
            Inventory full = MakeInventory(new Vector2Int(2, 2));
            Item box = MakeItem(new Vector2Int(1, 1));
            source.AddItem(box, Vector2Int.zero);
            full.AddItem(MakeItem(new Vector2Int(2, 2)), Vector2Int.zero);

            for (int x = 0; x < 2; x++)
            {
                for (int y = 0; y < 2; y++)
                {
                    Assert.AreEqual(MoveResult.InvalidPlacement, InventoryMoveController.TryMove(source, box, full, new Vector2Int(x, y)));
                }
            }

            CollectionAssert.AreEqual(new[] { (box, Vector2Int.zero) }, source.GetItems());
        }

        [TestCase(-1, 0)]
        [TestCase(0, -1)]
        [TestCase(3, 3)]
        [TestCase(100, 100)]
        public void MoveOutOfBounds_IsRefused_AndChangesNothing(int x, int y)
        {
            Inventory source = MakeInventory(new Vector2Int(4, 4));
            Inventory destination = MakeInventory(new Vector2Int(4, 4));
            Item box = MakeItem(new Vector2Int(2, 2));
            source.AddItem(box, Vector2Int.zero);

            Assert.AreEqual(MoveResult.InvalidPlacement, InventoryMoveController.TryMove(source, box, destination, new Vector2Int(x, y)));

            CollectionAssert.AreEqual(new[] { (box, Vector2Int.zero) }, source.GetItems());
            CollectionAssert.IsEmpty(destination.GetItems());
        }

        [Test]
        public void RefusedMove_RaisesNoChangedEvents()
        {
            Inventory source = MakeInventory(new Vector2Int(4, 4));
            Inventory destination = MakeInventory(new Vector2Int(4, 4));
            Item box = MakeItem(new Vector2Int(2, 2));
            source.AddItem(box, Vector2Int.zero);

            int changes = 0;
            source.Changed += () => changes++;
            destination.Changed += () => changes++;

            InventoryMoveController.TryMove(source, box, destination, new Vector2Int(3, 3));

            Assert.AreEqual(0, changes);
        }

        [Test]
        public void MoveWithinTheSameInventory_MayOverlapItsOwnOldCells()
        {
            Inventory inventory = MakeInventory(new Vector2Int(4, 4));
            Item box = MakeItem(new Vector2Int(2, 2));
            inventory.AddItem(box, Vector2Int.zero);

            Assert.AreEqual(MoveResult.Success, InventoryMoveController.TryMove(inventory, box, inventory, Vector2Int.one));

            CollectionAssert.AreEqual(new[] { (box, Vector2Int.one) }, inventory.GetItems());
            Assert.AreEqual(MoveResult.Success, InventoryMoveController.TryMove(inventory, box, inventory, Vector2Int.one), "Dropping an item where it already is is harmless.");
        }

        [Test]
        public void InvalidArguments_AreReportedWithoutThrowing()
        {
            Inventory source = MakeInventory(new Vector2Int(4, 4));
            Inventory destination = MakeInventory(new Vector2Int(4, 4));
            Item box = MakeItem(Vector2Int.one);
            source.AddItem(box, Vector2Int.zero);

            Assert.AreEqual(MoveResult.NoItem, InventoryMoveController.Validate(source, null, destination, Vector2Int.zero));
            Assert.AreEqual(MoveResult.NoDestination, InventoryMoveController.Validate(source, box, null, Vector2Int.zero));
            Assert.AreEqual(MoveResult.NoDestination, InventoryMoveController.Validate(null, box, destination, Vector2Int.zero));
            Assert.AreEqual(MoveResult.ItemNotInSource, InventoryMoveController.Validate(source, MakeItem(Vector2Int.one), destination, Vector2Int.zero));
            Assert.IsFalse(source.RequestMoveItem(box, null, Vector2Int.zero));
            Assert.IsFalse(source.TryMoveItem(box, null, Vector2Int.zero));
            CollectionAssert.AreEqual(new[] { (box, Vector2Int.zero) }, source.GetItems());
        }

        [Test]
        public void Validate_DoesNotMutate()
        {
            Inventory source = MakeInventory(new Vector2Int(4, 4));
            Inventory destination = MakeInventory(new Vector2Int(4, 4));
            Item box = MakeItem(Vector2Int.one);
            source.AddItem(box, Vector2Int.zero);

            Assert.AreEqual(MoveResult.Success, InventoryMoveController.Validate(source, box, destination, Vector2Int.one));

            CollectionAssert.AreEqual(new[] { (box, Vector2Int.zero) }, source.GetItems());
            CollectionAssert.IsEmpty(destination.GetItems());
        }

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
