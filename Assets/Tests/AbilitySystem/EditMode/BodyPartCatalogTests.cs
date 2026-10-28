using NUnit.Framework;
using SimpleRPG.Abilities;
using SimpleRPG.Appearance;
using SimpleRPG.InventorySystem;
using System.Collections.Generic;
using UnityEngine;

namespace SimpleRPG.Abilities.Tests.EditMode
{
    /// <summary>
    /// Body parts travel over the network as the permanent, never-reused short ids that every gameplay definition gets from the
    /// <see cref="DefinitionRegistry"/>, so appearance saved today still means the same parts after definitions are added or removed.
    /// </summary>
    public class BodyPartCatalogTests
    {
        private readonly List<Object> created = new List<Object>();
        private DefinitionRegistry previous;
        private DefinitionRegistry registry;
        private readonly IBodyPartCatalog catalog = new RegistryBodyPartCatalog();

        [SetUp]
        public void SetUp()
        {
            previous = DefinitionRegistry.Instance;
            registry = Create<DefinitionRegistry>();
            DefinitionRegistry.Instance = registry;
        }

        [TearDown]
        public void TearDown()
        {
            DefinitionRegistry.Instance = previous;
            foreach (Object o in created) if (o != null) Object.DestroyImmediate(o);
            created.Clear();
        }

        private T Create<T>() where T : ScriptableObject
        {
            T instance = ScriptableObject.CreateInstance<T>();
            created.Add(instance);
            return instance;
        }

        private BodyPartDefinition Part()
        {
            BodyPartDefinition part = Create<BodyPartDefinition>();
            part.AssignNewId();
            return part;
        }

        [Test]
        public void ABodyPartsWireId_IsItsRegistryShortId_AndResolvesBackToThePart()
        {
            BodyPartDefinition part = Part();
            Assert.IsTrue(registry.Rebuild(new GameplayDefinition[] { part }).Success);

            Assert.IsTrue(catalog.TryGetId(part, out uint id));
            Assert.IsTrue(registry.TryGetShortId(part.Id, out uint registryId));
            Assert.AreEqual(registryId, id, "the catalog adds no id scheme of its own");
            Assert.AreNotEqual(0u, id);

            Assert.IsTrue(catalog.TryGetPart(id, out BodyPartDefinition found));
            Assert.AreSame(part, found);
        }

        [Test]
        public void APartThatIsNotInTheRegistry_HasNoWireId()
        {
            BodyPartDefinition registered = Part();
            BodyPartDefinition stranger = Part();
            registry.Rebuild(new GameplayDefinition[] { registered });

            Assert.IsFalse(catalog.TryGetId(stranger, out _));
            Assert.IsFalse(catalog.TryGetId(null, out _));
            Assert.IsFalse(catalog.TryGetPart(0u, out _), "zero means no part");
        }

        [Test]
        public void AnIdBelongingToAnotherKindOfDefinition_IsNotABodyPart()
        {
            BodyPartDefinition part = Part();
            Item item = Create<Item>();
            item.AssignNewId();
            Assert.IsTrue(registry.Rebuild(new GameplayDefinition[] { part, item }).Success);
            registry.TryGetShortId(item.Id, out uint itemId);

            Assert.IsFalse(catalog.TryGetPart(itemId, out BodyPartDefinition notAPart), "an item's id must not come back as a body part");
            Assert.IsNull(notAPart);
        }

        [Test]
        public void ARetiredPartId_IsNeverGivenToAnotherPart()
        {
            BodyPartDefinition removed = Part();
            BodyPartDefinition kept = Part();
            registry.Rebuild(new GameplayDefinition[] { removed, kept });
            catalog.TryGetId(removed, out uint retiredId);

            BodyPartDefinition added = Part();
            Assert.IsTrue(registry.Rebuild(new GameplayDefinition[] { kept, added }).Success, "the first part is dropped and a new one added");

            Assert.IsFalse(catalog.TryGetPart(retiredId, out _), "a retired id finds nothing");
            Assert.IsTrue(catalog.TryGetId(added, out uint addedId));
            Assert.AreNotEqual(retiredId, addedId, "saved appearance can never be misread as the new part");
        }
    }
}
