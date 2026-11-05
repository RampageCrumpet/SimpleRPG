using NUnit.Framework;
using SimpleRPG.Abilities;
using SimpleRPG.EffectTools;
using SimpleRPG.InventorySystem;
using System.Collections.Generic;
using UnityEngine;

namespace SimpleRPG.Tests.EditMode
{
    /// <summary>Thorns, Rampage and Frostbite, built in memory as the tool builds them: they compile, are authored correctly and use the triggers they claim.</summary>
    public class ReactionAffixSetupTests
    {
        private readonly List<Object> made = new List<Object>();

        [TearDown]
        public void TearDown()
        {
            foreach (Object o in made)
            {
                if (o != null) Object.DestroyImmediate(o);
            }

            made.Clear();
        }

        [Test]
        public void EveryAbilityCompiles_AndEveryEffectIsSound()
        {
            var items = ReactionAffixSetup.BuildAll(made);

            foreach (Object o in made)
            {
                if (o is AbilityDefinition ability)
                {
                    CompileResult result = ability.Compile();
                    Assert.IsTrue(result.Success, ability.name + ": " + result.Report);
                }

                if (o is TriggeredEffect effect) Assert.IsNull(effect.Problem(), effect.name);
            }

            Assert.AreEqual(3, items.Count);
        }

        [Test]
        public void EachUsesItsTriggerAndTarget()
        {
            var items = ReactionAffixSetup.BuildAll(made);

            TriggeredEffect thorns = items.Find(i => i.family == "thorns").effect;
            TriggeredEffect rampage = items.Find(i => i.family == "rampage").effect;
            TriggeredEffect frost = items.Find(i => i.family == "frost").effect;

            Assert.AreEqual(EffectTrigger.OnTakeDamage, thorns.Trigger);
            Assert.AreEqual(EffectTarget.Other, thorns.Target, "Whoever struck the wearer.");
            Assert.AreEqual(EffectTrigger.OnKill, rampage.Trigger);
            Assert.AreEqual(EffectTarget.Owner, rampage.Target, "The wearer gets the bonus.");
            Assert.AreEqual(EffectTrigger.OnHit, frost.Trigger);
            Assert.AreEqual(EffectTarget.Other, frost.Target, "What the wearer hit.");
        }

        [Test]
        public void TheStackingStatusesChangeStatsTheGameReads()
        {
            ReactionAffixSetup.BuildAll(made);

            foreach (Object o in made)
            {
                if (o is StatusDefinition status)
                {
                    Assert.AreEqual(1, status.StatModifiers.Count, status.name);
                    Assert.IsTrue(SimpleRPG.Gameplay.AttributeConsumers.IsRead(status.StatModifiers[0].attributeKey), status.name + " changes something nothing reads.");
                    Assert.Greater(status.MaxStacks, 1, "They stack.");
                }
            }
        }
    }
}
