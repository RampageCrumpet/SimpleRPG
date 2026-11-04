using NUnit.Framework;
using SimpleRPG.Abilities;
using SimpleRPG.Abilities.Integration;
using SimpleRPG.Gameplay;
using SimpleRPG.HitDetection;
using UnityEngine;
using EntityId = SimpleRPG.Gameplay.EntityId;

namespace SimpleRPG.Tests.EditMode
{
    /// <summary>
    /// What a triggered effect requires of a trigger: its kind, whose action it is, and every condition. Pure rules over a context, no world.
    /// </summary>
    public class TriggeredEffectConditionTests
    {
        private static readonly SourceHandle Sword = SourceHandle.FromItem(new ItemInstanceId(1));
        private static readonly SourceHandle Dagger = SourceHandle.FromItem(new ItemInstanceId(2));

        private TriggeredEffect effect;

        [TearDown]
        public void TearDown()
        {
            if (effect != null) Object.DestroyImmediate(effect);
        }

        private TriggeredEffect Make(EffectTrigger trigger, EffectScope scope, params EffectCondition[] conditions)
        {
            effect = ScriptableObject.CreateInstance<TriggeredEffect>();
            effect.Configure(null, 1f, trigger, scope, EffectTarget.Other, conditions);
            return effect;
        }

        private static TriggerContext Hit(BodyLocation location, bool fatal, SourceHandle source = default, EffectTrigger trigger = EffectTrigger.OnHit) =>
            new TriggerContext(trigger, new EntityId(1), new EntityId(2), source, (int)location, 10, fatal, default);

        [Test]
        public void ANoConditionEffect_MatchesItsTriggerAndNoOther()
        {
            Make(EffectTrigger.OnHit, EffectScope.Owner);

            Assert.IsTrue(effect.Matches(Hit(BodyLocation.Torso, false), Sword));
            Assert.IsFalse(effect.Matches(Hit(BodyLocation.Torso, false, trigger: EffectTrigger.OnKill), Sword));
        }

        [Test]
        public void AFatalHeadshot_NeedsBothConditions()
        {
            Make(EffectTrigger.OnHit, EffectScope.Owner, new FatalCondition(true), new BodyLocationCondition(BodyLocation.Head));

            Assert.IsTrue(effect.Matches(Hit(BodyLocation.Head, true), Sword));
            Assert.IsFalse(effect.Matches(Hit(BodyLocation.Head, false), Sword), "Not fatal.");
            Assert.IsFalse(effect.Matches(Hit(BodyLocation.Torso, true), Sword), "Not the head.");
        }

        [Test]
        public void ALimbShot_CanNameSeveralLocations()
        {
            Make(EffectTrigger.OnHit, EffectScope.Owner, new BodyLocationCondition(BodyLocation.Arms, BodyLocation.Legs));

            Assert.IsTrue(effect.Matches(Hit(BodyLocation.Arms, false), Sword));
            Assert.IsTrue(effect.Matches(Hit(BodyLocation.Legs, true), Sword));
            Assert.IsFalse(effect.Matches(Hit(BodyLocation.Head, false), Sword));
        }

        [Test]
        public void ABodyLocationCondition_NeverPassesWhereNothingWasStruck()
        {
            var condition = new BodyLocationCondition(BodyLocation.Head);
            var none = new TriggerContext(EffectTrigger.OnKill, new EntityId(1), new EntityId(2), default, TriggerContext.NoBodyLocation, 0, true, default);

            Assert.IsFalse(condition.Passes(none));
        }

        [Test]
        public void AHostScopedEffect_NeedsItsHostToHaveDoneIt()
        {
            Make(EffectTrigger.OnHit, EffectScope.Host);

            Assert.IsTrue(effect.Matches(Hit(BodyLocation.Torso, false, Sword), Sword));
            Assert.IsFalse(effect.Matches(Hit(BodyLocation.Torso, false, Dagger), Sword));
            Assert.IsFalse(effect.Matches(Hit(BodyLocation.Torso, false), Sword), "A blow from no item at all is not the sword's.");
        }

        [Test]
        public void AConditionWithNoLocations_IsReportedAsAProblem()
        {
            Assert.IsNotNull(new BodyLocationCondition(new BodyLocation[0]).Problem());
            Assert.IsNull(new BodyLocationCondition(BodyLocation.Head).Problem());
        }
    }
}
