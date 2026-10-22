using NUnit.Framework;
using SimpleRPG.Gameplay;
using System.Collections.Generic;
using System.Linq;

namespace SimpleRPG.Abilities.Tests.EditMode
{
    public class EquipmentSetTests
    {
        private const int MainHand = 6;
        private const int Neck = 0;

        private static readonly EntityId Wearer = new EntityId(1);
        private static readonly ItemInstanceId Sword = new ItemInstanceId(10);
        private static readonly ItemInstanceId Dagger = new ItemInstanceId(11);
        private static readonly ItemInstanceId Amulet = new ItemInstanceId(12);

        private GameplayEventDispatcher events;
        private EquipmentSet equipment;
        private List<string> log;

        [SetUp]
        public void SetUp()
        {
            events = new GameplayEventDispatcher();
            equipment = new EquipmentSet(Wearer, events);
            log = new List<string>();
            events.Subscribe<ItemEquippedEvent>(e => log.Add("equipped " + e.Item.Value + " in " + e.Slot));
            events.Subscribe<ItemUnequippedEvent>(e => log.Add("unequipped " + e.Item.Value + " from " + e.Slot + " (" + e.Reason + ")"));
        }

        [Test]
        public void Equip_FillsTheSlot_AndRaisesOneEvent()
        {
            Assert.IsTrue(equipment.Equip(MainHand, Sword));

            Assert.IsTrue(equipment.TryGet(MainHand, out ItemInstanceId item));
            Assert.AreEqual(Sword, item);
            CollectionAssert.AreEqual(new[] { "equipped 10 in 6" }, log);
        }

        [Test]
        public void Event_NamesTheWearer()
        {
            ItemEquippedEvent seen = null;
            events.Subscribe<ItemEquippedEvent>(e => seen = e);

            equipment.Equip(Neck, Amulet);

            Assert.AreEqual(Wearer, seen.Wearer);
        }

        [Test]
        public void Unequip_EmptiesTheSlot_AndRaisesOneEvent()
        {
            equipment.Equip(MainHand, Sword);
            log.Clear();

            Assert.IsTrue(equipment.Unequip(MainHand));

            Assert.IsFalse(equipment.TryGet(MainHand, out _));
            CollectionAssert.AreEqual(new[] { "unequipped 10 from 6 (Removed)" }, log);
        }

        [Test]
        public void Unequip_OfAnEmptySlot_DoesNothing()
        {
            Assert.IsFalse(equipment.Unequip(MainHand));
            Assert.IsEmpty(log);
        }

        [Test]
        public void ReplacingAnItem_RaisesUnequipBeforeEquip()
        {
            equipment.Equip(MainHand, Sword);
            log.Clear();

            Assert.IsTrue(equipment.Equip(MainHand, Dagger));

            CollectionAssert.AreEqual(new[] { "unequipped 10 from 6 (Replaced)", "equipped 11 in 6" }, log);
            Assert.IsFalse(equipment.IsEquipped(Sword));
            Assert.IsTrue(equipment.IsEquipped(Dagger));
        }

        [Test]
        public void AnItemCannotBeInTwoSlots()
        {
            equipment.Equip(MainHand, Sword);
            log.Clear();

            Assert.IsFalse(equipment.Equip(Neck, Sword));

            Assert.IsEmpty(log);
            Assert.AreEqual(1, equipment.Count);
        }

        [Test]
        public void EquippingTheSameItemAgainInTheSameSlot_IsRefusedWithoutEvents()
        {
            equipment.Equip(MainHand, Sword);
            log.Clear();

            Assert.IsFalse(equipment.Equip(MainHand, Sword));

            Assert.IsEmpty(log);
        }

        [Test]
        public void InvalidItem_IsRefused()
        {
            Assert.IsFalse(equipment.Equip(MainHand, default));
            Assert.AreEqual(0, equipment.Count);
        }

        [Test]
        public void Slots_AreIndependent()
        {
            equipment.Equip(MainHand, Sword);
            equipment.Equip(Neck, Amulet);

            CollectionAssert.AreEqual(new[] { Neck, MainHand }, equipment.Items.Select(p => p.Key).ToArray());
            equipment.Unequip(Neck);
            Assert.IsTrue(equipment.IsEquipped(Sword));
            Assert.IsFalse(equipment.IsEquipped(Amulet));
        }

        [Test]
        public void ARuleReactingToAnEquipEvent_SeesTheNewEquipment()
        {
            bool swordEquippedWhenEventArrived = false;
            events.Subscribe<ItemEquippedEvent>(e => swordEquippedWhenEventArrived = equipment.IsEquipped(Sword));

            equipment.Equip(MainHand, Sword);

            Assert.IsTrue(swordEquippedWhenEventArrived);
        }

        [Test]
        public void ChangesMadeByAHandler_WaitTheirTurn()
        {
            events.Subscribe<ItemEquippedEvent>(e =>
            {
                if (e.Item == Sword) equipment.Equip(Neck, Amulet);
            });

            equipment.Equip(MainHand, Sword);

            CollectionAssert.AreEqual(new[] { "equipped 10 in 6", "equipped 12 in 0" }, log);
        }
    }
}
