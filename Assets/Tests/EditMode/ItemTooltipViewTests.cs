using NUnit.Framework;
using SimpleRPG.InventorySystem;
using SimpleRPG.UI;
using System.Collections.Generic;
using UnityEngine;

namespace SimpleRPG.Tests.EditMode
{
    /// <summary>
    /// The tooltip panel: it shows an item's lines coloured by kind, hides again, and is built under the canvas that asked for it.
    /// </summary>
    public class ItemTooltipViewTests
    {
        private readonly List<Object> created = new List<Object>();

        [TearDown]
        public void TearDown()
        {
            ItemTooltipView.Hide();
            foreach (Object o in created)
            {
                if (o != null) Object.DestroyImmediate(o);
            }

            created.Clear();
        }

        private Item Named(string name)
        {
            Item item = ScriptableObject.CreateInstance<ArmorData>();
            created.Add(item);
            typeof(Item).GetField("<ItemName>k__BackingField", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).SetValue(item, name);
            return item;
        }

        [Test]
        public void TheLinesAreColouredByKind()
        {
            var lines = new List<TooltipLine>
            {
                new TooltipLine(TooltipLineKind.Name, "Riveted Plate"),
                new TooltipLine(TooltipLineKind.Property, "+45 Armor Rating"),
                new TooltipLine(TooltipLineKind.Affix, "+3 Armor Rating"),
                new TooltipLine(TooltipLineKind.Reaction, "20% chance to cause bleeding on hit"),
            };

            string text = ItemTooltipView.Compose(lines);

            StringAssert.Contains("<b>Riveted Plate</b>", text);
            StringAssert.Contains("<color=#B8B8B8>+45 Armor Rating</color>", text);
            StringAssert.Contains("<color=#7DA8FF>+3 Armor Rating</color>", text);
            StringAssert.Contains("<color=#FFB060>20% chance to cause bleeding on hit</color>", text);
            Assert.AreEqual(3, text.Split('\n').Length - 1, "One line each.");
        }

        [Test]
        public void ShowingAnItem_PutsItsNameOnThePanel_AndHidingClearsIt()
        {
            var canvasObject = new GameObject("Canvas", typeof(Canvas));
            created.Add(canvasObject);
            var anchor = new GameObject("Icon", typeof(RectTransform));
            anchor.transform.SetParent(canvasObject.transform, false);
            Item item = Named("Test Helm");

            ItemTooltipView.Show(item, anchor.transform);
            StringAssert.Contains(item.DisplayName, ItemTooltipView.CurrentText);

            ItemTooltipView.Hide();
            Assert.IsEmpty(ItemTooltipView.CurrentText);
        }

        [Test]
        public void NoItem_OrNoCanvas_ShowsNothing()
        {
            var loose = new GameObject("Loose", typeof(RectTransform));
            created.Add(loose);

            ItemTooltipView.Show(null, loose.transform);
            ItemTooltipView.Show(Named("Loose Helm"), loose.transform);

            Assert.IsEmpty(ItemTooltipView.CurrentText);
        }
    }
}
