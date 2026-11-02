using NUnit.Framework;
using SimpleRPG.Delve;
using SimpleRPG.InventorySystem;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace SimpleRPG.Tests.EditMode
{
    /// <summary>
    /// A player who drops out gets their character back exactly as it was: the same items, in the same places, under the same instance IDs.
    /// If they never return, the same items go into a loot bag.
    /// </summary>
    public class ParkedCharacterTests
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

        private static ParkedCharacter Away(bool wasDead = false) =>
            new ParkedCharacter("player-0001", 3, new Vector3(1f, 2f, 3f), Quaternion.identity, 40, wasDead, 12.5d);

        [Test]
        public void CarriedItemsComeBack_WhereTheyWere_UnderTheSameInstanceIds()
        {
            Inventory before = MakeInventory(new Vector2Int(6, 4));
            Item sword = MakeItem(new Vector2Int(1, 3));
            Item potion = MakeItem(new Vector2Int(1, 1));
            before.AddItem(sword, new Vector2Int(0, 0));
            before.AddItem(potion, new Vector2Int(4, 2));
            var swordId = before.GetInstanceId(sword);
            var potionId = before.GetInstanceId(potion);

            ParkedCharacter parked = Away();
            parked.CaptureCarried(before);
            before.RemoveItem(sword);
            before.RemoveItem(potion);

            Inventory after = MakeInventory(new Vector2Int(6, 4));
            int failed = parked.RestoreCarried(after);

            Assert.AreEqual(0, failed);
            CollectionAssert.AreEquivalent(new[] { (sword, new Vector2Int(0, 0)), (potion, new Vector2Int(4, 2)) }, after.GetItems());
            Assert.AreEqual(swordId, after.GetInstanceId(sword));
            Assert.AreEqual(potionId, after.GetInstanceId(potion));
        }

        [Test]
        public void CapturingChangesNothing()
        {
            Inventory inventory = MakeInventory(new Vector2Int(4, 4));
            Item box = MakeItem(new Vector2Int(2, 2));
            inventory.AddItem(box, new Vector2Int(1, 1));

            Away().CaptureCarried(inventory);

            CollectionAssert.AreEqual(new[] { (box, new Vector2Int(1, 1)) }, inventory.GetItems());
        }

        [Test]
        public void AnItemThatCannotBePlacedBack_IsCounted()
        {
            Inventory before = MakeInventory(new Vector2Int(4, 4));
            Item box = MakeItem(new Vector2Int(2, 2));
            before.AddItem(box, new Vector2Int(2, 2));
            ParkedCharacter parked = Away();
            parked.CaptureCarried(before);

            Inventory tooSmall = MakeInventory(new Vector2Int(1, 1));

            Assert.AreEqual(1, parked.RestoreCarried(tooSmall));
        }

        [Test]
        public void IfThePlayerNeverReturns_EverythingGoesIntoTheBag_AsTheSameItems()
        {
            Inventory before = MakeInventory(new Vector2Int(6, 4));
            Item sword = MakeItem(new Vector2Int(1, 3));
            Item potion = MakeItem(new Vector2Int(1, 1));
            before.AddItem(sword, Vector2Int.zero);
            before.AddItem(potion, new Vector2Int(3, 3));
            var swordId = before.GetInstanceId(sword);
            ParkedCharacter parked = Away();
            parked.CaptureCarried(before);

            Inventory bag = MakeInventory(new Vector2Int(8, 8));
            int left = parked.MoveContentsTo(bag);

            Assert.AreEqual(0, left);
            Assert.IsTrue(bag.Contains(sword));
            Assert.IsTrue(bag.Contains(potion));
            Assert.AreEqual(swordId, bag.GetInstanceId(sword));
        }

        [Test]
        public void ABagTooSmall_ReportsWhatDidNotFit()
        {
            Inventory before = MakeInventory(new Vector2Int(4, 4));
            before.AddItem(MakeItem(new Vector2Int(1, 1)), Vector2Int.zero);
            before.AddItem(MakeItem(new Vector2Int(2, 2)), new Vector2Int(2, 2));
            ParkedCharacter parked = Away();
            parked.CaptureCarried(before);

            Assert.AreEqual(1, parked.MoveContentsTo(MakeInventory(new Vector2Int(1, 1))));
        }

        [Test]
        public void AnEmptyHandedCharacterIsEmpty_AndRestoresNothing()
        {
            ParkedCharacter parked = Away();

            Assert.IsTrue(parked.IsEmpty);
            Assert.AreEqual(0, parked.RestoreCarried(MakeInventory(new Vector2Int(2, 2))));
            Assert.AreEqual(0, parked.MoveContentsTo(MakeInventory(new Vector2Int(2, 2))));
        }

        [Test]
        public void TheRecordRemembersWhereAndHowHurtTheCharacterWas()
        {
            ParkedCharacter parked = Away(wasDead: true);

            Assert.AreEqual("player-0001", parked.PlayerId);
            Assert.AreEqual(3UL, parked.LastClientId);
            Assert.AreEqual(new Vector3(1f, 2f, 3f), parked.Position);
            Assert.AreEqual(40, parked.Health);
            Assert.IsTrue(parked.WasDead);
            Assert.AreEqual(12.5d, parked.ParkedAt);
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
