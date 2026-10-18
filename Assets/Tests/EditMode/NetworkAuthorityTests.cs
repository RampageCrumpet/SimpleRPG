using System.Reflection;
using NUnit.Framework;
using SimpleRPG.Abilities;
using SimpleRPG.InventorySystem;
using UnityEngine;

namespace SimpleRPG.Tests.EditMode
{
    /// <summary>
    /// Checks the pieces of the server-authority model that don't need a running network: the server cooldown record,
    /// the item registry and the inventory's validated moves.
    /// </summary>
    public class NetworkAuthorityTests
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

        // --- Server cooldowns ---

        [Test]
        public void Cooldown_FirstActivationIsAccepted()
        {
            var tracker = new ServerCooldownTracker(0f);
            Assert.IsTrue(tracker.TryActivate("Slash", 1f, 10f));
        }

        [Test]
        public void Cooldown_ActivationInsideTheCooldownIsRejected()
        {
            var tracker = new ServerCooldownTracker(0f);
            tracker.TryActivate("Slash", 1f, 10f);

            Assert.IsFalse(tracker.TryActivate("Slash", 1f, 10.5f));
        }

        [Test]
        public void Cooldown_ActivationAfterTheCooldownIsAccepted()
        {
            var tracker = new ServerCooldownTracker(0f);
            tracker.TryActivate("Slash", 1f, 10f);

            Assert.IsTrue(tracker.TryActivate("Slash", 1f, 11f));
        }

        [Test]
        public void Cooldown_RejectedActivationDoesNotRestartTheCooldown()
        {
            var tracker = new ServerCooldownTracker(0f);
            tracker.TryActivate("Slash", 1f, 10f);
            tracker.TryActivate("Slash", 1f, 10.9f);

            Assert.IsTrue(tracker.TryActivate("Slash", 1f, 11f));
        }

        [Test]
        public void Cooldown_ToleranceAllowsAPressThatIsSlightlyEarly()
        {
            var tracker = new ServerCooldownTracker(0.1f);
            tracker.TryActivate("Slash", 1f, 10f);

            Assert.IsTrue(tracker.TryActivate("Slash", 1f, 10.95f));
        }

        [Test]
        public void Cooldown_AbilitiesAreTrackedSeparately()
        {
            var tracker = new ServerCooldownTracker(0f);
            tracker.TryActivate("Slash", 1f, 10f);

            Assert.IsTrue(tracker.TryActivate("Fireball", 1f, 10.1f));
        }

        // --- Item registry ---

        [Test]
        public void Registry_ClonesShareTheirSourceAssetsId()
        {
            Item sword = MakeItem(new Vector2Int(1, 3));
            Item gem = MakeItem(new Vector2Int(1, 1));
            ItemRegistry registry = ItemRegistry.Create(new[] { sword, gem });

            Assert.AreEqual(1, registry.GetId(sword));
            Assert.AreEqual(2, registry.GetId(gem));
            Assert.AreEqual(2, registry.GetId(gem.CloneInstance()));
            Assert.AreSame(gem, registry.GetItem(2));
        }

        [Test]
        public void Registry_UnknownItemsAndIdsAreRejected()
        {
            ItemRegistry registry = ItemRegistry.Create(new[] { MakeItem(Vector2Int.one) });

            Assert.AreEqual(0, registry.GetId(MakeItem(Vector2Int.one)));
            Assert.AreEqual(0, registry.GetId(null));
            Assert.IsNull(registry.GetItem(0));
            Assert.IsNull(registry.GetItem(99));
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
