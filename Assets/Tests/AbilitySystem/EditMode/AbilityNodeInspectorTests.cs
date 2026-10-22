using NUnit.Framework;
using SimpleRPG.Abilities.Editor;
using SimpleRPG.Gameplay;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace SimpleRPG.Abilities.Tests.EditMode
{
    /// <summary>
    /// The node settings panel: every node type's settings can be edited from it, edits are undoable and saved with the asset, and they make
    /// the ability compile again.
    /// </summary>
    public class AbilityNodeInspectorTests
    {
        private const string Firebolt = "Assets/Abilitities/Abillities/Firebolt.asset";
        private const string TempFolder = "Assets/_NodeInspectorTests";

        private readonly List<UnityEngine.Object> created = new List<UnityEngine.Object>();

        [TearDown]
        public void TearDown()
        {
            foreach (AbilityGraphWindow window in Resources.FindObjectsOfTypeAll<AbilityGraphWindow>())
            {
                window.Close();
            }

            foreach (UnityEngine.Object o in created)
            {
                if (o != null) UnityEngine.Object.DestroyImmediate(o);
            }

            created.Clear();
            if (AssetDatabase.IsValidFolder(TempFolder)) AssetDatabase.DeleteAsset(TempFolder);
        }

        private AbilityDefinition NewAbility()
        {
            AbilityDefinition ability = ScriptableObject.CreateInstance<AbilityDefinition>();
            ability.name = "InspectorTest";
            created.Add(ability);
            return ability;
        }

        /// <summary>An ability saved as an asset, so a real window can open it and a save can be read back.</summary>
        private AbilityDefinition NewAsset(string fileName)
        {
            if (!AssetDatabase.IsValidFolder(TempFolder)) AssetDatabase.CreateFolder("Assets", "_NodeInspectorTests");
            AbilityDefinition ability = ScriptableObject.CreateInstance<AbilityDefinition>();
            AssetDatabase.CreateAsset(ability, TempFolder + "/" + fileName + ".asset");
            return ability;
        }

        private static IEnumerator WaitFor(Func<bool> condition, string what)
        {
            for (int i = 0; i < 60 && !condition(); i++)
            {
                yield return null;
            }

            Assert.IsTrue(condition(), "Timed out waiting for " + what);
        }

        private static AbilityNodeRecord Node(AbilityDefinition ability, string nodeId) => ability.Nodes.Single(n => n.NodeId == nodeId);

        private static IEnumerable<FieldInfo> SettingsOf(Type configType) =>
            configType.GetFields(BindingFlags.Public | BindingFlags.Instance).Where(f => f.GetCustomAttribute<NonSerializedAttribute>() == null);

        /// <summary>Opens a real window on the ability and selects a node, so its settings are shown with live bindings.</summary>
        private static IEnumerator OpenAndSelect(AbilityDefinition ability, string nodeId, Action<AbilityGraphWindow> opened)
        {
            AbilityGraphWindow window = AbilityGraphWindow.Open(ability);
            window.position = new Rect(100, 100, 1000, 600);
            yield return null;
            window.View.ClearSelection();
            window.View.AddToSelection(window.View.NodeViews[nodeId]);
            yield return WaitFor(() => window.Inspector.NodeId == nodeId, "the inspector to show the node");
            yield return null;
            opened(window);
        }

        // ----- Every node type -----

        [Test]
        public void EveryNodeType_ShowsAFieldForEachOfItsSettings()
        {
            int checkedTypes = 0;
            foreach (IAbilityNodeDescriptor descriptor in AbilityNodeRegistry.Default.All)
            {
                AbilityDefinition ability = NewAbility();
                string id = ability.AddNode(descriptor.TypeId);
                AbilityNodeInspector inspector = new AbilityNodeInspector(new AbilityGraphDocument(ability));

                inspector.Show(id);

                Assert.AreEqual(id, inspector.NodeId);
                Assert.AreEqual(SettingsOf(descriptor.ConfigType).Count(), inspector.Fields.Count(), descriptor.TypeId + " should show one field per setting.");
                checkedTypes++;
            }

            Assert.Greater(checkedTypes, 15, "All the built-in node types are covered.");
        }

        [Test]
        public void ANodeWithNoSettings_SaysSo()
        {
            AbilityDefinition ability = NewAbility();
            string id = ability.AddNode(NodeTypes.Activate, null, "extraActivate");
            AbilityNodeInspector inspector = new AbilityNodeInspector(new AbilityGraphDocument(ability));

            inspector.Show(id);

            Assert.IsEmpty(inspector.Fields);
            Assert.IsTrue(inspector.Query<Label>().ToList().Any(l => l.text.Contains("no settings")));
        }

        [Test]
        public void NothingSelected_ShowsTheAbilitysOwnDetails()
        {
            AbilityDefinition ability = NewAbility();
            AbilityNodeInspector inspector = new AbilityNodeInspector(new AbilityGraphDocument(ability));

            inspector.Show(null);

            Assert.IsNull(inspector.NodeId);
            CollectionAssert.AreEquivalent(
                new[] { "abilityName", "toolTip", "icon", "sound", "cooldownSeconds" },
                inspector.Fields.Select(f => f.bindingPath));
        }

        [Test]
        public void ANodeWithMissingSettings_OffersToResetThemToTheDefaults()
        {
            AbilityDefinition ability = NewAbility();
            AbilityGraphDocument document = new AbilityGraphDocument(ability);
            string id = ability.AddNode(NodeTypes.Chance, null, "chance");
            ability.SetNodeConfig(id, null);
            AbilityNodeInspector inspector = new AbilityNodeInspector(document);

            inspector.Show(id);

            Assert.IsEmpty(inspector.Fields);
            Assert.IsTrue(inspector.Query<Button>().ToList().Any(b => b.text.Contains("Reset")));

            Assert.IsTrue(document.ResetConfig(id));
            Assert.IsInstanceOf<ChanceConfig>(Node(ability, id).Config);
            Assert.AreEqual(0.5f, ((ChanceConfig)Node(ability, id).Config).Probability);
        }

        [Test]
        public void ANodeWithTheWrongKindOfSettings_OffersToResetThem()
        {
            AbilityDefinition ability = NewAbility();
            string id = ability.AddNode(NodeTypes.Chance, null, "chance");
            ability.SetNodeConfig(id, new ConstantConfig { Value = 3f });
            AbilityNodeInspector inspector = new AbilityNodeInspector(new AbilityGraphDocument(ability));

            inspector.Show(id);

            Assert.IsEmpty(inspector.Fields);
            Assert.IsTrue(inspector.Query<Button>().ToList().Any(b => b.text.Contains("Reset")));
        }

        [Test]
        public void ANodeOfAnUnknownType_ShowsNoFields()
        {
            AbilityDefinition ability = NewAbility();
            string id = ability.AddNode("gone.v1", null, "ghost");
            AbilityNodeInspector inspector = new AbilityNodeInspector(new AbilityGraphDocument(ability));

            inspector.Show(id);

            Assert.IsEmpty(inspector.Fields);
        }

        [Test]
        public void ResettingSettings_IsUndoable()
        {
            AbilityDefinition ability = NewAbility();
            AbilityGraphDocument document = new AbilityGraphDocument(ability);
            string id = ability.AddNode(NodeTypes.Constant, new ConstantConfig { Value = 9f }, "c");

            Undo.IncrementCurrentGroup();
            document.ResetConfig(id);
            Undo.IncrementCurrentGroup();
            Assert.AreEqual(0f, ((ConstantConfig)Node(ability, id).Config).Value);

            Undo.PerformUndo();

            Assert.AreEqual(9f, ((ConstantConfig)Node(ability, id).Config).Value);
        }

        // ----- Selection -----

        [Test]
        public void TheCanvas_ReportsWhichNodesAreSelected()
        {
            AbilityDefinition ability = NewAbility();
            ability.AddNode(NodeTypes.Damage, null, "damage");
            AbilityGraphView view = new AbilityGraphView(new AbilityGraphDocument(ability));
            int raised = 0;
            view.SelectionChanged += () => raised++;

            view.AddToSelection(view.NodeViews["damage"]);

            CollectionAssert.AreEqual(new[] { "damage" }, view.SelectedNodeIds);
            Assert.AreEqual(1, raised);

            view.ClearSelection();
            Assert.IsEmpty(view.SelectedNodeIds);
            Assert.AreEqual(2, raised);
        }

        [Test]
        public void ARedraw_KeepsTheSelection_AndReportsItOnce()
        {
            AbilityDefinition ability = NewAbility();
            ability.AddNode(NodeTypes.Damage, null, "damage");
            ability.AddNode(NodeTypes.Constant, null, "c");
            AbilityGraphView view = new AbilityGraphView(new AbilityGraphDocument(ability));
            view.AddToSelection(view.NodeViews["damage"]);
            int raised = 0;
            view.SelectionChanged += () => raised++;

            view.Rebuild();

            CollectionAssert.AreEqual(new[] { "damage" }, view.SelectedNodeIds);
            Assert.AreEqual(1, raised);
        }

        // ----- Editing through the real window -----

        [UnityTest]
        public IEnumerator SelectingANode_ShowsItsSettingsInTheWindow()
        {
            AbilityDefinition ability = NewAsset("Select");
            string id = ability.AddNode(NodeTypes.EntitiesInRadius, null, "area");

            yield return OpenAndSelect(ability, id, window =>
            {
                Assert.AreEqual(4, window.Inspector.Fields.Count(), "Radius and three flags.");
                Assert.IsNotNull(window.Inspector.Query<FloatField>().First());
                Assert.AreEqual(3, window.Inspector.Query<Toggle>().ToList().Count);
            });
        }

        [UnityTest]
        public IEnumerator EditingANumber_ChangesTheAsset_CanBeUndone_AndRecompiles()
        {
            AbilityDefinition ability = NewAsset("Number");
            string id = ability.AddNode(NodeTypes.Constant, new ConstantConfig { Value = 1f }, "amount");
            CompileResult before = ability.Compile();

            yield return OpenAndSelect(ability, id, window =>
            {
                FloatField field = window.Inspector.Query<FloatField>().First();
                Undo.IncrementCurrentGroup();
                field.value = 42f;
                Undo.IncrementCurrentGroup();
            });

            Assert.AreEqual(42f, ((ConstantConfig)Node(ability, id).Config).Value);
            Assert.AreNotSame(before, ability.Compile(), "A settings change makes the ability compile again.");
            Assert.IsTrue(EditorUtility.IsDirty(ability));

            Undo.PerformUndo();
            Assert.AreEqual(1f, ((ConstantConfig)Node(ability, id).Config).Value);
        }

        [UnityTest]
        public IEnumerator EditingAnEnumeration_ChangesTheAsset()
        {
            AbilityDefinition ability = NewAsset("Enum");
            string id = ability.AddNode(NodeTypes.Damage, null, "damage");

            yield return OpenAndSelect(ability, id, window =>
            {
                EnumFlagsField field = window.Inspector.Query<EnumFlagsField>().First();
                field.value = DamageProvenance.Proc;
            });

            Assert.AreEqual(DamageProvenance.Proc, ((DamageConfig)Node(ability, id).Config).Provenance);
        }

        [UnityTest]
        public IEnumerator EditingARangedNumber_ChangesTheAsset()
        {
            AbilityDefinition ability = NewAsset("Range");
            string id = ability.AddNode(NodeTypes.Chance, null, "chance");

            yield return OpenAndSelect(ability, id, window =>
            {
                Slider slider = window.Inspector.Query<Slider>().First();
                slider.value = 0.25f;
            });

            Assert.AreEqual(0.25f, ((ChanceConfig)Node(ability, id).Config).Probability, 1e-5f);
        }

        [UnityTest]
        public IEnumerator ChoosingAPrefab_ChangesTheAsset()
        {
            GameObject prefab = ((LaunchProjectileConfig)AssetDatabase.LoadAssetAtPath<AbilityDefinition>(Firebolt).Nodes
                .Single(n => n.TypeId == NodeTypes.LaunchProjectile).Config).Projectile;
            Assert.IsNotNull(prefab);
            AbilityDefinition ability = NewAsset("Prefab");
            string id = ability.AddNode(NodeTypes.LaunchProjectile, null, "launch");

            yield return OpenAndSelect(ability, id, window =>
            {
                ObjectField field = window.Inspector.Query<ObjectField>().First();
                field.value = prefab;
            });

            Assert.AreEqual(prefab, ((LaunchProjectileConfig)Node(ability, id).Config).Projectile);
        }

        [UnityTest]
        public IEnumerator PickingAnAttribute_FillsInItsKey_AndTypingOneWorksToo()
        {
            AbilityDefinition ability = NewAsset("Attribute");
            string id = ability.AddNode(NodeTypes.Attribute, null, "attr");

            yield return OpenAndSelect(ability, id, window =>
            {
                ToolbarMenu menu = window.Inspector.Query<ToolbarMenu>().First();
                DropdownMenuAction pick = menu.menu.MenuItems().OfType<DropdownMenuAction>().First(a => a.name == "intelligence");
                pick.Execute();
            });

            Assert.AreEqual("simplerpg.attr.intelligence", ((AttributeConfig)Node(ability, id).Config).AttributeKey);

            AbilityGraphWindow open = Resources.FindObjectsOfTypeAll<AbilityGraphWindow>().First();
            TextField text = open.Inspector.Query<TextField>().First();
            text.value = "game.attr.magicpower";
            Assert.AreEqual("game.attr.magicpower", ((AttributeConfig)Node(ability, id).Config).AttributeKey);
        }

        [UnityTest]
        public IEnumerator EditingTheAbilitysDetails_ChangesTheAsset()
        {
            AbilityDefinition ability = NewAsset("Details");
            AbilityGraphWindow window = AbilityGraphWindow.Open(ability);
            window.position = new Rect(100, 100, 1000, 600);
            yield return null;
            window.View.ClearSelection();
            yield return WaitFor(() => window.Inspector.Query<FloatField>().ToList().Count > 0 && window.Inspector.NodeId == null, "the ability details");
            yield return null;

            window.Inspector.Query<TextField>().First().value = "Frost Nova";
            window.Inspector.Query<FloatField>().First().value = 7.5f;

            Assert.AreEqual("Frost Nova", ability.AbilityName);
            Assert.AreEqual(7.5f, ability.CooldownSeconds);
        }

        [UnityTest]
        public IEnumerator SettingsEditedInTheWindow_SurviveSavingAndReopening()
        {
            AbilityDefinition ability = NewAsset("Persist");
            string id = ability.AddNode(NodeTypes.Constant, null, "amount");
            string path = AssetDatabase.GetAssetPath(ability);

            yield return OpenAndSelect(ability, id, window =>
            {
                window.Inspector.Query<FloatField>().First().value = 12.5f;
                window.View.Document.Save();
            });

            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            AbilityDefinition reopened = AssetDatabase.LoadAssetAtPath<AbilityDefinition>(path);
            Assert.AreEqual(12.5f, ((ConstantConfig)Node(reopened, id).Config).Value);
            StringAssert.Contains("12.5", System.IO.File.ReadAllText(path));
        }
    }
}
