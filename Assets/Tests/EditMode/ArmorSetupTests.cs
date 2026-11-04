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

        [Test]
        public void TheSeed_UsesTheStandardGridSizes()
        {
            // 2 by 2 for head, hands and feet; 2 wide and 3 tall for body armor.
            foreach (ArmorSetup.Piece piece in ArmorSetup.Pieces)
            {
                if (ArmorSetup.TryStandardSize(piece.Slot, out Vector2Int standard))
                {
                    Assert.AreEqual(standard, piece.Size, piece.Folder);
                }
            }
        }

        [TestCase(EquipmentSlot.head, 2, 2)]
        [TestCase(EquipmentSlot.hands, 2, 2)]
        [TestCase(EquipmentSlot.feet, 2, 2)]
        [TestCase(EquipmentSlot.chest, 2, 3)]
        public void TheStandardSizes(EquipmentSlot slot, int width, int height)
        {
            Assert.IsTrue(ArmorSetup.TryStandardSize(slot, out Vector2Int size));
            Assert.AreEqual(new Vector2Int(width, height), size);
        }

        [TestCase(EquipmentSlot.legs)]
        [TestCase(EquipmentSlot.neck)]
        [TestCase(EquipmentSlot.mainHand)]
        public void ASlotWithNoStandardSizeYet_HasNone(EquipmentSlot slot)
        {
            Assert.IsFalse(ArmorSetup.TryStandardSize(slot, out _));
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
                if (part.IsSkinned)
                {
                    Assert.IsEmpty(ArmorSetup.BonesMissingFromRig(part.BoneNames), part.name + " is skinned to bones the body rig lacks; export it with Authoring/export_armor.py");
                }
            }
        }
    }
}
