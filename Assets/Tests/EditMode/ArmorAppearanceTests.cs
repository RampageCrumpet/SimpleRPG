using NUnit.Framework;
using SimpleRPG.Abilities;
using SimpleRPG.Appearance;
using SimpleRPG.InventorySystem;
using System.Collections.Generic;
using UnityEngine;

namespace SimpleRPG.Tests.EditMode
{
    /// <summary>
    /// Worn armor dresses a character: which parts are the look of an item, and what each slot shows with armor on and off.
    /// </summary>
    public class ArmorAppearanceTests
    {
        private readonly List<Object> created = new List<Object>();
        private DefinitionRegistry previousRegistry;

        [SetUp]
        public void SetUp()
        {
            previousRegistry = DefinitionRegistry.Instance;
            ArmorParts.Reset();
        }

        [TearDown]
        public void TearDown()
        {
            DefinitionRegistry.Instance = previousRegistry;
            ArmorParts.Reset();
            foreach (Object o in created)
            {
                Object.DestroyImmediate(o);
            }

            created.Clear();
        }

        private BodyPartDefinition Part(BodyPartSlot slot, Item dresses = null)
        {
            BodyPartDefinition part = ScriptableObject.CreateInstance<BodyPartDefinition>();
            created.Add(part);
            part.EnsureId();
            part.Configure(slot, null, null, null, "", new Bounds(), "", Vector3.zero, Quaternion.identity, Vector3.one);
            part.Dress(dresses);
            return part;
        }

        private ArmorData Armor()
        {
            ArmorData armor = ScriptableObject.CreateInstance<ArmorData>();
            created.Add(armor);
            armor.EnsureId();
            return armor;
        }

        private void Register(params GameplayDefinition[] definitions)
        {
            DefinitionRegistry registry = ScriptableObject.CreateInstance<DefinitionRegistry>();
            created.Add(registry);
            RegistryReport report = registry.Rebuild(definitions);
            Assert.IsTrue(report.Success, string.Join("; ", report.Errors));
            DefinitionRegistry.Instance = registry;
        }

        // ---- What each slot shows ----

        [Test]
        public void WithNoArmor_TheBareBodyShows()
        {
            BodyPartDefinition head = Part(BodyPartSlot.Head);
            BodyPartDefinition torso = Part(BodyPartSlot.Torso);

            var desired = ArmorAppearancePlan.Desired(new[] { head, torso }, null);

            Assert.AreSame(head, desired[BodyPartSlot.Head]);
            Assert.AreSame(torso, desired[BodyPartSlot.Torso]);
            Assert.AreEqual(2, desired.Count);
        }

        [Test]
        public void ArmorShowsInPlaceOfTheBarePartInItsSlot_AndNothingElseChanges()
        {
            BodyPartDefinition bareHead = Part(BodyPartSlot.Head);
            BodyPartDefinition bareTorso = Part(BodyPartSlot.Torso);
            BodyPartDefinition helm = Part(BodyPartSlot.Head);

            var desired = ArmorAppearancePlan.Desired(new[] { bareHead, bareTorso }, new[] { helm });

            Assert.AreSame(helm, desired[BodyPartSlot.Head]);
            Assert.AreSame(bareTorso, desired[BodyPartSlot.Torso]);
        }

        [Test]
        public void TakingTheArmorOff_ReturnsTheBarePart()
        {
            BodyPartDefinition bareHead = Part(BodyPartSlot.Head);
            BodyPartDefinition helm = Part(BodyPartSlot.Head);

            Assert.AreSame(helm, ArmorAppearancePlan.Desired(new[] { bareHead }, new[] { helm })[BodyPartSlot.Head]);
            Assert.AreSame(bareHead, ArmorAppearancePlan.Desired(new[] { bareHead }, new BodyPartDefinition[0])[BodyPartSlot.Head]);
        }

        [Test]
        public void AnItemWithTwoParts_CoversBothSides()
        {
            BodyPartDefinition left = Part(BodyPartSlot.HandLeft);
            BodyPartDefinition right = Part(BodyPartSlot.HandRight);

            var desired = ArmorAppearancePlan.Desired(new BodyPartDefinition[0], new[] { left, right });

            Assert.AreSame(left, desired[BodyPartSlot.HandLeft]);
            Assert.AreSame(right, desired[BodyPartSlot.HandRight]);
        }

        [Test]
        public void ASlotWithNeitherArmorNorABarePart_ShowsNothing()
        {
            var desired = ArmorAppearancePlan.Desired(new[] { Part(BodyPartSlot.Head) }, null);

            Assert.IsFalse(desired.ContainsKey(BodyPartSlot.Legs));
        }

        [Test]
        public void IfTwoPartsClaimASlot_TheLaterWins()
        {
            BodyPartDefinition first = Part(BodyPartSlot.Torso);
            BodyPartDefinition second = Part(BodyPartSlot.Torso);

            Assert.AreSame(second, ArmorAppearancePlan.Desired(null, new[] { first, second })[BodyPartSlot.Torso]);
        }

        [Test]
        public void MissingPartsAreIgnored()
        {
            var desired = ArmorAppearancePlan.Desired(new BodyPartDefinition[] { null }, new BodyPartDefinition[] { null });

            Assert.AreEqual(0, desired.Count);
        }

        // ---- Which parts are an item's look ----

        [Test]
        public void ThePartsOfAnItem_AreFoundFromTheParts_ThroughTheRegistry()
        {
            ArmorData plate = Armor();
            ArmorData mail = Armor();
            BodyPartDefinition plateLeft = Part(BodyPartSlot.HandLeft, plate);
            BodyPartDefinition plateRight = Part(BodyPartSlot.HandRight, plate);
            BodyPartDefinition mailTorso = Part(BodyPartSlot.Torso, mail);
            BodyPartDefinition bare = Part(BodyPartSlot.Head);
            Register(plate, mail, plateLeft, plateRight, mailTorso, bare);

            CollectionAssert.AreEquivalent(new[] { plateLeft, plateRight }, ArmorParts.For(plate));
            CollectionAssert.AreEquivalent(new[] { mailTorso }, ArmorParts.For(mail));
        }

        [Test]
        public void AnInstanceOfAnItem_HasItsAssetsParts()
        {
            ArmorData helm = Armor();
            BodyPartDefinition part = Part(BodyPartSlot.Head, helm);
            Register(helm, part);
            Item instance = helm.CloneInstance();
            created.Add(instance);

            CollectionAssert.AreEquivalent(new[] { part }, ArmorParts.For(instance));
        }

        [Test]
        public void AnItemWithNoParts_OrNoItem_HasNone()
        {
            ArmorData naked = Armor();
            Register(naked);

            CollectionAssert.IsEmpty(ArmorParts.For(naked));
            CollectionAssert.IsEmpty(ArmorParts.For(null));
        }

        [Test]
        public void ANewRegistry_IsLookedAtAfresh()
        {
            ArmorData helm = Armor();
            BodyPartDefinition part = Part(BodyPartSlot.Head, helm);
            Register(helm);
            CollectionAssert.IsEmpty(ArmorParts.For(helm));

            Register(helm, part);

            CollectionAssert.AreEquivalent(new[] { part }, ArmorParts.For(helm));
        }

        [Test]
        public void ABarePart_DressesNoItem()
        {
            Assert.IsNull(Part(BodyPartSlot.Head).DressesItem);
        }
    }
}
