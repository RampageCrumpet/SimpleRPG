using NUnit.Framework;
using SimpleRPG.Appearance;
using SimpleRPG.AppearanceTools;
using SimpleRPG.InventorySystem;
using System.Linq;
using UnityEngine;

namespace SimpleRPG.Tests.EditMode
{
    /// <summary>
    /// The armor authoring tool's rules: which place on the body a mesh covers, and that the seed describes sensible pieces.
    /// </summary>
    public class ArmorSetupTests
    {
        [TestCase(EquipmentSlot.head, "Armor_KettleHelm", BodyPartSlot.Headgear)]
        [TestCase(EquipmentSlot.chest, "MailHauberk", BodyPartSlot.Torso)]
        [TestCase(EquipmentSlot.legs, "Greaves", BodyPartSlot.Legs)]
        public void SinglePlaceSlots_NeedNoSide(EquipmentSlot equipment, string mesh, BodyPartSlot expected)
        {
            Assert.IsTrue(ArmorSetup.TryPartSlot(equipment, mesh, out BodyPartSlot slot));
            Assert.AreEqual(expected, slot);
        }

        [TestCase("ReinforcedGloves_L", BodyPartSlot.HandLeft)]
        [TestCase("ReinforcedGloves_R", BodyPartSlot.HandRight)]
        [TestCase("Gauntlet.L", BodyPartSlot.HandLeft)]
        [TestCase("Gauntlet.R", BodyPartSlot.HandRight)]
        [TestCase("LeftGauntlet", BodyPartSlot.HandLeft)]
        [TestCase("rightglove", BodyPartSlot.HandRight)]
        public void Hands_AreLeftOrRightByTheirName(string mesh, BodyPartSlot expected)
        {
            Assert.IsTrue(ArmorSetup.TryPartSlot(EquipmentSlot.hands, mesh, out BodyPartSlot slot));
            Assert.AreEqual(expected, slot);
        }

        [TestCase("Boot_L", BodyPartSlot.FootLeft)]
        [TestCase("Boot_R", BodyPartSlot.FootRight)]
        public void Feet_AreLeftOrRightByTheirName(string mesh, BodyPartSlot expected)
        {
            Assert.IsTrue(ArmorSetup.TryPartSlot(EquipmentSlot.feet, mesh, out BodyPartSlot slot));
            Assert.AreEqual(expected, slot);
        }

        [TestCase("PlateGauntlets")]
        [TestCase("")]
        [TestCase(null)]
        [TestCase("LeftAndRight")]
        public void AHandOrFootWithNoSideInItsName_IsNotGuessed(string mesh)
        {
            Assert.IsFalse(ArmorSetup.TryPartSlot(EquipmentSlot.hands, mesh, out _));
            Assert.IsFalse(ArmorSetup.TryPartSlot(EquipmentSlot.feet, mesh, out _));
        }

        [TestCase(EquipmentSlot.neck)]
        [TestCase(EquipmentSlot.mainHand)]
        [TestCase(EquipmentSlot.Consumable)]
        public void ASlotThatIsNotArmor_CoversNoPartOfTheBody(EquipmentSlot equipment)
        {
            Assert.IsFalse(ArmorSetup.TryPartSlot(equipment, "Anything_L", out _));
        }

        [Test]
        public void TheSeed_DescribesEachPieceOnce()
        {
            CollectionAssert.AllItemsAreUnique(ArmorSetup.Pieces.Select(p => p.Folder));
            CollectionAssert.AllItemsAreUnique(ArmorSetup.Pieces.Select(p => p.DisplayName));
        }

        [Test]
        public void TheSeed_OnlyUsesArmorSlots_WithPositiveRatings_ThatFitTheInventory()
        {
            foreach (ArmorSetup.Piece piece in ArmorSetup.Pieces)
            {
                Assert.IsTrue(ArmorData.IsArmorSlot(piece.Slot), piece.Folder + " is not in an armor slot.");
                Assert.Greater(piece.ArmorRating, 0f, piece.Folder);
                Assert.Greater(piece.Size.x, 0, piece.Folder);
                Assert.Greater(piece.Size.y, 0, piece.Folder);

                // The player's grid is 12 by 4 cells.
                Assert.LessOrEqual(piece.Size.x, 12, piece.Folder);
                Assert.LessOrEqual(piece.Size.y, 4, piece.Folder);
            }
        }

        [TestCase(EquipmentSlot.head, 2, 2)]
        [TestCase(EquipmentSlot.hands, 2, 2)]
        [TestCase(EquipmentSlot.feet, 2, 2)]
        [TestCase(EquipmentSlot.chest, 2, 3)]
        [TestCase(EquipmentSlot.legs, 2, 3)]
        public void TheDefaultSizeOfASlot(EquipmentSlot slot, int width, int height)
        {
            Assert.IsTrue(ArmorSetup.TryDefaultSize(slot, out Vector2Int size));
            Assert.AreEqual(new Vector2Int(width, height), size);
        }

        [TestCase(EquipmentSlot.neck)]
        [TestCase(EquipmentSlot.mainHand)]
        [TestCase(EquipmentSlot.Consumable)]
        public void ASlotThatIsNotArmor_HasNoDefaultSize(EquipmentSlot slot)
        {
            Assert.IsFalse(ArmorSetup.TryDefaultSize(slot, out _));
        }

        [Test]
        public void APieceStartsWithItsSlotsDefaultSize()
        {
            var legs = new ArmorSetup.Piece("Greaves", "Greaves", EquipmentSlot.legs, 20f);
            var boots = new ArmorSetup.Piece("Boots", "Boots", EquipmentSlot.feet, 6f);

            Assert.AreEqual(new Vector2Int(2, 3), legs.Size);
            Assert.AreEqual(new Vector2Int(2, 2), boots.Size);
        }

        [Test]
        public void APieceMayHaveASizeOfItsOwn_ForAnySlot()
        {
            var tower = new ArmorSetup.Piece("TowerHelm", "Tower Helm", EquipmentSlot.head, new Vector2Int(3, 3), 30f);
            var cap = new ArmorSetup.Piece("Cap", "Cap", EquipmentSlot.chest, new Vector2Int(1, 1), 1f);

            Assert.AreEqual(new Vector2Int(3, 3), tower.Size, "Nothing forces a helm to be 2 by 2.");
            Assert.AreEqual(new Vector2Int(1, 1), cap.Size);
        }

        [Test]
        public void TheSeed_StartsEachPieceAtItsSlotsDefault_BecauseNoneOverridesIt()
        {
            foreach (ArmorSetup.Piece piece in ArmorSetup.Pieces)
            {
                Assert.IsTrue(ArmorSetup.TryDefaultSize(piece.Slot, out Vector2Int size), piece.Folder);
                Assert.AreEqual(size, piece.Size, piece.Folder);
            }
        }

        [Test]
        public void TheSeed_HeavierPiecesOfASlotProtectMore()
        {
            foreach (var group in ArmorSetup.Pieces.GroupBy(p => p.Slot))
            {
                float[] ratings = group.Select(p => p.ArmorRating).ToArray();
                CollectionAssert.AllItemsAreUnique(ratings, group.Key + " pieces should be told apart by protection.");
            }

            float Rating(string folder) => ArmorSetup.Pieces.Single(p => p.Folder == folder).ArmorRating;
            Assert.Less(Rating("LinenTunic"), Rating("Gambeson"));
            Assert.Less(Rating("Gambeson"), Rating("MailHauberk"));
            Assert.Less(Rating("MailHauberk"), Rating("PlateArmor"));
            Assert.Less(Rating("LeatherGloves"), Rating("PlateGauntlets"));
        }

        [Test]
        public void TheSeed_CoversHeadChestAndHands_UntilLegsAndFeetArrive()
        {
            CollectionAssert.IsSupersetOf(ArmorSetup.Pieces.Select(p => p.Slot).Distinct(), new[] { EquipmentSlot.head, EquipmentSlot.chest, EquipmentSlot.hands });
        }

        [Test]
        public void EveryArmorPart_IsSkinnedOnlyToBonesTheBodyRigHas()
        {
            foreach (string guid in UnityEditor.AssetDatabase.FindAssets("t:BodyPartDefinition", new[] { ArmorSetup.PartsFolder }))
            {
                var part = UnityEditor.AssetDatabase.LoadAssetAtPath<BodyPartDefinition>(UnityEditor.AssetDatabase.GUIDToAssetPath(guid));
                foreach (BodyPartPiece piece in part.Pieces)
                {
                    if (piece.IsSkinned)
                    {
                        Assert.IsEmpty(ArmorSetup.BonesMissingFromRig(piece.BoneNames), part.name + " is skinned to bones the body rig lacks; export it with Authoring/export_armor.py");
                    }
                }
            }
        }

        [Test]
        public void NoSlotOfAnItem_IsDressedByMoreThanOnePart()
        {
            var seen = new System.Collections.Generic.HashSet<(Item, BodyPartSlot)>();
            foreach (string guid in UnityEditor.AssetDatabase.FindAssets("t:BodyPartDefinition", new[] { ArmorSetup.PartsFolder }))
            {
                var part = UnityEditor.AssetDatabase.LoadAssetAtPath<BodyPartDefinition>(UnityEditor.AssetDatabase.GUIDToAssetPath(guid));
                if (part.DressesItem == null) continue;
                Assert.IsTrue(seen.Add((part.DressesItem, part.Slot)),
                    part.name + " shares " + part.Slot + " of " + part.DressesItem.name + " with another part, so only one would show. Merge the meshes into one part (more pieces) with SimpleRPG > Items > Build Armor Items And Parts.");
            }
        }
    }
}
