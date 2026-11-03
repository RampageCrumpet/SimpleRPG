using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using SimpleRPG.Audio;
using SimpleRPG.InventorySystem;
using UnityEngine;

namespace SimpleRPG.Tests.EditMode
{
    /// <summary>
    /// The inventory tells observers when the local player completes a move and knows nothing of sound; the sound side decides what the move
    /// is called. Together they keep the old behavior: carried to another container is a placement, shuffled inside one is a move.
    /// </summary>
    public class InventoryMoveSoundTests
    {
        private readonly List<(Item item, Inventory source, Inventory destination)> moves = new List<(Item, Inventory, Inventory)>();
        private GameObject host;

        [SetUp]
        public void SetUp()
        {
            moves.Clear();
            Inventory.LocalItemMoved += OnMoved;
        }

        [TearDown]
        public void TearDown()
        {
            Inventory.LocalItemMoved -= OnMoved;
            if (host != null)
            {
                Object.DestroyImmediate(host);
            }
        }

        private void OnMoved(Item item, Inventory source, Inventory destination) => moves.Add((item, source, destination));

        [Test]
        public void MoveToAnotherInventory_RaisesTheEvent_AndSoundsAsAPlacement()
        {
            Inventory source = MakeInventory();
            Inventory destination = MakeInventory();
            Item box = MakeItem();
            source.AddItem(box, Vector2Int.zero);

            Assert.IsTrue(source.RequestMoveItem(box, destination, Vector2Int.zero));

            Assert.AreEqual(1, moves.Count);
            Assert.AreSame(box, moves[0].item);
            Assert.AreSame(source, moves[0].source);
            Assert.AreSame(destination, moves[0].destination);
            Assert.AreEqual(InventoryAudioAction.Place, InventoryMoveSounds.ActionFor(moves[0].source, moves[0].destination));
        }

        [Test]
        public void MoveWithinOneInventory_RaisesTheEvent_AndSoundsAsAMove()
        {
            Inventory inventory = MakeInventory();
            Item box = MakeItem();
            inventory.AddItem(box, Vector2Int.zero);

            Assert.IsTrue(inventory.RequestMoveItem(box, inventory, new Vector2Int(2, 2)));

            Assert.AreEqual(1, moves.Count);
            Assert.AreSame(inventory, moves[0].source);
            Assert.AreSame(inventory, moves[0].destination);
            Assert.AreEqual(InventoryAudioAction.Move, InventoryMoveSounds.ActionFor(moves[0].source, moves[0].destination));
        }

        [Test]
        public void RefusedMove_RaisesNothing()
        {
            Inventory source = MakeInventory();
            Item box = MakeItem();
            source.AddItem(box, Vector2Int.zero);

            Assert.IsFalse(source.RequestMoveItem(box, null, Vector2Int.zero));

            CollectionAssert.IsEmpty(moves);
        }

        private Inventory MakeInventory()
        {
            if (host == null)
            {
                host = new GameObject("InventoryHost");
            }

            Inventory inventory = host.AddComponent<Inventory>();
            typeof(Inventory).GetField("<InventorySize>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(inventory, new Vector2Int(4, 4));
            inventory.Awake();
            return inventory;
        }

        private static Item MakeItem()
        {
            Item item = ScriptableObject.CreateInstance<Item>();
            typeof(Item).GetField("<ItemSize>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(item, Vector2Int.one);
            return item;
        }
    }
}
