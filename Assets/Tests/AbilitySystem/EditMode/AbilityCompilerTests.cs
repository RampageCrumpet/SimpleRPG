using NUnit.Framework;
using SimpleRPG.Gameplay;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace SimpleRPG.Abilities.Tests.EditMode
{
    public class AbilityCompilerTests
    {
        private readonly List<Object> created = new List<Object>();

        private GameObject prefab;

        [SetUp]
        public void SetUp()
        {
            prefab = new GameObject("ProjectilePrefab");
            created.Add(prefab);
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

        private AbilityDefinition Track(AbilityDefinition definition)
        {
            created.Add(definition);
            return definition;
        }

        private static ValidationMessage[] Errors(CompileResult result) => result.Report.Errors.ToArray();

        private static void AssertError(CompileResult result, string nodeId, string portId, string textFragment)
        {
            ValidationMessage match = result.Report.Errors.FirstOrDefault(m => m.NodeId == nodeId && m.PortId == portId && m.Text.Contains(textFragment));
            Assert.IsNotNull(match, "Expected an error on node '" + nodeId + "' port '" + portId + "' containing '" + textFragment + "'. Found:\n" + result.Report);
        }

        [Test]
        public void FireboltGraph_Compiles_WithNoErrors()
        {
            AbilityDefinition ability = Track(AbilityBuilder.Firebolt(prefab));

            CompileResult result = ability.Compile();

            Assert.IsTrue(result.Success, result.Report.ToString());
            Assert.IsFalse(result.Report.HasErrors);
            Assert.AreEqual(3, result.Ability.Operations.Length, "Activate, Launch Projectile and Damage are the operations.");
        }

        [Test]
        public void CompiledOperations_KnowTheirGraphNodes()
        {
            CompiledAbility compiled = Track(AbilityBuilder.Firebolt(prefab)).Compile().Ability;

            CollectionAssert.AreEquivalent(new[] { "activate", "launch", "damage" }, compiled.Operations.Select(o => o.NodeId).ToArray());
            Assert.AreEqual("activate", compiled.Operations[compiled.EntryOperation].NodeId);
        }

        [Test]
        public void AStarterAbility_IsValid()
        {
            AbilityDefinition ability = Track(AbilityBuilder.Empty("Starter"));

            Assert.IsTrue(ability.Compile().Success);
        }

        [Test]
        public void Messages_NameTheAsset()
        {
            AbilityDefinition ability = Track(AbilityBuilder.Empty("Named Ability"));
            ability.AddNode("simplerpg.nonexistent.v1", null, "mystery");

            CompileResult result = ability.Compile();

            Assert.IsFalse(result.Success);
            StringAssert.Contains("Named Ability", result.Report.Errors.First().ToString());
            StringAssert.Contains("mystery", result.Report.Errors.First().ToString());
        }

        [Test]
        public void UnknownNodeType_IsRejected()
        {
            AbilityDefinition ability = Track(AbilityBuilder.Empty("A"));
            ability.AddNode("simplerpg.nonexistent.v1", null, "mystery");

            AssertError(ability.Compile(), "mystery", null, "Unknown node type");
        }

        [Test]
        public void DuplicateNodeIds_AreRejected()
        {
            List<AbilityNodeRecord> nodes = new List<AbilityNodeRecord>
            {
                new AbilityNodeRecord { NodeId = "x", TypeId = NodeTypes.Activate, Config = new ActivateConfig() },
                new AbilityNodeRecord { NodeId = "x", TypeId = NodeTypes.Constant, Config = new ConstantConfig() },
            };

            CompileResult result = AbilityCompiler.Compile("Dup", default, nodes, new List<AbilityEdge>(), AbilityNodeRegistry.Default);

            AssertError(result, "x", null, "share the ID");
        }

        [Test]
        public void MissingNodeId_IsRejected()
        {
            List<AbilityNodeRecord> nodes = new List<AbilityNodeRecord> { new AbilityNodeRecord { NodeId = "", TypeId = NodeTypes.Activate, Config = new ActivateConfig() } };

            CompileResult result = AbilityCompiler.Compile("NoId", default, nodes, new List<AbilityEdge>(), AbilityNodeRegistry.Default);

            Assert.IsTrue(result.Report.Errors.Any(e => e.Text.Contains("no node ID")));
        }

        [Test]
        public void MissingOrWrongConfig_IsRejected()
        {
            AbilityDefinition ability = Track(AbilityBuilder.Empty("A"));
            ability.AddNode(NodeTypes.Constant, new DamageConfig(), "wrong");

            AssertError(ability.Compile(), "wrong", null, "settings");
        }

        [Test]
        public void DanglingEdge_IsRejected()
        {
            AbilityDefinition ability = Track(AbilityBuilder.Empty("A"));
            ability.Connect("activate", BuiltInNodes.Next, "ghost", BuiltInNodes.In);

            AssertError(ability.Compile(), "ghost", BuiltInNodes.In, "does not exist");
        }

        [Test]
        public void UnknownPort_IsRejected()
        {
            AbilityDefinition ability = Track(AbilityBuilder.Empty("A"));
            string damage = ability.AddNode(NodeTypes.Damage, null, "damage");
            ability.Connect("activate", "Bogus", damage, BuiltInNodes.In);

            AssertError(ability.Compile(), "activate", "Bogus", "no port");
        }

        [Test]
        public void ConnectingFloatToEntity_IsRejected_WithThePortNamed()
        {
            AbilityDefinition ability = Track(AbilityBuilder.Empty("A"));
            string damage = ability.AddNode(NodeTypes.Damage, null, "damage");
            string constant = ability.AddNode(NodeTypes.Constant, null, "number");
            ability.Connect("activate", BuiltInNodes.Next, damage, BuiltInNodes.In);
            ability.Connect(constant, BuiltInNodes.Value, damage, BuiltInNodes.Target);

            AssertError(ability.Compile(), damage, BuiltInNodes.Target, "no implicit conversion");
        }

        [Test]
        public void ConnectingFlowToData_IsRejected()
        {
            AbilityDefinition ability = Track(AbilityBuilder.Empty("A"));
            string damage = ability.AddNode(NodeTypes.Damage, null, "damage");
            ability.Connect("activate", BuiltInNodes.Next, damage, BuiltInNodes.Amount);

            AssertError(ability.Compile(), damage, BuiltInNodes.Amount, "Cannot connect");
        }

        [Test]
        public void ConnectingAnInputToAnInput_IsRejected()
        {
            AbilityDefinition ability = Track(AbilityBuilder.Empty("A"));
            string damage = ability.AddNode(NodeTypes.Damage, null, "damage");
            string other = ability.AddNode(NodeTypes.Damage, null, "other");
            ability.Connect(damage, BuiltInNodes.In, other, BuiltInNodes.In);

            AssertError(ability.Compile(), damage, BuiltInNodes.In, "output port");
        }

        [Test]
        public void MissingRequiredInput_IsRejected()
        {
            AbilityDefinition ability = Track(AbilityBuilder.Empty("A"));
            string damage = ability.AddNode(NodeTypes.Damage, null, "damage");
            ability.Connect("activate", BuiltInNodes.Next, damage, BuiltInNodes.In);

            CompileResult result = ability.Compile();

            AssertError(result, damage, BuiltInNodes.Target, "not connected");
            AssertError(result, damage, BuiltInNodes.Amount, "not connected");
        }

        [Test]
        public void TwoConnectionsIntoOneDataInput_AreRejected()
        {
            AbilityDefinition ability = Track(AbilityBuilder.Empty("A"));
            string add = ability.AddNode(NodeTypes.Add, null, "add");
            string one = ability.AddNode(NodeTypes.Constant, null, "one");
            string two = ability.AddNode(NodeTypes.Constant, null, "two");
            ability.Connect(one, BuiltInNodes.Value, add, BuiltInNodes.A);
            ability.Connect(two, BuiltInNodes.Value, add, BuiltInNodes.A);

            AssertError(ability.Compile(), add, BuiltInNodes.A, "takes one");
        }

        [Test]
        public void ADataCycle_IsRejected()
        {
            AbilityDefinition ability = Track(AbilityBuilder.Empty("A"));
            string first = ability.AddNode(NodeTypes.Add, null, "first");
            string second = ability.AddNode(NodeTypes.Add, null, "second");
            ability.Connect(first, BuiltInNodes.Result, second, BuiltInNodes.A);
            ability.Connect(second, BuiltInNodes.Result, first, BuiltInNodes.A);

            Assert.IsTrue(ability.Compile().Report.Errors.Any(e => e.Text.Contains("cycle")), ability.Compile().Report.ToString());
        }

        [Test]
        public void AFlowCycle_IsRejected()
        {
            AbilityDefinition ability = Track(AbilityBuilder.Empty("A"));
            string damage = ability.AddNode(NodeTypes.Damage, null, "damage");
            ability.Connect("activate", BuiltInNodes.Next, damage, BuiltInNodes.In);
            ability.Connect(damage, BuiltInNodes.Next, damage, BuiltInNodes.In);

            Assert.IsTrue(ability.Compile().Report.Errors.Any(e => e.Text.Contains("cycle")), ability.Compile().Report.ToString());
        }

        [Test]
        public void AFlowOutput_WithTwoConnections_IsRejected()
        {
            AbilityDefinition ability = Track(AbilityBuilder.Empty("A"));
            string a = ability.AddNode(NodeTypes.Damage, null, "a");
            string b = ability.AddNode(NodeTypes.Damage, null, "b");
            ability.Connect("activate", BuiltInNodes.Next, a, BuiltInNodes.In);
            ability.Connect("activate", BuiltInNodes.Next, b, BuiltInNodes.In);

            AssertError(ability.Compile(), "activate", BuiltInNodes.Next, "leads to one operation");
        }

        [Test]
        public void NoActivateNode_IsRejected()
        {
            CompileResult result = AbilityCompiler.Compile("Headless", default, new List<AbilityNodeRecord>(), new List<AbilityEdge>(), AbilityNodeRegistry.Default);

            Assert.IsTrue(result.Report.Errors.Any(e => e.Text.Contains("no Activate node")));
        }

        [Test]
        public void TwoActivateNodes_AreRejected()
        {
            AbilityDefinition ability = Track(AbilityBuilder.Empty("A"));
            ability.AddNode(NodeTypes.Activate, null, "second");

            Assert.IsTrue(ability.Compile().Report.Errors.Any(e => e.Text.Contains("exactly one")));
        }

        [Test]
        public void EventVictim_UsedWithoutAnImpact_IsRejected()
        {
            AbilityDefinition ability = Track(AbilityBuilder.Empty("A"));
            string damage = ability.AddNode(NodeTypes.Damage, null, "damage");
            string victim = ability.AddNode(NodeTypes.EventVictim, null, "victim");
            string amount = ability.AddNode(NodeTypes.Constant, null, "amount");
            ability.Connect("activate", BuiltInNodes.Next, damage, BuiltInNodes.In);
            ability.Connect(victim, BuiltInNodes.Value, damage, BuiltInNodes.Target);
            ability.Connect(amount, BuiltInNodes.Value, damage, BuiltInNodes.Amount);

            AssertError(ability.Compile(), victim, null, "only available after an impact");
        }

        [Test]
        public void MissingProjectilePrefab_IsRejected()
        {
            AbilityDefinition ability = Track(AbilityBuilder.Firebolt(null));

            AssertError(ability.Compile(), "launch", null, "projectile prefab");
        }

        [Test]
        public void ADataNodeNothingUses_IsOnlyAWarning()
        {
            AbilityDefinition ability = Track(AbilityBuilder.Firebolt(prefab));
            ability.AddNode(NodeTypes.Constant, null, "unused");

            CompileResult result = ability.Compile();

            Assert.IsTrue(result.Success, "A warning must not stop compilation.");
            Assert.IsTrue(result.Report.Warnings.Any(w => w.NodeId == "unused"));
        }

        [Test]
        public void AttributeNode_NeedsAKey_AndNotAConstantKind()
        {
            AbilityDefinition ability = Track(AbilityBuilder.Empty("A"));
            ability.AddNode(NodeTypes.Attribute, new AttributeConfig { Kind = AttributeBindingKind.Constant, AttributeKey = "" }, "attr");

            CompileResult result = ability.Compile();

            Assert.IsTrue(result.Report.Errors.Any(e => e.NodeId == "attr" && e.Text.Contains("attribute key")));
            Assert.IsTrue(result.Report.Errors.Any(e => e.NodeId == "attr" && e.Text.Contains("Constant node")));
        }

        [Test]
        public void ConstantNode_RejectsNonFiniteValues()
        {
            AbilityDefinition ability = Track(AbilityBuilder.Empty("A"));
            ability.AddNode(NodeTypes.Constant, new ConstantConfig { Value = float.NaN }, "bad");

            AssertError(ability.Compile(), "bad", null, "finite");
        }

        [Test]
        public void AGraphWithErrors_NeverProducesACompiledAbility()
        {
            AbilityDefinition ability = Track(AbilityBuilder.Empty("A"));
            ability.AddNode("simplerpg.nonexistent.v1", null, "mystery");

            Assert.IsNull(ability.Compile().Ability);
        }

        [Test]
        public void ChangingTheGraph_InvalidatesTheCompiledResult()
        {
            AbilityDefinition ability = Track(AbilityBuilder.Empty("A"));
            Assert.IsTrue(ability.Compile().Success);

            ability.AddNode("simplerpg.nonexistent.v1", null, "mystery");

            Assert.IsFalse(ability.Compile().Success);
        }

        [Test]
        public void AddNode_RejectsADuplicateId_AndGeneratesUniqueOnes()
        {
            AbilityDefinition ability = Track(AbilityBuilder.Empty("A"));

            Assert.Throws<System.ArgumentException>(() => ability.AddNode(NodeTypes.Constant, null, "activate"));
            string first = ability.AddNode(NodeTypes.Constant);
            string second = ability.AddNode(NodeTypes.Constant);

            Assert.AreNotEqual(first, second);
        }

        [Test]
        public void ANewNodeType_AppearsInTheRegistrySearch_WithoutEditingAnythingElse()
        {
            AbilityNodeRegistry registry = new AbilityNodeRegistry();
            BuiltInNodes.RegisterAll(registry);
            registry.Register(new NodeDescriptor<ConstantConfig>(
                "test.data.pi.v1", "Pi", "Data", "The number pi.",
                new[] { PortDescriptor.DataOut("Value", DataType.Float) },
                (config, context) => new SimpleRPGTestPi()));

            Assert.IsTrue(registry.Search("pi").Any(d => d.TypeId == "test.data.pi.v1"));
            Assert.IsTrue(registry.Search("number pi").Any());
            Assert.IsFalse(AbilityNodeRegistry.Default.Search("pi").Any(d => d.TypeId == "test.data.pi.v1"));
        }

        [Test]
        public void RegisteringATypeTwice_IsRefused()
        {
            AbilityNodeRegistry registry = new AbilityNodeRegistry();
            BuiltInNodes.RegisterAll(registry);

            Assert.Throws<System.ArgumentException>(() => BuiltInNodes.RegisterAll(registry));
        }

        [Test]
        public void Search_FindsByNameCategoryAndDescription_IgnoringCase()
        {
            AbilityNodeRegistry registry = AbilityNodeRegistry.Default;

            Assert.IsTrue(registry.Search("DAMAGE").Any(d => d.TypeId == NodeTypes.Damage));
            Assert.IsTrue(registry.Search("delivery").Any(d => d.TypeId == NodeTypes.LaunchProjectile));
            Assert.IsTrue(registry.Search("combat system").Any(d => d.TypeId == NodeTypes.Damage));
            Assert.AreEqual(registry.All.Count(), registry.Search("").Count());
        }

        private sealed class SimpleRPGTestPi : FloatExpr
        {
            protected override double EvaluateCore(ExecutionContext context, in BranchFrame frame) => System.Math.PI;
        }
    }
}
