using NUnit.Framework;
using SimpleRPG.Abilities.Editor;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace SimpleRPG.Abilities.Tests.EditMode
{
    /// <summary>
    /// What the editor tells the author when the ability is wrong, and the copy, paste and duplicate commands.
    /// </summary>
    public class AbilityGraphValidationTests
    {
        private const string Firebolt = "Assets/Abilities/Abilities/Firebolt.asset";

        private readonly List<Object> created = new List<Object>();

        [SetUp]
        public void SetUp()
        {
            AbilityGraphClipboard.Current = null;
        }

        [TearDown]
        public void TearDown()
        {
            foreach (Object o in created)
            {
                if (o != null) Object.DestroyImmediate(o);
            }

            created.Clear();
            AbilityGraphClipboard.Current = null;
        }

        /// <summary>A new ability, which already has an Activate node with the ID "activate".</summary>
        private AbilityDefinition NewAbility()
        {
            AbilityDefinition ability = ScriptableObject.CreateInstance<AbilityDefinition>();
            ability.name = "ValidationTest";
            created.Add(ability);
            return ability;
        }

        /// <summary>Activate, a Damage that still lacks its inputs, and a Constant feeding nothing: an ability with problems.</summary>
        private AbilityDefinition Broken()
        {
            AbilityDefinition ability = NewAbility();
            ability.AddNode(NodeTypes.Damage, null, "damage");
            ability.AddNode(NodeTypes.Constant, null, "orphan");
            ability.Connect("activate", BuiltInNodes.Next, "damage", BuiltInNodes.In);
            return ability;
        }

        // ----- Validation on the canvas -----

        [Test]
        public void AValidAbility_ShowsNoProblems()
        {
            AbilityDefinition ability = AssetDatabase.LoadAssetAtPath<AbilityDefinition>(Firebolt);
            AbilityGraphView view = new AbilityGraphView(new AbilityGraphDocument(ability));
            AbilityProblemsPanel panel = new AbilityProblemsPanel(_ => { });

            panel.Show(view.Validation);

            Assert.IsTrue(view.Validation.Success);
            Assert.IsTrue(view.NodeViews.Values.All(n => n.Problems.Count == 0 && n.BadgeText == string.Empty));
            StringAssert.StartsWith("No problems", panel.Summary);
            Assert.IsEmpty(panel.Rows);
        }

        [Test]
        public void ANodeWithAnError_IsMarkedWithItsMessages_AndTheFaultyPortIsRed()
        {
            AbilityGraphView view = new AbilityGraphView(new AbilityGraphDocument(Broken()));

            AbilityNodeView damage = view.NodeViews["damage"];

            Assert.IsFalse(view.Validation.Success);
            Assert.IsTrue(damage.Problems.Any(p => p.Severity == ValidationSeverity.Error));
            StringAssert.Contains("error", damage.BadgeText);
            foreach (ValidationMessage message in damage.Problems)
            {
                StringAssert.Contains(message.Text, damage.tooltip);
            }

            ValidationMessage portProblem = damage.Problems.First(p => p.PortId != null && p.Severity == ValidationSeverity.Error);
            damage.TryGetPort(portProblem.PortId, out UnityEditor.Experimental.GraphView.Port port);
            Assert.AreEqual(new Color(0.85f, 0.2f, 0.2f), port.portColor);
            Assert.AreEqual(0, view.NodeViews["activate"].Problems.Count(p => p.Severity == ValidationSeverity.Error));
        }

        [Test]
        public void ANodeWithOnlyAWarning_IsMarkedAsAWarning()
        {
            AbilityGraphView view = new AbilityGraphView(new AbilityGraphDocument(Broken()));

            AbilityNodeView orphan = view.NodeViews["orphan"];

            Assert.IsNotEmpty(orphan.Problems, "Nothing uses the Constant's value.");
            Assert.IsTrue(orphan.Problems.All(p => p.Severity == ValidationSeverity.Warning));
            StringAssert.Contains("warning", orphan.BadgeText);
        }

        [Test]
        public void FixingAProblem_ClearsItsMarks()
        {
            AbilityDefinition ability = NewAbility();
            ability.AddNode(NodeTypes.Activate, null, "second");
            AbilityGraphDocument document = new AbilityGraphDocument(ability);
            AbilityGraphView view = new AbilityGraphView(document);
            Assert.IsFalse(view.Validation.Success);
            Assert.IsTrue(view.NodeViews.Values.Any(n => n.Problems.Any(p => p.Severity == ValidationSeverity.Error)), "Two entry nodes is an error on a node.");

            document.RemoveNodes(new[] { "second" });

            Assert.IsTrue(view.Validation.Success);
            Assert.IsTrue(view.NodeViews.Values.All(n => n.Problems.Count == 0 && n.BadgeText == string.Empty));
        }

        [Test]
        public void AChangeMadeBehindTheDocumentsBack_IsNoticedOnTheNextCheck()
        {
            AbilityDefinition ability = NewAbility();
            AbilityGraphView view = new AbilityGraphView(new AbilityGraphDocument(ability));
            CompileResult before = view.Validation;
            CompileResult announced = null;
            view.ValidationChanged += result => announced = result;

            // Like an edit in the settings panel: the asset changes without the document knowing.
            ability.AddNode(NodeTypes.Activate, null, "intruder");
            view.RefreshValidation();

            Assert.AreNotSame(before, view.Validation);
            Assert.AreSame(view.Validation, announced);
        }

        [Test]
        public void CheckingAgainWhenNothingChanged_DoesNotAnnounceAgain()
        {
            AbilityGraphView view = new AbilityGraphView(new AbilityGraphDocument(NewAbility()));
            int announced = 0;
            view.ValidationChanged += _ => announced++;

            view.RefreshValidation();
            view.RefreshValidation();

            Assert.AreEqual(0, announced);
        }

        [Test]
        public void TheProblemsList_HasARowPerProblem_ErrorsFirst()
        {
            AbilityGraphView view = new AbilityGraphView(new AbilityGraphDocument(Broken()));
            AbilityProblemsPanel panel = new AbilityProblemsPanel(_ => { });

            panel.Show(view.Validation);

            Assert.AreEqual(view.Validation.Report.Messages.Count, panel.Rows.Count);
            StringAssert.Contains("error", panel.Summary);
            StringAssert.Contains("will not run", panel.Summary);
            int firstWarning = panel.Rows.ToList().FindIndex(r => ((ValidationMessage)r.userData).Severity == ValidationSeverity.Warning);
            Assert.IsTrue(firstWarning < 0 || panel.Rows.Take(firstWarning).All(r => ((ValidationMessage)r.userData).Severity == ValidationSeverity.Error));
        }

        [Test]
        public void ClickingAProblem_SelectsItsNode()
        {
            AbilityGraphView view = new AbilityGraphView(new AbilityGraphDocument(Broken()));
            AbilityProblemsPanel panel = new AbilityProblemsPanel(id => view.SelectNodes(new[] { id }));
            panel.Show(view.Validation);
            Button row = panel.Rows.First(r => ((ValidationMessage)r.userData).NodeId == "damage");

            panel.Choose((ValidationMessage)row.userData);

            CollectionAssert.AreEqual(new[] { "damage" }, view.SelectedNodeIds);
        }

        [Test]
        public void AProblemWithTheWholeAbility_IsListedButHasNoNodeToSelect()
        {
            AbilityDefinition ability = NewAbility();
            AbilityGraphDocument document = new AbilityGraphDocument(ability);
            document.RemoveNodes(new[] { "activate" });
            AbilityGraphView view = new AbilityGraphView(document);
            string selected = null;
            AbilityProblemsPanel panel = new AbilityProblemsPanel(id => selected = id);

            panel.Show(view.Validation);

            ValidationMessage graphLevel = view.Validation.Report.Messages.Single(m => m.NodeId == null);
            StringAssert.Contains("Activate", graphLevel.Text);
            Button row = panel.Rows.Single(r => ((ValidationMessage)r.userData).NodeId == null);

            panel.Choose((ValidationMessage)row.userData);

            Assert.IsNull(selected);
        }

        [Test]
        public void ARefusedConnection_ExplainsWhy()
        {
            AbilityDefinition ability = NewAbility();
            ability.AddNode(NodeTypes.Damage, null, "damage");
            ability.AddNode(NodeTypes.EventVictim, null, "victim");
            AbilityGraphDocument document = new AbilityGraphDocument(ability);
            AbilityProblemsPanel panel = new AbilityProblemsPanel(_ => { });
            document.Refused += panel.ShowNotice;
            string before = EditorJsonUtility.ToJson(ability);

            bool connected = document.Connect("victim", BuiltInNodes.Value, "damage", BuiltInNodes.Amount, out string reason);

            Assert.IsFalse(connected);
            Assert.AreEqual(reason, panel.Notice);
            StringAssert.Contains("Entity", panel.Notice);
            Assert.AreEqual(before, EditorJsonUtility.ToJson(ability));

            panel.ShowNotice(null);
            Assert.AreEqual(string.Empty, panel.Notice);
        }

        // ----- Copy, paste and duplicate -----

        /// <summary>Activate -> Damage, with a Constant (value 7) feeding the amount.</summary>
        private AbilityDefinition Source()
        {
            AbilityDefinition ability = NewAbility();
            ability.AddNode(NodeTypes.Damage, null, "damage");
            ability.AddNode(NodeTypes.Constant, new ConstantConfig { Value = 7f }, "amount");
            ability.Connect("activate", BuiltInNodes.Next, "damage", BuiltInNodes.In);
            ability.Connect("amount", BuiltInNodes.Value, "damage", BuiltInNodes.Amount);
            return ability;
        }

        [Test]
        public void Pasting_GivesEveryNodeAFreshId_AndKeepsTheConnectionsBetweenThem()
        {
            AbilityDefinition ability = Source();
            AbilityGraphDocument document = new AbilityGraphDocument(ability);

            document.Copy(new[] { "damage", "amount" });
            IReadOnlyList<string> pasted = document.PasteCurrent();

            Assert.AreEqual(2, pasted.Count);
            Assert.IsFalse(pasted.Intersect(new[] { "activate", "damage", "amount" }).Any(), "Pasted nodes never reuse an existing ID.");
            Assert.AreEqual(5, ability.Nodes.Count);
            Assert.AreEqual(5, ability.Nodes.Select(n => n.NodeId).Distinct().Count());

            AbilityNodeRecord pastedDamage = ability.Nodes.Single(n => n.NodeId == pasted[0]);
            AbilityNodeRecord pastedAmount = ability.Nodes.Single(n => n.NodeId == pasted[1]);
            Assert.AreEqual(NodeTypes.Damage, pastedDamage.TypeId);
            Assert.AreEqual(NodeTypes.Constant, pastedAmount.TypeId);
            Assert.IsTrue(ability.Edges.Any(e => e.FromNodeId == pastedAmount.NodeId && e.ToNodeId == pastedDamage.NodeId && e.ToPortId == BuiltInNodes.Amount),
                "The connection between the pasted nodes comes with them.");
        }

        [Test]
        public void Pasting_DoesNotConnectToAnythingOutsideTheCopy_OrChangeTheOriginals()
        {
            AbilityDefinition ability = Source();
            AbilityGraphDocument document = new AbilityGraphDocument(ability);
            document.Copy(new[] { "damage", "amount" });

            IReadOnlyList<string> pasted = document.PasteCurrent();

            Assert.AreEqual(1, ability.Edges.Count(e => e.FromNodeId == "activate"), "The Activate node still leads only to the original.");
            Assert.IsFalse(ability.Edges.Any(e => e.FromNodeId == "activate" && pasted.Contains(e.ToNodeId)));
            Assert.AreEqual(2 + 1, ability.Edges.Count, "The two originals' connections plus the one between the pasted nodes.");
            Assert.AreEqual(1, ability.Edges.Count(e => e.ToNodeId == "damage" && e.ToPortId == BuiltInNodes.Amount));
        }

        [Test]
        public void PastedNodes_AreOffsetFromTheOriginals()
        {
            AbilityDefinition ability = Source();
            AbilityGraphDocument document = new AbilityGraphDocument(ability);
            document.MoveNodes(new Dictionary<string, Vector2> { { "damage", new Vector2(100f, 50f) } });
            document.Copy(new[] { "damage" });

            string pasted = document.PasteCurrent().Single();

            Assert.AreEqual(new Vector2(140f, 90f), ability.Nodes.Single(n => n.NodeId == pasted).EditorPosition);
        }

        [Test]
        public void ThePastedSettings_AreACopyThatCanBeEditedSeparately()
        {
            AbilityDefinition ability = Source();
            AbilityGraphDocument document = new AbilityGraphDocument(ability);
            document.Copy(new[] { "amount" });

            string pasted = document.PasteCurrent().Single();
            ConstantConfig copy = (ConstantConfig)ability.Nodes.Single(n => n.NodeId == pasted).Config;
            ConstantConfig original = (ConstantConfig)ability.Nodes.Single(n => n.NodeId == "amount").Config;

            Assert.AreEqual(7f, copy.Value);
            Assert.AreNotSame(original, copy);
            copy.Value = 99f;
            Assert.AreEqual(7f, original.Value);
        }

        [Test]
        public void ACopiedAssetReference_IsKept()
        {
            GameObject prefab = ((LaunchProjectileConfig)AssetDatabase.LoadAssetAtPath<AbilityDefinition>(Firebolt).Nodes
                .Single(n => n.TypeId == NodeTypes.LaunchProjectile).Config).Projectile;
            AbilityDefinition ability = NewAbility();
            ability.AddNode(NodeTypes.LaunchProjectile, new LaunchProjectileConfig { Projectile = prefab }, "launch");
            AbilityGraphDocument document = new AbilityGraphDocument(ability);

            document.Copy(new[] { "launch" });
            string pasted = document.PasteCurrent().Single();

            Assert.AreEqual(prefab, ((LaunchProjectileConfig)ability.Nodes.Single(n => n.NodeId == pasted).Config).Projectile);
        }

        [Test]
        public void APaste_IsOneUndoStep()
        {
            AbilityDefinition ability = Source();
            AbilityGraphDocument document = new AbilityGraphDocument(ability);
            string before = EditorJsonUtility.ToJson(ability);
            document.Copy(new[] { "damage", "amount" });

            Undo.IncrementCurrentGroup();
            document.PasteCurrent();
            Undo.IncrementCurrentGroup();
            Assert.AreEqual(5, ability.Nodes.Count);

            Undo.PerformUndo();

            Assert.AreEqual(before, EditorJsonUtility.ToJson(ability));
        }

        [Test]
        public void PastingTwice_MakesTwoIndependentCopies()
        {
            AbilityDefinition ability = Source();
            AbilityGraphDocument document = new AbilityGraphDocument(ability);
            document.Copy(new[] { "damage", "amount" });

            IReadOnlyList<string> first = document.PasteCurrent();
            IReadOnlyList<string> second = document.PasteCurrent();

            Assert.IsEmpty(first.Intersect(second));
            Assert.AreEqual(7, ability.Nodes.Count);
        }

        [Test]
        public void CopiedNodes_CanBePastedIntoAnotherAbility()
        {
            AbilityGraphDocument source = new AbilityGraphDocument(Source());
            AbilityDefinition target = NewAbility();
            AbilityGraphDocument destination = new AbilityGraphDocument(target);
            source.Copy(new[] { "damage", "amount" });

            IReadOnlyList<string> pasted = destination.PasteCurrent();

            Assert.AreEqual(3, target.Nodes.Count);
            Assert.AreEqual(1, target.Edges.Count);
            Assert.AreEqual(2, pasted.Count);
        }

        [Test]
        public void CopyingNothing_LeavesTheClipboardAlone()
        {
            AbilityGraphDocument document = new AbilityGraphDocument(Source());
            document.Copy(new[] { "amount" });
            AbilityGraphClipboard kept = AbilityGraphClipboard.Current;

            document.Copy(new string[0]);

            Assert.AreSame(kept, AbilityGraphClipboard.Current);
            Assert.IsEmpty(new AbilityGraphDocument(NewAbility()).PasteCurrent().Skip(1));
        }

        [Test]
        public void PastingWithAnEmptyClipboard_DoesNothing()
        {
            AbilityDefinition ability = NewAbility();
            AbilityGraphDocument document = new AbilityGraphDocument(ability);

            IReadOnlyList<string> pasted = document.PasteCurrent();

            Assert.IsEmpty(pasted);
            Assert.AreEqual(1, ability.Nodes.Count);
        }

        [Test]
        public void Duplicating_CopiesTheNodesWithoutTouchingTheClipboard()
        {
            AbilityDefinition ability = Source();
            AbilityGraphDocument document = new AbilityGraphDocument(ability);
            document.Copy(new[] { "activate" });
            AbilityGraphClipboard kept = AbilityGraphClipboard.Current;

            IReadOnlyList<string> duplicated = document.Duplicate(new[] { "damage", "amount" });

            Assert.AreEqual(2, duplicated.Count);
            Assert.AreSame(kept, AbilityGraphClipboard.Current);
            Assert.AreEqual(5, ability.Nodes.Count);
        }

        [Test]
        public void TheCanvasCommands_CopyPasteAndDuplicate_SelectWhatTheyAdd()
        {
            AbilityDefinition ability = Source();
            AbilityGraphView view = new AbilityGraphView(new AbilityGraphDocument(ability));
            view.SelectNodes(new[] { "damage", "amount" });

            view.CopySelection();
            IReadOnlyList<string> pasted = view.PasteClipboard();

            CollectionAssert.AreEquivalent(pasted, view.SelectedNodeIds);
            Assert.AreEqual(5, view.NodeViews.Count);
            Assert.AreEqual(3, view.DrawnEdges.Count());

            IReadOnlyList<string> duplicated = view.DuplicateSelection();

            CollectionAssert.AreEquivalent(duplicated, view.SelectedNodeIds);
            Assert.AreEqual(7, view.NodeViews.Count);
        }

        [Test]
        public void PastingAnEntryNode_IsReportedByTheCompiler_NotSilentlyAllowedOrBlocked()
        {
            AbilityDefinition ability = NewAbility();
            AbilityGraphDocument document = new AbilityGraphDocument(ability);
            AbilityGraphView view = new AbilityGraphView(document);
            view.SelectNodes(new[] { "activate" });

            view.CopySelection();
            view.PasteClipboard();

            Assert.AreEqual(2, ability.Nodes.Count);
            Assert.IsFalse(view.Validation.Success);
            Assert.IsTrue(view.Validation.Report.Errors.Any(e => e.Text.Contains("entry")));
        }
    }
}
