using NUnit.Framework;
using SimpleRPG.Abilities.Editor;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Experimental.GraphView;
using UnityEngine;

namespace SimpleRPG.Abilities.Tests.EditMode
{
    /// <summary>
    /// The visual ability editor's foundation: the document operations, the canvas that draws them, and the promise that opening an ability and
    /// saving it again changes nothing.
    /// </summary>
    public class AbilityGraphEditorTests
    {
        private const string Firebolt = "Assets/Abilitities/Abillities/Firebolt.asset";
        private const string MeleeAttack = "Assets/Abilitities/Abillities/MeleeAttack.asset";
        private const string TempFolder = "Assets/_AbilityGraphEditorTests";

        private readonly List<Object> created = new List<Object>();

        [TearDown]
        public void TearDown()
        {
            foreach (Object o in created)
            {
                if (o != null) Object.DestroyImmediate(o);
            }

            created.Clear();
            if (AssetDatabase.IsValidFolder(TempFolder)) AssetDatabase.DeleteAsset(TempFolder);
        }

        /// <summary>
        /// A new ability as the editor creates it: it already has an Activate node, with the ID "activate".
        /// </summary>
        private AbilityDefinition NewAbility()
        {
            AbilityDefinition ability = ScriptableObject.CreateInstance<AbilityDefinition>();
            ability.name = "TestAbility";
            created.Add(ability);
            return ability;
        }

        /// <summary>
        /// Activate -> Damage, with a Constant feeding the amount: a small valid-looking graph to edit.
        /// </summary>
        private static AbilityGraphDocument Small(AbilityDefinition ability)
        {
            Assert.IsTrue(ability.HasNode(NodeTypes.Activate), "A new ability starts with an Activate node.");
            ability.AddNode(NodeTypes.Damage, null, "damage");
            ability.AddNode(NodeTypes.Constant, null, "amount");
            ability.AddNode(NodeTypes.EventVictim, null, "victim");
            ability.Connect("activate", BuiltInNodes.Next, "damage", BuiltInNodes.In);
            ability.Connect("amount", BuiltInNodes.Value, "damage", BuiltInNodes.Amount);
            return new AbilityGraphDocument(ability);
        }

        private static string Snapshot(AbilityDefinition ability) => EditorJsonUtility.ToJson(ability);

        // A view that is not in a window has no layout yet, so its position is read from the style it was given.
        private static Vector2 PositionOf(AbilityNodeView node) => new Vector2(node.style.left.value.value, node.style.top.value.value);

        // ----- Document operations -----

        [Test]
        public void AddingANode_PutsItWhereAskedWithItsDefaultSettings()
        {
            AbilityGraphDocument document = new AbilityGraphDocument(NewAbility());

            string id = document.AddNode(NodeTypes.Damage, new Vector2(120f, 40f));

            AbilityNodeRecord node = document.Definition.Nodes.Single(n => n.NodeId == id);
            Assert.AreEqual(2, document.Definition.Nodes.Count);
            Assert.AreEqual(NodeTypes.Damage, node.TypeId);
            Assert.AreEqual(new Vector2(120f, 40f), node.EditorPosition);
            Assert.IsNotNull(node.Config, "A new node starts with its type's default settings.");
        }

        [Test]
        public void AddingAnUnregisteredType_IsRefused()
        {
            AbilityGraphDocument document = new AbilityGraphDocument(NewAbility());

            Assert.IsNull(document.AddNode("not.a.node.v1", Vector2.zero));
            Assert.AreEqual(1, document.Definition.Nodes.Count, "Only the Activate node a new ability starts with.");
        }

        [Test]
        public void EveryEditIsUndoable()
        {
            AbilityDefinition ability = NewAbility();
            AbilityGraphDocument document = Small(ability);
            string before = Snapshot(ability);

            Undo.IncrementCurrentGroup();
            document.AddNode(NodeTypes.Multiply, new Vector2(10f, 10f));
            Undo.IncrementCurrentGroup();
            Assert.AreEqual(5, ability.Nodes.Count);

            Undo.PerformUndo();

            Assert.AreEqual(4, ability.Nodes.Count);
            Assert.AreEqual(before, Snapshot(ability));
        }

        [Test]
        public void UndoingAConnection_RestoresTheOldOne()
        {
            AbilityDefinition ability = NewAbility();
            AbilityGraphDocument document = Small(ability);
            ability.AddNode(NodeTypes.Constant, null, "other");
            string before = Snapshot(ability);

            Undo.IncrementCurrentGroup();
            Assert.IsTrue(document.Connect("other", BuiltInNodes.Value, "damage", BuiltInNodes.Amount, out _));
            Undo.IncrementCurrentGroup();
            Assert.AreEqual("other", ability.Edges.Single(e => e.ToPortId == BuiltInNodes.Amount).FromNodeId);

            Undo.PerformUndo();

            Assert.AreEqual(before, Snapshot(ability));
        }

        [Test]
        public void ConnectingCompatiblePorts_AddsTheConnection()
        {
            AbilityDefinition ability = NewAbility();
            AbilityGraphDocument document = Small(ability);
            ability.AddNode(NodeTypes.Damage, null, "second");

            bool connected = document.Connect("damage", BuiltInNodes.Next, "second", BuiltInNodes.In, out string reason);

            Assert.IsTrue(connected, reason);
            Assert.IsTrue(ability.Edges.Any(e => e.FromNodeId == "damage" && e.ToNodeId == "second"));
        }

        [Test]
        public void ConnectingIncompatiblePorts_IsRefusedWithAReason_AndChangesNothing()
        {
            AbilityDefinition ability = NewAbility();
            AbilityGraphDocument document = Small(ability);
            string before = Snapshot(ability);

            bool flowToData = document.Connect("activate", BuiltInNodes.Next, "damage", BuiltInNodes.Amount, out string flowReason);
            bool entityToFloat = document.Connect("victim", BuiltInNodes.Value, "damage", BuiltInNodes.Amount, out string typeReason);
            bool inputToInput = document.Connect("damage", BuiltInNodes.In, "damage", BuiltInNodes.Target, out string directionReason);

            Assert.IsFalse(flowToData);
            Assert.IsFalse(entityToFloat);
            Assert.IsFalse(inputToInput);
            Assert.IsNotEmpty(flowReason);
            Assert.IsNotEmpty(typeReason);
            Assert.IsNotEmpty(directionReason);
            Assert.AreEqual(before, Snapshot(ability));
        }

        [Test]
        public void ConnectingANodeToItself_IsRefused()
        {
            AbilityDefinition ability = NewAbility();
            AbilityGraphDocument document = Small(ability);
            ability.AddNode(NodeTypes.Multiply, null, "m");

            Assert.IsFalse(document.Connect("m", BuiltInNodes.Result, "m", BuiltInNodes.A, out string reason));
            Assert.IsNotEmpty(reason);
        }

        [Test]
        public void ADataInputTakesOneConnection_ANewOneReplacesTheOld()
        {
            AbilityDefinition ability = NewAbility();
            AbilityGraphDocument document = Small(ability);
            ability.AddNode(NodeTypes.Constant, null, "replacement");

            document.Connect("replacement", BuiltInNodes.Value, "damage", BuiltInNodes.Amount, out _);

            AbilityEdge edge = ability.Edges.Single(e => e.ToNodeId == "damage" && e.ToPortId == BuiltInNodes.Amount);
            Assert.AreEqual("replacement", edge.FromNodeId);
        }

        [Test]
        public void AFlowOutputLeadsToOneOperation_ANewOneReplacesTheOld()
        {
            AbilityDefinition ability = NewAbility();
            AbilityGraphDocument document = Small(ability);
            ability.AddNode(NodeTypes.Damage, null, "second");

            document.Connect("activate", BuiltInNodes.Next, "second", BuiltInNodes.In, out _);

            AbilityEdge edge = ability.Edges.Single(e => e.FromNodeId == "activate");
            Assert.AreEqual("second", edge.ToNodeId);
        }

        [Test]
        public void ADataOutputCanFeedManyInputs_AndAFlowInputCanBeReachedFromMany()
        {
            AbilityDefinition ability = NewAbility();
            AbilityGraphDocument document = Small(ability);
            ability.AddNode(NodeTypes.Damage, null, "second");
            ability.AddNode(NodeTypes.Chance, null, "chance");

            document.Connect("amount", BuiltInNodes.Value, "second", BuiltInNodes.Amount, out _);
            document.Connect("chance", BuiltInNodes.Success, "damage", BuiltInNodes.In, out _);

            Assert.AreEqual(2, ability.Edges.Count(e => e.FromNodeId == "amount"));
            Assert.AreEqual(2, ability.Edges.Count(e => e.ToNodeId == "damage" && e.ToPortId == BuiltInNodes.In));
        }

        [Test]
        public void ConnectingTwice_DoesNotDuplicateTheConnection()
        {
            AbilityDefinition ability = NewAbility();
            AbilityGraphDocument document = Small(ability);

            document.Connect("amount", BuiltInNodes.Value, "damage", BuiltInNodes.Amount, out _);

            Assert.AreEqual(2, ability.Edges.Count);
        }

        [Test]
        public void Disconnecting_RemovesOnlyThatConnection()
        {
            AbilityDefinition ability = NewAbility();
            AbilityGraphDocument document = Small(ability);

            Assert.IsTrue(document.Disconnect("amount", BuiltInNodes.Value, "damage", BuiltInNodes.Amount));
            Assert.IsFalse(document.Disconnect("amount", BuiltInNodes.Value, "damage", BuiltInNodes.Amount));

            Assert.AreEqual(1, ability.Edges.Count);
        }

        [Test]
        public void DeletingANode_TakesItsConnectionsWithIt()
        {
            AbilityDefinition ability = NewAbility();
            AbilityGraphDocument document = Small(ability);

            document.RemoveNodes(new[] { "damage" });

            Assert.IsFalse(ability.Nodes.Any(n => n.NodeId == "damage"));
            Assert.AreEqual(0, ability.Edges.Count);
        }

        [Test]
        public void MovingNodes_IsOneUndoStep()
        {
            AbilityDefinition ability = NewAbility();
            AbilityGraphDocument document = Small(ability);
            string before = Snapshot(ability);

            Undo.IncrementCurrentGroup();
            document.MoveNodes(new Dictionary<string, Vector2> { { "activate", new Vector2(5f, 5f) }, { "damage", new Vector2(50f, 5f) } });
            Undo.IncrementCurrentGroup();
            Assert.AreEqual(new Vector2(50f, 5f), ability.Nodes.Single(n => n.NodeId == "damage").EditorPosition);

            Undo.PerformUndo();

            Assert.AreEqual(before, Snapshot(ability));
        }

        [Test]
        public void AGraphThatWasNeverArranged_IsShownLaidOut_WithoutChangingTheAsset()
        {
            AbilityDefinition ability = NewAbility();
            AbilityGraphDocument document = Small(ability);
            string before = Snapshot(ability);

            IReadOnlyDictionary<string, Vector2> shown = document.DisplayPositions();

            Assert.AreEqual(before, Snapshot(ability), "Looking at a graph does not edit it.");
            Assert.AreEqual(ability.Nodes.Count, shown.Values.Distinct().Count(), "No two nodes share a position.");
            Assert.Less(shown["activate"].x, shown["damage"].x, "A node sits to the right of what leads to it.");
            Assert.Less(shown["amount"].x, shown["damage"].x);
        }

        [Test]
        public void AnArrangedGraph_IsShownWhereItWasLeft()
        {
            AbilityDefinition ability = NewAbility();
            AbilityGraphDocument document = Small(ability);
            document.MoveNodes(new Dictionary<string, Vector2> { { "damage", new Vector2(-40f, 12f) } });

            Assert.AreEqual(new Vector2(-40f, 12f), document.DisplayPositions()["damage"]);
        }

        [Test]
        public void AutoLayout_StoresTheLayout()
        {
            AbilityDefinition ability = NewAbility();
            AbilityGraphDocument document = Small(ability);

            document.AutoLayout();

            foreach (KeyValuePair<string, Vector2> pair in AbilityGraphDocument.ComputeLayout(ability))
            {
                Assert.AreEqual(pair.Value, ability.Nodes.Single(n => n.NodeId == pair.Key).EditorPosition);
            }
        }

        [Test]
        public void ALayoutOfACycle_Terminates()
        {
            AbilityDefinition ability = NewAbility();
            ability.AddNode(NodeTypes.Damage, null, "a");
            ability.AddNode(NodeTypes.Damage, null, "b");
            ability.Connect("a", BuiltInNodes.Next, "b", BuiltInNodes.In);
            ability.Connect("b", BuiltInNodes.Next, "a", BuiltInNodes.In);

            Assert.AreEqual(3, AbilityGraphDocument.ComputeLayout(ability).Count);
        }

        // ----- The connection rules agree with the compiler -----

        [Test]
        public void TheCanvasRulesForConnecting_MatchTheCompilersForEveryPairOfPorts()
        {
            List<(IAbilityNodeDescriptor node, PortDescriptor port)> all = new List<(IAbilityNodeDescriptor, PortDescriptor)>();
            foreach (IAbilityNodeDescriptor descriptor in AbilityNodeRegistry.Default.All)
            {
                foreach (PortDescriptor port in descriptor.Ports) all.Add((descriptor, port));
            }

            Assert.Greater(all.Count, 30);
            int disagreements = 0;
            string first = null;
            foreach ((IAbilityNodeDescriptor fromNode, PortDescriptor from) in all)
            {
                foreach ((IAbilityNodeDescriptor toNode, PortDescriptor to) in all)
                {
                    AbilityDefinition ability = NewAbility();
                    ability.AddNode(fromNode.TypeId, null, "from");
                    ability.AddNode(toNode.TypeId, null, "to");
                    ability.Connect("from", from.Id, "to", to.Id);

                    bool compilerRefuses = ability.Compile().Report.Messages.Any(m =>
                        m.Severity == ValidationSeverity.Error && (m.Text.Contains("Cannot connect") || m.Text.Contains("must start at") || m.Text.Contains("must end at")));
                    bool canvasAllows = PortCompatibility.CanConnect(from, to, out _);

                    if (compilerRefuses == canvasAllows)
                    {
                        disagreements++;
                        first = first ?? fromNode.TypeId + "." + from.Id + " -> " + toNode.TypeId + "." + to.Id;
                    }

                    Object.DestroyImmediate(ability);
                }
            }

            Assert.AreEqual(0, disagreements, "The canvas and the compiler disagree about connections, first at " + first);
        }

        // ----- The canvas -----

        [Test]
        public void TheCanvas_DrawsEveryNodeAndConnection_WithTheTypesPorts()
        {
            foreach (string path in new[] { Firebolt, MeleeAttack })
            {
                AbilityDefinition ability = AssetDatabase.LoadAssetAtPath<AbilityDefinition>(path);
                Assert.IsNotNull(ability, path);
                AbilityGraphView view = new AbilityGraphView(new AbilityGraphDocument(ability));

                Assert.AreEqual(ability.Nodes.Count, view.NodeViews.Count, path);
                Assert.AreEqual(ability.Edges.Count, view.DrawnEdges.Count(), path);
                foreach (AbilityNodeRecord record in ability.Nodes)
                {
                    AbilityNodeView node = view.NodeViews[record.NodeId];
                    Assert.IsTrue(AbilityNodeRegistry.Default.TryGet(record.TypeId, out IAbilityNodeDescriptor descriptor));
                    Assert.AreEqual(descriptor.DisplayName, node.title);
                    CollectionAssert.AreEquivalent(descriptor.Ports.Select(p => p.Id), node.Ports.Select(p => ((PortDescriptor)p.userData).Id), record.NodeId);
                }
            }
        }

        [Test]
        public void TheCanvas_ShowsTheShippedGraphsWithoutNodesOnTopOfEachOther()
        {
            foreach (string path in new[] { Firebolt, MeleeAttack })
            {
                AbilityDefinition ability = AssetDatabase.LoadAssetAtPath<AbilityDefinition>(path);
                AbilityGraphView view = new AbilityGraphView(new AbilityGraphDocument(ability));

                int distinct = view.NodeViews.Values.Select(n => PositionOf(n)).Distinct().Count();
                Assert.AreEqual(view.NodeViews.Count, distinct, path);
            }
        }

        [Test]
        public void OpeningAndSavingTheShippedAbilities_ChangesNothing()
        {
            foreach (string path in new[] { Firebolt, MeleeAttack })
            {
                AbilityDefinition ability = AssetDatabase.LoadAssetAtPath<AbilityDefinition>(path);
                string json = Snapshot(ability);
                string report = ability.Compile().Report.ToString();
                bool success = ability.Compile().Success;
                bool wasDirty = EditorUtility.IsDirty(ability);

                AbilityGraphDocument document = new AbilityGraphDocument(ability);
                AbilityGraphView view = new AbilityGraphView(document);
                view.Rebuild();
                view.FrameGraph();
                document.Save();

                Assert.AreEqual(json, Snapshot(ability), path + " was changed by looking at it.");
                Assert.AreEqual(wasDirty, EditorUtility.IsDirty(ability), path);
                Assert.AreEqual(report, ability.Compile().Report.ToString(), path);
                Assert.AreEqual(success, ability.Compile().Success, path);
            }
        }

        [Test]
        public void EditsMadeThroughTheDocument_AreDrawnByTheCanvas()
        {
            AbilityDefinition ability = NewAbility();
            AbilityGraphDocument document = Small(ability);
            AbilityGraphView view = new AbilityGraphView(document);
            Assert.AreEqual(4, view.NodeViews.Count);

            string id = document.AddNode(NodeTypes.Multiply, new Vector2(300f, 100f));
            document.Connect(id, BuiltInNodes.Result, "damage", BuiltInNodes.Amount, out _);

            Assert.AreEqual(5, view.NodeViews.Count);
            Assert.AreEqual(new Vector2(300f, 100f), PositionOf(view.NodeViews[id]));
            Assert.AreEqual(2, view.DrawnEdges.Count(), "The new connection replaced the old one.");
        }

        [Test]
        public void UndoingAnEdit_RedrawsTheCanvas()
        {
            AbilityDefinition ability = NewAbility();
            AbilityGraphDocument document = Small(ability);
            AbilityGraphView view = new AbilityGraphView(document);

            Undo.IncrementCurrentGroup();
            document.AddNode(NodeTypes.Multiply, Vector2.zero);
            Undo.IncrementCurrentGroup();
            Assert.AreEqual(5, view.NodeViews.Count);

            Undo.PerformUndo();

            Assert.AreEqual(4, view.NodeViews.Count);
        }

        [Test]
        public void DraggingFromAPort_OffersOnlyPortsItCouldConnectTo()
        {
            AbilityDefinition ability = NewAbility();
            AbilityGraphView view = new AbilityGraphView(Small(ability));
            Port flowOut = view.NodeViews["activate"].Ports.Single(p => ((PortDescriptor)p.userData).Id == BuiltInNodes.Next);
            Port dataIn = view.NodeViews["damage"].Ports.Single(p => ((PortDescriptor)p.userData).Id == BuiltInNodes.Amount);

            List<Port> fromFlow = view.GetCompatiblePorts(flowOut, null);
            List<Port> toAmount = view.GetCompatiblePorts(dataIn, null);

            Assert.IsNotEmpty(fromFlow);
            Assert.IsTrue(fromFlow.All(p => ((PortDescriptor)p.userData).Kind == PortKind.Flow && ((PortDescriptor)p.userData).Direction == PortDirection.Input));
            Assert.IsNotEmpty(toAmount);
            Assert.IsTrue(toAmount.All(p => ((PortDescriptor)p.userData).DataType == DataType.Float && ((PortDescriptor)p.userData).Direction == PortDirection.Output));
        }

        [Test]
        public void ANodeOfAnUnknownType_IsDrawnSoItCanBeSeenAndDeleted()
        {
            AbilityDefinition ability = NewAbility();
            AbilityGraphDocument document = Small(ability);
            ability.AddNode("old.removed.node.v1", null, "ghost");
            AbilityGraphView view = new AbilityGraphView(document);

            Assert.IsTrue(view.NodeViews.ContainsKey("ghost"));
            Assert.IsTrue(view.NodeViews["ghost"].title.Contains("old.removed.node.v1"));

            document.RemoveNodes(new[] { "ghost" });

            Assert.IsFalse(view.NodeViews.ContainsKey("ghost"));
        }

        [Test]
        public void ThePalette_OffersEveryRegisteredNodeType_ByCategory()
        {
            NodePalette palette = ScriptableObject.CreateInstance<NodePalette>();
            created.Add(palette);
            string chosen = null;
            palette.Initialize(AbilityNodeRegistry.Default, id => chosen = id);

            List<SearchTreeEntry> tree = palette.CreateSearchTree(new SearchWindowContext(Vector2.zero));

            List<SearchTreeEntry> leaves = tree.Where(e => !(e is SearchTreeGroupEntry)).ToList();
            CollectionAssert.AreEquivalent(AbilityNodeRegistry.Default.All.Select(d => d.TypeId), leaves.Select(e => (string)e.userData));
            palette.OnSelectEntry(leaves[0], new SearchWindowContext(Vector2.zero));
            Assert.AreEqual((string)leaves[0].userData, chosen);
        }

        // ----- Save and reopen -----

        [Test]
        public void ANewAbilityBuiltInTheEditor_SurvivesSavingAndReopening()
        {
            Directory.CreateDirectory(TempFolder);
            AssetDatabase.Refresh();
            string path = TempFolder + "/Authored.asset";
            AbilityDefinition ability = ScriptableObject.CreateInstance<AbilityDefinition>();
            AssetDatabase.CreateAsset(ability, path);
            AbilityGraphDocument document = new AbilityGraphDocument(ability);

            string activate = ability.Nodes.Single(n => n.TypeId == NodeTypes.Activate).NodeId;
            document.MoveNodes(new Dictionary<string, Vector2> { { activate, new Vector2(0f, 0f) } });
            string damage = document.AddNode(NodeTypes.Damage, new Vector2(300f, 0f));
            string victim = document.AddNode(NodeTypes.EventVictim, new Vector2(0f, 200f));
            string amount = document.AddNode(NodeTypes.Constant, new Vector2(0f, 400f));
            Assert.IsTrue(document.Connect(activate, BuiltInNodes.Next, damage, BuiltInNodes.In, out _));
            Assert.IsTrue(document.Connect(amount, BuiltInNodes.Value, damage, BuiltInNodes.Amount, out _));
            Assert.IsTrue(document.IsDirty);

            document.Save();
            Assert.IsFalse(document.IsDirty, "Saving writes the asset.");
            string saved = Snapshot(ability);

            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            AbilityDefinition reopened = AssetDatabase.LoadAssetAtPath<AbilityDefinition>(path);
            AbilityGraphDocument second = new AbilityGraphDocument(reopened);
            AbilityGraphView view = new AbilityGraphView(second);

            Assert.AreEqual(saved, Snapshot(reopened));
            Assert.AreEqual(4, view.NodeViews.Count);
            Assert.AreEqual(2, view.DrawnEdges.Count());
            Assert.AreEqual(new Vector2(300f, 0f), PositionOf(view.NodeViews[damage]));
            string file = File.ReadAllText(path);
            StringAssert.Contains(NodeTypes.Damage, file);
            StringAssert.Contains(damage, file);
        }
    }
}
