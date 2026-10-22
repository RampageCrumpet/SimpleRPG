using NUnit.Framework;
using SimpleRPG.Abilities.Editor;
using SimpleRPG.Gameplay;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Experimental.GraphView;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace SimpleRPG.Abilities.Tests.EditMode
{
    /// <summary>
    /// The acceptance bar for the visual ability editor: a new ability is authored from existing primitives using only what a person has in
    /// the editor (the node palette, dragging connections, the settings panel, Save), then compiled, reopened from disk and run in the test
    /// world on the real executor. No C# is written and no serialized asset is edited by hand.
    /// </summary>
    public class AbilityAuthoringAcceptanceTests
    {
        private const string Firebolt = "Assets/Abilitities/Abillities/Firebolt.asset";
        private const string MeleeAttack = "Assets/Abilitities/Abillities/MeleeAttack.asset";
        private const string TempFolder = "Assets/_AuthoringAcceptance";

        [TearDown]
        public void TearDown()
        {
            foreach (AbilityGraphWindow window in Resources.FindObjectsOfTypeAll<AbilityGraphWindow>())
            {
                window.Close();
            }

            if (AssetDatabase.IsValidFolder(TempFolder)) AssetDatabase.DeleteAsset(TempFolder);
        }

        // ----- What a person does in the editor -----

        /// <summary>Creates a new ability asset the way Create > SimpleRPG > Ability does, and opens it in the editor.</summary>
        private static IEnumerator CreateAndOpen(string fileName, Action<AbilityGraphWindow> opened)
        {
            if (!AssetDatabase.IsValidFolder(TempFolder)) AssetDatabase.CreateFolder("Assets", "_AuthoringAcceptance");
            AbilityDefinition ability = ScriptableObject.CreateInstance<AbilityDefinition>();
            AssetDatabase.CreateAsset(ability, TempFolder + "/" + fileName + ".asset");

            AbilityGraphWindow window = AbilityGraphWindow.Open(ability);
            window.position = new Rect(100, 100, 1100, 700);
            yield return null;
            opened(window);
        }

        /// <summary>Picks a node type from the palette by the name shown there, and drops it on the canvas. Returns the new node's ID.</summary>
        private static string AddFromPalette(AbilityGraphWindow window, string typeId, Vector2 position)
        {
            AbilityGraphDocument document = window.View.Document;
            string displayName = document.Registry.All.Single(d => d.TypeId == typeId).DisplayName;
            HashSet<string> before = new HashSet<string>(document.Definition.Nodes.Select(n => n.NodeId));

            NodePalette palette = ScriptableObject.CreateInstance<NodePalette>();
            try
            {
                palette.Initialize(document.Registry, chosen => document.AddNode(chosen, position));
                List<SearchTreeEntry> tree = palette.CreateSearchTree(new SearchWindowContext(Vector2.zero));
                SearchTreeEntry entry = tree.Single(e => !(e is SearchTreeGroupEntry) && e.content.text == displayName);
                Assert.IsTrue(palette.OnSelectEntry(entry, new SearchWindowContext(Vector2.zero)));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(palette);
            }

            return document.Definition.Nodes.Select(n => n.NodeId).Single(id => !before.Contains(id));
        }

        /// <summary>Drags a connection from an output port to an input port, as the canvas does when the mouse is released over the target.</summary>
        private static void Drag(AbilityGraphWindow window, string fromNode, string fromPort, string toNode, string toPort)
        {
            AbilityGraphView view = window.View;
            Port from = view.NodeViews[fromNode].Ports.Single(p => ((PortDescriptor)p.userData).Id == fromPort);
            Port to = view.NodeViews[toNode].Ports.Single(p => ((PortDescriptor)p.userData).Id == toPort);
            Assert.Contains(to, view.GetCompatiblePorts(from, null), "While dragging, the canvas offers " + toNode + "." + toPort + " as a place to drop.");

            Edge edge = from.ConnectTo(to);
            view.graphViewChanged(new GraphViewChange { edgesToCreate = new List<Edge> { edge } });
            view.Rebuild();
        }

        /// <summary>Selects a node and waits for its settings to appear in the settings panel.</summary>
        private static IEnumerator Select(AbilityGraphWindow window, string nodeId)
        {
            window.View.SelectNodes(new[] { nodeId });
            for (int i = 0; i < 60 && !(window.Inspector.NodeId == nodeId && window.Inspector.Fields.Any() && window.Inspector.Query<VisualElement>().ToList().Count > 5); i++)
            {
                yield return null;
            }

            yield return null;
        }

        private static IEnumerator SelectNothing(AbilityGraphWindow window)
        {
            window.View.ClearSelection();
            for (int i = 0; i < 60 && window.Inspector.NodeId != null; i++) yield return null;
            yield return null;
        }

        private static AbilityDefinition SaveAndReopen(AbilityGraphWindow window, out AbilityGraphWindow reopened)
        {
            AbilityDefinition ability = window.View.Document.Definition;
            string path = AssetDatabase.GetAssetPath(ability);
            window.View.Document.Save();
            Assert.IsFalse(window.View.Document.IsDirty, "Saving writes the asset.");
            window.Close();

            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            AbilityDefinition loaded = AssetDatabase.LoadAssetAtPath<AbilityDefinition>(path);
            reopened = AbilityGraphWindow.Open(loaded);
            return loaded;
        }

        private static GameObject ProjectilePrefab() =>
            ((LaunchProjectileConfig)AssetDatabase.LoadAssetAtPath<AbilityDefinition>(Firebolt).Nodes.Single(n => n.TypeId == NodeTypes.LaunchProjectile).Config).Projectile;

        private static void AssertEditorClean(AbilityGraphWindow window)
        {
            Assert.IsTrue(window.View.Validation.Success, window.View.Validation.Report.ToString());
            StringAssert.StartsWith("No problems", window.Problems.Summary);
        }

        // ----- A projectile spell, from nothing -----

        [UnityTest]
        public IEnumerator ABoltSpell_IsAuthoredSavedReopenedAndRun_WithNoCodeAndNoHandEditing()
        {
            GameObject prefab = ProjectilePrefab();
            AbilityGraphWindow window = null;
            yield return CreateAndOpen("Authored Bolt", w => window = w);
            AbilityGraphDocument document = window.View.Document;
            Assert.AreEqual(1, document.Definition.Nodes.Count, "A new ability starts with its Activate node.");

            // The ability's own details.
            yield return SelectNothing(window);
            window.Inspector.Query<TextField>().First().value = "Authored Bolt";
            window.Inspector.Query<FloatField>().First().value = 2.5f;

            // Nodes from the palette.
            string activate = document.Definition.Nodes.Single().NodeId;
            string launch = AddFromPalette(window, NodeTypes.LaunchProjectile, new Vector2(300, 0));
            string damage = AddFromPalette(window, NodeTypes.Damage, new Vector2(600, 0));
            string victim = AddFromPalette(window, NodeTypes.EventVictim, new Vector2(300, 200));
            string amount = AddFromPalette(window, NodeTypes.Constant, new Vector2(300, 400));
            Assert.IsFalse(window.View.Validation.Success, "A half-built ability does not compile.");

            // Settings.
            yield return Select(window, launch);
            window.Inspector.Query<ObjectField>().First().value = prefab;
            yield return Select(window, amount);
            window.Inspector.Query<FloatField>().First().value = 25f;

            // Connections, dragged.
            Drag(window, activate, BuiltInNodes.Next, launch, BuiltInNodes.In);
            Drag(window, launch, BuiltInNodes.OnImpact, damage, BuiltInNodes.In);
            Drag(window, victim, BuiltInNodes.Value, damage, BuiltInNodes.Target);
            Drag(window, amount, BuiltInNodes.Value, damage, BuiltInNodes.Amount);

            // Valid, with nothing hand-edited.
            window.View.RefreshValidation();
            AssertEditorClean(window);

            // Saved, closed, and reopened from disk.
            string before = EditorJsonUtility.ToJson(document.Definition);
            AbilityDefinition reopened = SaveAndReopen(window, out AbilityGraphWindow second);
            yield return null;
            Assert.AreEqual(before, EditorJsonUtility.ToJson(reopened), "What was saved is exactly what was authored.");
            Assert.AreEqual("Authored Bolt", reopened.AbilityName);
            Assert.AreEqual(2.5f, reopened.CooldownSeconds);
            Assert.AreEqual(5, second.View.NodeViews.Count);
            Assert.AreEqual(4, second.View.DrawnEdges.Count());
            AssertEditorClean(second);
            Assert.IsTrue(reopened.Compile().Success);

            // Run on the real executor.
            TestWorldRun run = second.RunInTestWorld();

            Assert.IsTrue(run.Ran);
            Assert.IsTrue(run.Completed);
            Assert.AreEqual(25, run.HealthLost["Target 1"], "The authored 25 damage landed.");
            Assert.AreEqual(0, run.HealthLost["Caster"]);
            Assert.AreEqual(1, run.ProjectilesLaunched);
            Assert.IsEmpty(run.Problems);
            Assert.IsTrue(new[] { activate, launch, damage }.All(id => second.View.NodeViews[id].Visited), "The canvas shows the nodes that ran.");
            StringAssert.Contains("Target 1 lost 25 health", second.Problems.Notice);
        }

        // ----- A melee attack, matching the shipped one -----

        [UnityTest]
        public IEnumerator AMeleeAttack_AuthoredFromScratch_DoesWhatTheShippedOneDoes()
        {
            AbilityGraphWindow window = null;
            yield return CreateAndOpen("Authored Cleave", w => window = w);
            AbilityGraphDocument document = window.View.Document;
            string activate = document.Definition.Nodes.Single().NodeId;

            string swing = AddFromPalette(window, NodeTypes.MeleeSwing, new Vector2(300, 0));
            string damage = AddFromPalette(window, NodeTypes.Damage, new Vector2(900, 0));
            string victim = AddFromPalette(window, NodeTypes.EventVictim, new Vector2(300, 200));
            string weapon = AddFromPalette(window, NodeTypes.EventWeaponDamage, new Vector2(300, 400));
            string combo = AddFromPalette(window, NodeTypes.EventComboScalar, new Vector2(300, 600));
            string multiply = AddFromPalette(window, NodeTypes.Multiply, new Vector2(600, 500));

            yield return Select(window, damage);
            window.Inspector.Query<EnumFlagsField>().First().value = DamageProvenance.DirectWeaponHit;

            Drag(window, activate, BuiltInNodes.Next, swing, BuiltInNodes.In);
            Drag(window, swing, BuiltInNodes.OnHit, damage, BuiltInNodes.In);
            Drag(window, victim, BuiltInNodes.Value, damage, BuiltInNodes.Target);
            Drag(window, weapon, BuiltInNodes.Value, multiply, BuiltInNodes.A);
            Drag(window, combo, BuiltInNodes.Value, multiply, BuiltInNodes.B);
            Drag(window, multiply, BuiltInNodes.Result, damage, BuiltInNodes.Amount);
            window.View.RefreshValidation();
            AssertEditorClean(window);

            AbilityDefinition reopened = SaveAndReopen(window, out AbilityGraphWindow second);
            yield return null;
            Assert.IsTrue(reopened.Compile().Success);

            AbilityTestWorld authoredWorld = new AbilityTestWorld { WeaponDamage = 12f, AttackNumber = 2, ComboScalar = 1.5f };
            AbilityTestWorld shippedWorld = new AbilityTestWorld { WeaponDamage = 12f, AttackNumber = 2, ComboScalar = 1.5f };
            TestWorldRun authored = authoredWorld.Run(reopened);
            TestWorldRun shipped = shippedWorld.Run(AssetDatabase.LoadAssetAtPath<AbilityDefinition>(MeleeAttack));

            Assert.AreEqual(18, authored.HealthLost["Target 1"], "12 x 1.5");
            CollectionAssert.AreEqual(shipped.HealthLost, authored.HealthLost, "The authored attack hurts exactly like the shipped MeleeAttack.");
            Assert.AreEqual(1, authored.SwingsStarted);
        }

        // ----- An area spell: sets, branches, and a different projectile result -----

        [UnityTest]
        public IEnumerator AnExplosion_AuthoredWithAnAreaAndAForEach_HurtsEveryoneInRange()
        {
            GameObject prefab = ProjectilePrefab();
            AbilityGraphWindow window = null;
            yield return CreateAndOpen("Authored Blast", w => window = w);
            AbilityGraphDocument document = window.View.Document;
            string activate = document.Definition.Nodes.Single().NodeId;

            string launch = AddFromPalette(window, NodeTypes.LaunchProjectile, new Vector2(300, 0));
            string area = AddFromPalette(window, NodeTypes.EntitiesInRadius, new Vector2(300, 200));
            string each = AddFromPalette(window, NodeTypes.ForEachTarget, new Vector2(600, 0));
            string current = AddFromPalette(window, NodeTypes.CurrentTarget, new Vector2(600, 200));
            string amount = AddFromPalette(window, NodeTypes.Constant, new Vector2(600, 400));
            string damage = AddFromPalette(window, NodeTypes.Damage, new Vector2(900, 0));

            yield return Select(window, launch);
            window.Inspector.Query<ObjectField>().First().value = prefab;
            yield return Select(window, area);
            window.Inspector.Query<FloatField>().First().value = 4f;
            yield return Select(window, amount);
            window.Inspector.Query<FloatField>().First().value = 15f;

            Drag(window, activate, BuiltInNodes.Next, launch, BuiltInNodes.In);
            Drag(window, launch, BuiltInNodes.OnImpact, each, BuiltInNodes.In);
            Drag(window, area, BuiltInNodes.Value, each, BuiltInNodes.Targets);
            Drag(window, each, BuiltInNodes.Each, damage, BuiltInNodes.In);
            Drag(window, current, BuiltInNodes.Value, damage, BuiltInNodes.Target);
            Drag(window, amount, BuiltInNodes.Value, damage, BuiltInNodes.Amount);
            window.View.RefreshValidation();
            AssertEditorClean(window);

            AbilityDefinition reopened = SaveAndReopen(window, out AbilityGraphWindow second);
            yield return null;
            AbilityTestWorld world = new AbilityTestWorld(targetCount: 3);

            TestWorldRun run = world.Run(reopened);

            Assert.IsTrue(run.Ran);
            Assert.AreEqual(15, run.HealthLost["Target 1"]);
            Assert.AreEqual(15, run.HealthLost["Target 2"]);
            Assert.AreEqual(15, run.HealthLost["Target 3"]);
            Assert.AreEqual(0, run.HealthLost["Caster"], "The caster is left out by the area's default setting.");
        }

        // ----- Mistakes are caught before the ability runs -----

        [UnityTest]
        public IEnumerator AMissingConnection_IsShownAndStopsTheRun_UntilItIsFixed()
        {
            GameObject prefab = ProjectilePrefab();
            AbilityGraphWindow window = null;
            yield return CreateAndOpen("Authored Mistake", w => window = w);
            AbilityGraphDocument document = window.View.Document;
            string activate = document.Definition.Nodes.Single().NodeId;
            string launch = AddFromPalette(window, NodeTypes.LaunchProjectile, new Vector2(300, 0));
            string damage = AddFromPalette(window, NodeTypes.Damage, new Vector2(600, 0));
            string victim = AddFromPalette(window, NodeTypes.EventVictim, new Vector2(300, 200));
            string amount = AddFromPalette(window, NodeTypes.Constant, new Vector2(300, 400));
            yield return Select(window, launch);
            window.Inspector.Query<ObjectField>().First().value = prefab;
            yield return Select(window, amount);
            window.Inspector.Query<FloatField>().First().value = 40f;
            Drag(window, activate, BuiltInNodes.Next, launch, BuiltInNodes.In);
            Drag(window, launch, BuiltInNodes.OnImpact, damage, BuiltInNodes.In);
            Drag(window, victim, BuiltInNodes.Value, damage, BuiltInNodes.Target);

            // The amount was never connected.
            window.View.RefreshValidation();
            Assert.IsFalse(window.View.Validation.Success);
            AbilityNodeView damageView = window.View.NodeViews[damage];
            Assert.IsTrue(damageView.Problems.Any(p => p.PortId == BuiltInNodes.Amount && p.Severity == ValidationSeverity.Error), "The error is on the Amount port of the Damage node.");
            StringAssert.Contains("error", window.Problems.Summary);

            TestWorldRun refused = window.RunInTestWorld();
            Assert.IsFalse(refused.Ran);
            StringAssert.Contains("1 error to fix first", refused.Summary);

            // Fix it the way a person would, then run again.
            Drag(window, amount, BuiltInNodes.Value, damage, BuiltInNodes.Amount);
            window.View.RefreshValidation();
            AssertEditorClean(window);
            TestWorldRun fixedRun = window.RunInTestWorld();

            Assert.IsTrue(fixedRun.Ran);
            Assert.AreEqual(40, fixedRun.HealthLost["Target 1"]);
        }

        // ----- Reuse: copy an authored ability's nodes into another -----

        [UnityTest]
        public IEnumerator NodesAuthoredInOneAbility_CanBeCopiedIntoAnother_AndStillRun()
        {
            GameObject prefab = ProjectilePrefab();
            AbilityGraphWindow first = null;
            yield return CreateAndOpen("Authored Source", w => first = w);
            string launch = AddFromPalette(first, NodeTypes.LaunchProjectile, new Vector2(300, 0));
            string damage = AddFromPalette(first, NodeTypes.Damage, new Vector2(600, 0));
            string victim = AddFromPalette(first, NodeTypes.EventVictim, new Vector2(300, 200));
            string amount = AddFromPalette(first, NodeTypes.Constant, new Vector2(300, 400));
            yield return Select(first, launch);
            first.Inspector.Query<ObjectField>().First().value = prefab;
            yield return Select(first, amount);
            first.Inspector.Query<FloatField>().First().value = 33f;
            Drag(first, launch, BuiltInNodes.OnImpact, damage, BuiltInNodes.In);
            Drag(first, victim, BuiltInNodes.Value, damage, BuiltInNodes.Target);
            Drag(first, amount, BuiltInNodes.Value, damage, BuiltInNodes.Amount);

            first.View.SelectNodes(new[] { launch, damage, victim, amount });
            first.View.CopySelection();

            AbilityGraphWindow second = null;
            yield return CreateAndOpen("Authored Target", w => second = w);
            string entry = second.View.Document.Definition.Nodes.Single().NodeId;
            IReadOnlyList<string> pasted = second.View.PasteClipboard();
            string pastedLaunch = pasted.Select(id => second.View.Document.Definition.Nodes.Single(n => n.NodeId == id)).Single(n => n.TypeId == NodeTypes.LaunchProjectile).NodeId;
            Drag(second, entry, BuiltInNodes.Next, pastedLaunch, BuiltInNodes.In);
            second.View.RefreshValidation();
            AssertEditorClean(second);

            TestWorldRun run = second.RunInTestWorld();

            Assert.IsTrue(run.Ran);
            Assert.AreEqual(33, run.HealthLost["Target 1"], "The copy carried its settings, including the projectile and the amount.");
        }

        // ----- The result is an ordinary project asset -----

        [UnityTest]
        public IEnumerator AnAuthoredAbility_IsAnOrdinaryAsset_WithAStablePermanentId()
        {
            AbilityGraphWindow window = null;
            yield return CreateAndOpen("Authored Asset", w => window = w);
            AbilityDefinition ability = window.View.Document.Definition;

            AbilityDefinition reopened = SaveAndReopen(window, out _);

            Assert.IsFalse(reopened.Id.IsNone, "Saving gives the ability a permanent ID, like every gameplay definition.");
            DefinitionId id = reopened.Id;
            AssetDatabase.ImportAsset(AssetDatabase.GetAssetPath(reopened), ImportAssetOptions.ForceUpdate);
            Assert.AreEqual(id, AssetDatabase.LoadAssetAtPath<AbilityDefinition>(AssetDatabase.GetAssetPath(reopened)).Id, "The ID survives a reimport.");
            Assert.AreSame(ability, reopened);
        }
    }
}
