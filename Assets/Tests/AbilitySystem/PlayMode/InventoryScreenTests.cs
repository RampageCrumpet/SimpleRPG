using NUnit.Framework;
using SimpleRPG.UI;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace SimpleRPG.Abilities.Tests.PlayMode
{
    /// <summary>
    /// Checks the pieces of the HUD and inventory screens that do not need a running host: the buff bar shows only what is active, and the
    /// paper dolls and inventories share one cell size.
    /// </summary>
    public class InventoryScreenTests
    {
        private readonly List<GameObject> created = new List<GameObject>();

        [TearDown]
        public void TearDown()
        {
            foreach (GameObject go in created)
            {
                if (go != null) Object.DestroyImmediate(go);
            }

            created.Clear();
        }

        private BuffBarUI CreateBuffBar(out GameObject strip, out RectTransform slots)
        {
            GameObject root = new GameObject("BuffBar", typeof(RectTransform));
            created.Add(root);
            BuffBarUI bar = root.AddComponent<BuffBarUI>();

            strip = new GameObject("Strip", typeof(RectTransform));
            strip.transform.SetParent(root.transform, false);
            GameObject container = new GameObject("Slots", typeof(RectTransform));
            container.transform.SetParent(root.transform, false);
            slots = (RectTransform)container.transform;

            typeof(BuffBarUI).GetField("strip", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(bar, strip);
            typeof(BuffBarUI).GetField("slotContainer", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(bar, slots);
            return bar;
        }

        private static List<BuffEntry> Entries(int count)
        {
            List<BuffEntry> entries = new List<BuffEntry>();
            for (int i = 0; i < count; i++)
            {
                entries.Add(new BuffEntry("Effect" + i, null, 1));
            }

            return entries;
        }

        private static int ActiveChildren(Transform parent)
        {
            int active = 0;
            foreach (Transform child in parent)
            {
                if (child.gameObject.activeSelf) active++;
            }

            return active;
        }

        [Test]
        public void BuffBar_WithNoEffects_ShowsNothing()
        {
            BuffBarUI bar = CreateBuffBar(out GameObject strip, out RectTransform slots);

            bar.Show(Entries(0));

            Assert.AreEqual(0, bar.VisibleCount);
            Assert.IsFalse(strip.activeSelf, "No empty placeholder strip.");
            Assert.AreEqual(0, ActiveChildren(slots));
        }

        [Test]
        public void BuffBar_ShowsOneSlotPerActiveEffect_AndHidesThemWhenTheyEnd()
        {
            BuffBarUI bar = CreateBuffBar(out GameObject strip, out RectTransform slots);

            bar.Show(Entries(3));
            Assert.AreEqual(3, bar.VisibleCount);
            Assert.IsTrue(strip.activeSelf);
            Assert.AreEqual(3, ActiveChildren(slots));

            bar.Show(Entries(1));
            Assert.AreEqual(1, bar.VisibleCount);
            Assert.AreEqual(1, ActiveChildren(slots));

            bar.Show(Entries(0));
            Assert.AreEqual(0, ActiveChildren(slots));
            Assert.IsFalse(strip.activeSelf);
        }

        [Test]
        public void BuffBar_ShowsAtMostEightSlots()
        {
            BuffBarUI bar = CreateBuffBar(out _, out RectTransform slots);

            bar.Show(Entries(12));

            Assert.AreEqual(BuffBarUI.MaxSlots, bar.VisibleCount);
            Assert.AreEqual(BuffBarUI.MaxSlots, ActiveChildren(slots));
        }

        [Test]
        public void PaperDollSlots_AreTheSameSizeAsInventoryCells()
        {
            Assert.AreEqual(InventoryUI.ReferenceCellSize, PaperDollUI.SlotSize);
        }

        [Test]
        public void CompactPaperDoll_KeepsEverySlotInsideAWideShortRegion()
        {
            // The interacted object's doll region is 852x260 reference units, centred on its middle.
            foreach (InventorySystem.EquipmentSlot slot in InventorySystem.ArmorData.ArmorSlots)
            {
                Vector2 offset = PaperDollUI.SlotOffset(PaperDollLayout.Compact, slot);
                Assert.LessOrEqual(Mathf.Abs(offset.x) + PaperDollUI.SlotSize / 2f, 426f, slot + " within the width.");
                Assert.LessOrEqual(Mathf.Abs(offset.y) + PaperDollUI.SlotSize / 2f, 130f, slot + " within the height.");
            }
        }

        [Test]
        public void FullPaperDoll_KeepsEverySlotInsideTheDollRegion()
        {
            // The player's doll region is 500 units tall and at least 852 wide, centred on its middle.
            foreach (InventorySystem.EquipmentSlot slot in InventorySystem.ArmorData.ArmorSlots)
            {
                Vector2 offset = PaperDollUI.SlotOffset(PaperDollLayout.Full, slot);
                Assert.LessOrEqual(Mathf.Abs(offset.x) + PaperDollUI.SlotSize / 2f, 426f, slot + " within the width.");
                Assert.LessOrEqual(Mathf.Abs(offset.y) + PaperDollUI.SlotSize / 2f, 250f, slot + " within the height.");
            }
        }
    }
}
