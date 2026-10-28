using NUnit.Framework;
using SimpleRPG.Abilities.Editor;
using SimpleRPG.Gameplay;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace SimpleRPG.Abilities.Tests.EditMode
{
    public class DefinitionRegistryTests
    {
        private readonly List<Object> created = new List<Object>();

        private DefinitionRegistry registry;

        [SetUp]
        public void SetUp()
        {
            registry = Create<DefinitionRegistry>();
        }

        [TearDown]
        public void TearDown()
        {
            foreach (Object o in created)
            {
                if (o != null) Object.DestroyImmediate(o);
            }

            created.Clear();
        }

        private T Create<T>() where T : ScriptableObject
        {
            T instance = ScriptableObject.CreateInstance<T>();
            created.Add(instance);
            return instance;
        }

        private TestDefinition Definition(string name, bool withId = true)
        {
            TestDefinition definition = Create<TestDefinition>();
            definition.name = name;

            if (withId)
            {
                definition.AssignNewId();
            }
            else
            {
                // In the editor a new definition is given an ID as it is created, and applying serialized changes re-checks it, so the field is
                // cleared directly to stand in for a definition that was saved without one.
                typeof(GameplayDefinition)
                    .GetField("definitionId", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                    .SetValue(definition, string.Empty);
            }

            return definition;
        }

        [Test]
        public void NewDefinition_HasNoId_UntilOneIsAssigned()
        {
            TestDefinition definition = Definition("A", withId: false);

            Assert.IsFalse(definition.HasId);
            Assert.IsTrue(definition.Id.IsNone);

            Assert.IsTrue(definition.EnsureId());
            Assert.IsTrue(definition.HasId);
        }

        [Test]
        public void DefinitionCreatedInTheEditor_IsGivenAnIdAutomatically()
        {
            TestDefinition definition = Create<TestDefinition>();

            Assert.IsTrue(definition.HasId, "Reset assigns an ID, so a definition is never saved without one.");
        }

        [Test]
        public void EnsureId_NeverChangesAnExistingId()
        {
            TestDefinition definition = Definition("A");
            DefinitionId original = definition.Id;

            Assert.IsFalse(definition.EnsureId());

            Assert.AreEqual(original, definition.Id);
        }

        [Test]
        public void AssignNewId_ReplacesTheId()
        {
            TestDefinition definition = Definition("A");
            DefinitionId original = definition.Id;

            definition.AssignNewId();

            Assert.AreNotEqual(original, definition.Id);
        }

        [Test]
        public void Validate_ReportsAMissingId_ByName()
        {
            List<string> errors = DefinitionRegistry.Validate(new[] { Definition("Ghost", withId: false) });

            Assert.AreEqual(1, errors.Count);
            StringAssert.Contains("Ghost", errors[0]);
            StringAssert.Contains("no definition ID", errors[0]);
        }

        [Test]
        public void Validate_ReportsDuplicateIds_NamingEveryHolder()
        {
            TestDefinition first = Definition("First");
            TestDefinition second = Definition("Second");
            SerializedObject copy = new SerializedObject(second);
            copy.FindProperty("definitionId").stringValue = first.Id.ToString();
            copy.ApplyModifiedPropertiesWithoutUndo();

            List<string> errors = DefinitionRegistry.Validate(new[] { first, second });

            Assert.AreEqual(1, errors.Count);
            StringAssert.Contains("First", errors[0]);
            StringAssert.Contains("Second", errors[0]);
        }

        [Test]
        public void Validate_AcceptsDistinctIds()
        {
            Assert.IsEmpty(DefinitionRegistry.Validate(new[] { Definition("A"), Definition("B") }));
        }

        [Test]
        public void Rebuild_RegistersEveryDefinition_AndLooksThemUpByBothIds()
        {
            TestDefinition a = Definition("A");
            TestDefinition b = Definition("B");

            RegistryReport report = registry.Rebuild(new[] { a, b });

            Assert.IsTrue(report.Success);
            Assert.AreEqual(2, report.Added);
            Assert.AreEqual(2, registry.Count);

            Assert.IsTrue(registry.TryGet(a.Id, out GameplayDefinition foundA));
            Assert.AreSame(a, foundA);

            Assert.IsTrue(registry.TryGetShortId(b.Id, out uint shortId));
            Assert.IsTrue(registry.TryGetByShortId(shortId, out GameplayDefinition foundB));
            Assert.AreSame(b, foundB);
        }

        [Test]
        public void Rebuild_AssignsDistinctNonZeroShortIds()
        {
            TestDefinition[] definitions = Enumerable.Range(0, 10).Select(i => Definition("D" + i)).ToArray();

            registry.Rebuild(definitions);

            List<uint> shortIds = definitions.Select(d => { registry.TryGetShortId(d.Id, out uint s); return s; }).ToList();
            CollectionAssert.AllItemsAreUnique(shortIds);
            Assert.IsTrue(shortIds.All(s => s != 0));
        }

        [Test]
        public void Rebuild_KeepsExistingShortIds_AndAppendsNewOnes()
        {
            TestDefinition a = Definition("A");
            registry.Rebuild(new[] { a });
            registry.TryGetShortId(a.Id, out uint originalShortId);

            TestDefinition b = Definition("B");
            RegistryReport report = registry.Rebuild(new[] { a, b });

            Assert.AreEqual(1, report.Added);
            registry.TryGetShortId(a.Id, out uint afterShortId);
            Assert.AreEqual(originalShortId, afterShortId, "Adding a definition must not change an existing short ID.");
        }

        [Test]
        public void Rebuild_NeverReusesARetiredShortId()
        {
            TestDefinition a = Definition("A");
            TestDefinition b = Definition("B");
            registry.Rebuild(new[] { a, b });
            registry.TryGetShortId(b.Id, out uint retired);

            RegistryReport removal = registry.Rebuild(new[] { a });
            TestDefinition c = Definition("C");
            registry.Rebuild(new[] { a, c });

            Assert.AreEqual(1, removal.Removed);
            Assert.IsFalse(registry.TryGetByShortId(retired, out _));
            registry.TryGetShortId(c.Id, out uint newShortId);
            Assert.AreNotEqual(retired, newShortId);
        }

        [Test]
        public void Rebuild_DoesNotDependOnTheOrderDefinitionsAreGivenIn()
        {
            TestDefinition a = Definition("A");
            TestDefinition b = Definition("B");
            TestDefinition c = Definition("C");
            DefinitionRegistry other = Create<DefinitionRegistry>();

            registry.Rebuild(new[] { a, b, c });
            other.Rebuild(new[] { c, a, b });

            foreach (TestDefinition d in new[] { a, b, c })
            {
                registry.TryGetShortId(d.Id, out uint x);
                other.TryGetShortId(d.Id, out uint y);
                Assert.AreEqual(x, y);
            }
        }

        [Test]
        public void Rebuild_WithAnInvalidDefinition_ChangesNothing()
        {
            TestDefinition good = Definition("Good");
            registry.Rebuild(new[] { good });

            RegistryReport report = registry.Rebuild(new[] { good, Definition("Bad", withId: false) });

            Assert.IsFalse(report.Success);
            Assert.AreEqual(1, registry.Count);
            Assert.IsTrue(registry.TryGet(good.Id, out _));
        }

        private AbilityDefinition Ability(string displayName)
        {
            AbilityDefinition ability = Create<AbilityDefinition>();
            typeof(AbilityDefinition)
                .GetField("abilityName", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                .SetValue(ability, displayName);
            ability.AssignNewId();
            return ability;
        }

        [Test]
        public void AbilitiesWithTheSameDisplayName_AreTwoAbilities_ToTheValidatorAndTheRegistry()
        {
            AbilityDefinition first = Ability("Slash");
            AbilityDefinition second = Ability("Slash");

            Assert.AreEqual(first.AbilityName, second.AbilityName);
            Assert.IsEmpty(DefinitionRegistry.Validate(new GameplayDefinition[] { first, second }), "a shared display name is not an error");
            Assert.IsTrue(registry.Rebuild(new GameplayDefinition[] { first, second }).Success);

            Assert.IsTrue(registry.TryGetShortId(first.Id, out uint firstShort));
            Assert.IsTrue(registry.TryGetShortId(second.Id, out uint secondShort));
            Assert.AreNotEqual(firstShort, secondShort);
            Assert.IsTrue(registry.TryGetByShortId(firstShort, out GameplayDefinition foundFirst));
            Assert.IsTrue(registry.TryGetByShortId(secondShort, out GameplayDefinition foundSecond));
            Assert.AreSame(first, foundFirst, "each id finds its own ability, whatever the display names are");
            Assert.AreSame(second, foundSecond);
        }

        [Test]
        public void ChangingAnAbilitysDisplayName_ChangesNeitherItsIdNorHowItIsFound()
        {
            AbilityDefinition ability = Ability("Firebolt");
            Assert.IsTrue(registry.Rebuild(new GameplayDefinition[] { ability }).Success);
            DefinitionId id = ability.Id;
            registry.TryGetShortId(id, out uint shortId);

            typeof(AbilityDefinition)
                .GetField("abilityName", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                .SetValue(ability, "Flame Dart");

            Assert.AreEqual("Flame Dart", ability.AbilityName);
            Assert.AreEqual(id, ability.Id);
            Assert.IsTrue(registry.TryGetByShortId(shortId, out GameplayDefinition byShort));
            Assert.AreSame(ability, byShort);
            Assert.IsTrue(registry.TryGet(id, out GameplayDefinition byId));
            Assert.AreSame(ability, byId);
            Assert.IsTrue(registry.CheckUpToDate(new GameplayDefinition[] { ability }).Success, "a rename leaves the registry up to date");
        }

        [Test]
        public void Lookup_OfAnUnknownId_ReturnsFalse()
        {
            registry.Rebuild(new[] { Definition("A") });

            Assert.IsFalse(registry.TryGet(DefinitionId.NewId(), out GameplayDefinition found));
            Assert.IsNull(found);
            Assert.IsFalse(registry.TryGetByShortId(9999, out _));
            Assert.IsFalse(registry.TryGetShortId(DefinitionId.NewId(), out _));
        }

        [Test]
        public void CheckUpToDate_PassesRightAfterARebuild()
        {
            TestDefinition[] definitions = { Definition("A"), Definition("B") };
            registry.Rebuild(definitions);

            Assert.IsTrue(registry.CheckUpToDate(definitions).Success);
        }

        [Test]
        public void CheckUpToDate_FlagsAnUnregisteredDefinition()
        {
            TestDefinition a = Definition("A");
            registry.Rebuild(new[] { a });

            RegistryReport report = registry.CheckUpToDate(new[] { a, Definition("Late") });

            Assert.IsFalse(report.Success);
            StringAssert.Contains("Late", report.Errors[0]);
        }

        [Test]
        public void CheckUpToDate_FlagsAStaleEntry()
        {
            TestDefinition a = Definition("A");
            TestDefinition b = Definition("B");
            registry.Rebuild(new[] { a, b });

            RegistryReport report = registry.CheckUpToDate(new[] { a });

            Assert.IsFalse(report.Success);
            StringAssert.Contains("stale", report.Errors[0]);
        }

        [Test]
        public void IdFixer_GivesADuplicateANewId_AndLeavesTheOriginalAlone()
        {
            TestDefinition original = Definition("Original");
            TestDefinition copy = Definition("Copy");
            SerializedObject serialized = new SerializedObject(copy);
            serialized.FindProperty("definitionId").stringValue = original.Id.ToString();
            serialized.ApplyModifiedPropertiesWithoutUndo();
            DefinitionId originalId = original.Id;

            List<GameplayDefinition> changed = DefinitionIdFixer.Fix(new[] { copy }, new[] { original, copy });

            Assert.AreEqual(1, changed.Count);
            Assert.AreSame(copy, changed[0]);
            Assert.AreEqual(originalId, original.Id);
            Assert.AreNotEqual(originalId, copy.Id);
            Assert.IsEmpty(DefinitionRegistry.Validate(new[] { original, copy }));
        }

        [Test]
        public void IdFixer_GivesAnImportedDefinitionWithoutAnIdOne()
        {
            TestDefinition fresh = Definition("Fresh", withId: false);

            DefinitionIdFixer.Fix(new[] { fresh }, new[] { fresh });

            Assert.IsTrue(fresh.HasId);
        }

        [Test]
        public void IdFixer_LeavesAUniqueIdAlone()
        {
            TestDefinition imported = Definition("Imported");
            DefinitionId id = imported.Id;

            List<GameplayDefinition> changed = DefinitionIdFixer.Fix(new[] { imported }, new[] { imported, Definition("Other") });

            Assert.IsEmpty(changed);
            Assert.AreEqual(id, imported.Id);
        }

        [Test]
        public void IdFixer_ImportedPair_SharingAnId_EndWithDistinctIds()
        {
            TestDefinition a = Definition("A");
            TestDefinition b = Definition("B");
            SerializedObject serialized = new SerializedObject(b);
            serialized.FindProperty("definitionId").stringValue = a.Id.ToString();
            serialized.ApplyModifiedPropertiesWithoutUndo();

            DefinitionIdFixer.Fix(new[] { a, b }, new[] { a, b });

            Assert.IsEmpty(DefinitionRegistry.Validate(new[] { a, b }));
        }
    }

    /// <summary>
    /// Checks IDs and the registry through real assets: identity survives rename and move, a duplicated asset is repaired, and the project's own
    /// registry asset is present and up to date. This last test is the same check the build gate runs.
    /// </summary>
    public class DefinitionAssetTests
    {
        private const string TempFolder = "Assets/Tests/AbilitySystem/EditMode/TempDefinitions";

        [SetUp]
        public void SetUp()
        {
            if (!AssetDatabase.IsValidFolder(TempFolder))
            {
                AssetDatabase.CreateFolder("Assets/Tests/AbilitySystem/EditMode", "TempDefinitions");
            }
        }

        [TearDown]
        public void TearDown()
        {
            AssetDatabase.DeleteAsset(TempFolder);
            AssetDatabase.Refresh();
        }

        private static TestDefinition CreateAsset(string name)
        {
            TestDefinition definition = ScriptableObject.CreateInstance<TestDefinition>();
            definition.AssignNewId();
            AssetDatabase.CreateAsset(definition, TempFolder + "/" + name + ".asset");
            return definition;
        }

        [Test]
        public void Id_SurvivesRenameAndMove()
        {
            TestDefinition definition = CreateAsset("Original");
            DefinitionId id = definition.Id;
            string path = AssetDatabase.GetAssetPath(definition);

            Assert.AreEqual(string.Empty, AssetDatabase.RenameAsset(path, "Renamed"));
            AssetDatabase.CreateFolder(TempFolder, "Moved");
            Assert.AreEqual(string.Empty, AssetDatabase.MoveAsset(TempFolder + "/Renamed.asset", TempFolder + "/Moved/Renamed.asset"));

            TestDefinition reloaded = AssetDatabase.LoadAssetAtPath<TestDefinition>(TempFolder + "/Moved/Renamed.asset");
            Assert.AreEqual(id, reloaded.Id);
        }

        [Test]
        public void Id_IsSavedWithTheAsset()
        {
            TestDefinition definition = CreateAsset("Saved");
            DefinitionId id = definition.Id;
            AssetDatabase.SaveAssets();
            AssetDatabase.ImportAsset(AssetDatabase.GetAssetPath(definition));

            TestDefinition reloaded = AssetDatabase.LoadAssetAtPath<TestDefinition>(AssetDatabase.GetAssetPath(definition));

            Assert.AreEqual(id, reloaded.Id);
        }

        [Test]
        public void DuplicatedAsset_CopiesTheId_AndTheFixerGivesTheCopyANewOne()
        {
            TestDefinition original = CreateAsset("Source");
            string originalPath = AssetDatabase.GetAssetPath(original);
            string copyPath = TempFolder + "/Source Copy.asset";
            Assert.IsTrue(AssetDatabase.CopyAsset(originalPath, copyPath));
            TestDefinition copy = AssetDatabase.LoadAssetAtPath<TestDefinition>(copyPath);
            Assert.AreEqual(original.Id, copy.Id, "A raw duplicate copies the serialized ID, which is why the fixer exists.");

            List<GameplayDefinition> changed = DefinitionIdFixer.Fix(new[] { copy }, new GameplayDefinition[] { original, copy });

            Assert.AreEqual(1, changed.Count);
            Assert.AreNotEqual(original.Id, copy.Id);
        }

        [Test]
        public void ProjectDefinitions_AreValid_AndTheRegistryAssetIsUpToDate()
        {
            List<string> problems = DefinitionRegistryTools.ValidateForBuild();

            Assert.IsEmpty(problems, string.Join("\n", problems));
        }

        [Test]
        public void RegistryAsset_LoadsFromResources_WithoutTheAssetDatabase()
        {
            DefinitionRegistry loaded = Resources.Load<DefinitionRegistry>(DefinitionRegistry.ResourcePath);

            Assert.IsNotNull(loaded, "The registry must be in a Resources folder so it ships in builds and dedicated servers.");
        }
    }
}
