using NUnit.Framework;
using SimpleRPG.AppearanceTools;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace SimpleRPG.Tests.EditMode
{
    /// <summary>
    /// Folding worn armor into something that lies on a floor: sleeves drawn in, laid flat, brought to size, set on its base.
    /// </summary>
    public class GarmentFoldTests
    {
        // A body-shaped shell with arms held out: a torso column and a sleeve out to each side at shoulder height.
        private static Vector3[] Shirt() => new[]
        {
            new Vector3(0f, 0f, 0f), new Vector3(0f, 0.7f, 0f), new Vector3(0.2f, 0.7f, 0.1f), new Vector3(-0.2f, 0.7f, -0.1f),
            new Vector3(0.7f, 0.6f, 0f), new Vector3(-0.7f, 0.6f, 0f),
        };

        private static Bounds BoundsOf(IEnumerable<Vector3[]> pieces)
        {
            Vector3[] all = pieces.SelectMany(p => p).ToArray();
            var bounds = new Bounds(all[0], Vector3.zero);
            foreach (Vector3 v in all) bounds.Encapsulate(v);
            return bounds;
        }

        [Test]
        public void AFoldedGarment_LiesFlat_NotStandingInTheAirWithItsArmsOut()
        {
            var pieces = new List<Vector3[]> { Shirt() };
            Bounds before = BoundsOf(pieces);

            GarmentFold.Fold(pieces, GarmentFold.Settings.Default);
            Bounds after = BoundsOf(pieces);

            Assert.Less(after.size.y, 0.25f, "It lies on the floor: its height is the garment's thickness, not a person's.");
            Assert.Less(after.size.x, before.size.x, "The sleeves are drawn in.");
            Assert.AreEqual(0f, after.min.y, 1e-5f, "It rests on its lowest point.");
            Assert.AreEqual(0f, after.center.x, 1e-5f);
            Assert.AreEqual(0f, after.center.z, 1e-5f);
        }

        [Test]
        public void ALargeGarment_IsBroughtToFitAFloorPickup_ButASmallOneIsNotEnlarged()
        {
            var large = new List<Vector3[]> { new[] { new Vector3(0f, 0f, 0f), new Vector3(0f, 1.6f, 0f) } };
            var small = new List<Vector3[]> { new[] { new Vector3(0f, 0f, 0f), new Vector3(0f, 0.1f, 0f) } };

            GarmentFold.Fold(large, GarmentFold.Settings.Default);
            GarmentFold.Fold(small, GarmentFold.Settings.Default);

            Assert.AreEqual(GarmentFold.Settings.Default.LargestSide, Mathf.Max(BoundsOf(large).size.x, BoundsOf(large).size.z), 1e-4f);
            Assert.AreEqual(0.1f, Mathf.Max(BoundsOf(small).size.x, BoundsOf(small).size.z), 1e-4f);
        }

        [Test]
        public void APairOfPieces_ShareOneFold_SoTheyStayAPair()
        {
            var left = new[] { new Vector3(-0.7f, 0.6f, 0f), new Vector3(-0.75f, 0.6f, 0f) };
            var right = new[] { new Vector3(0.7f, 0.6f, 0f), new Vector3(0.75f, 0.6f, 0f) };
            var pair = new List<Vector3[]> { left, right };

            GarmentFold.Fold(pair, GarmentFold.Settings.Default);

            Assert.Less(left[0].x, right[0].x, "Left stays left of right.");
            Assert.AreEqual(-right[0].x, left[0].x, 1e-5f, "Centred over the same middle.");
            Assert.AreEqual(left[0].y, right[0].y, 1e-6f, "On the same floor.");
        }

        [Test]
        public void AHelmet_StandsOnItsBase_AndIsNotLaidDown()
        {
            var helmet = new List<Vector3[]> { new[] { new Vector3(0.1f, 0f, 0.1f), new Vector3(-0.1f, 0.25f, -0.1f) } };

            GarmentFold.Stand(helmet, GarmentFold.Settings.Default);
            Bounds bounds = BoundsOf(helmet);

            Assert.AreEqual(0.25f, bounds.size.y, 1e-5f, "Its height is kept.");
            Assert.AreEqual(0f, bounds.min.y, 1e-5f);
            Assert.AreEqual(0f, bounds.center.x, 1e-5f);
        }

        [Test]
        public void TheSameGarment_FoldsTheSameWayEveryTime()
        {
            var a = new List<Vector3[]> { Shirt() };
            var b = new List<Vector3[]> { Shirt() };

            GarmentFold.Fold(a, GarmentFold.Settings.Default);
            GarmentFold.Fold(b, GarmentFold.Settings.Default);

            CollectionAssert.AreEqual(a[0], b[0]);
        }

        [Test]
        public void OnlyHeadPiecesStandUp()
        {
            Assert.IsFalse(ArmorWorldFormSetup.IsFolded(SimpleRPG.InventorySystem.EquipmentSlot.head));
            Assert.IsTrue(ArmorWorldFormSetup.IsFolded(SimpleRPG.InventorySystem.EquipmentSlot.chest));
            Assert.IsTrue(ArmorWorldFormSetup.IsFolded(SimpleRPG.InventorySystem.EquipmentSlot.hands));
        }
    }
}
