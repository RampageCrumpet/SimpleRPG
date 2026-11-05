using NUnit.Framework;
using SimpleRPG.Abilities;
using SimpleRPG.EffectTools;
using SimpleRPG.InventorySystem;
using System.Collections.Generic;
using UnityEngine;

namespace SimpleRPG.Tests.EditMode
{
    /// <summary>
    /// The bleeding effect, built in memory the way the tool builds it: every piece compiles, the chain connects, and the affix is a live one.
    /// </summary>
    public class BleedingSetupTests
    {
        private readonly List<Object> created = new List<Object>();

        [TearDown]
        public void TearDown()
        {
            foreach (Object o in created)
            {
                if (o != null) Object.DestroyImmediate(o);
            }

            created.Clear();
        }

        private T Track<T>(T made) where T : Object
        {
            created.Add(made);
            return made;
        }

        [Test]
        public void TheTickAbility_Compiles()
        {
            CompileResult result = Track(BleedingSetup.BuildTickAbility()).Compile();

            Assert.IsTrue(result.Success, result.Report.ToString());
        }

        [Test]
        public void TheApplyAbility_Compiles_AndStartsFromATriggeredEffect()
        {
            AbilityDefinition tick = Track(BleedingSetup.BuildTickAbility());
            StatusDefinition status = Track(BleedingSetup.BuildStatus(tick));
            AbilityDefinition apply = Track(BleedingSetup.BuildApplyAbility(status));

            CompileResult result = apply.Compile();

            Assert.IsTrue(result.Success, result.Report.ToString());
            Assert.IsTrue(apply.HasNode(NodeTypes.TriggerEntry));
            Assert.AreSame(tick, status.TickAbility);
            Assert.AreEqual(BleedingSetup.MaxStacks, status.MaxStacks);
        }

        [Test]
        public void TheEffect_IsAuthoredCorrectly_AndItsAffixIsLive()
        {
            AbilityDefinition tick = Track(BleedingSetup.BuildTickAbility());
            StatusDefinition status = Track(BleedingSetup.BuildStatus(tick));
            AbilityDefinition apply = Track(BleedingSetup.BuildApplyAbility(status));
            TriggeredEffect effect = Track(BleedingSetup.BuildEffect(apply));
            AffixDefinition affix = Track(BleedingSetup.BuildAffix(effect));

            Assert.IsNull(effect.Problem());
            Assert.AreEqual(EffectTrigger.OnHit, effect.Trigger);
            Assert.AreEqual(EffectTarget.Other, effect.Target);
            Assert.AreEqual(BleedingSetup.ChanceOnHit, effect.Chance, 1e-6f);
            Assert.IsNull(affix.Problem());
            Assert.IsTrue(affix.IsLive(out string reason), reason);
            Assert.AreEqual(AffixKind.Suffix, affix.Kind);
            CollectionAssert.AreEqual(new[] { effect }, GetEffects(affix));
            StringAssert.Contains("bleeding", effect.Description);
        }

        private static List<TriggeredEffect> GetEffects(AffixDefinition affix)
        {
            var into = new List<TriggeredEffect>();
            affix.AddTriggeredEffects(into);
            return into;
        }
    }
}
