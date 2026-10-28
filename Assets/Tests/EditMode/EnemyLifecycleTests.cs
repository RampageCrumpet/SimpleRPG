using AI;
using NUnit.Framework;
using SimpleRPG.AI;
using System.Collections.Generic;
using UnityEngine;

namespace SimpleRPG.Tests
{
    public sealed class FixedTargetSense : PrimarySense
    {
        public Character[] Targets;
        public override IEnumerable<Character> Sense() => Targets;
        public override bool CanSense(Character target) => System.Array.IndexOf(Targets, target) >= 0;
    }

    public class EnemyLifecycleTests
    {
        private sealed class State : IState
        {
            public System.Action Enter;
            public int Updates;
            public int Exits;
            public void OnStateEnter() => Enter?.Invoke();
            public void OnStateUpdate() => Updates++;
            public void OnStateExit() => Exits++;
        }

        [Test]
        public void TransitionDuringEnter_PreservesTheFinalState()
        {
            var machine = new StateMachine();
            var first = new State();
            var final = new State();
            var middle = new State { Enter = () => machine.ChangeState(final) };
            machine.InitializeStateMachine(first);
            machine.ChangeState(middle);
            machine.Update();
            Assert.AreEqual(1, final.Updates);
            Assert.AreEqual(0, middle.Updates);
            Assert.AreEqual(1, middle.Exits);
        }

        [Test]
        public void TargetTracking_ChoosesNearestAndReacquiresWithoutThrowingWhenTargetsDisappear()
        {
            var owner = new GameObject("Enemy");
            var near = new GameObject("Near");
            var far = new GameObject("Far");
            try
            {
                var controller = owner.AddComponent<EnemyController>();
                controller.Character = owner.GetComponent<Character>();
                var close = near.AddComponent<Character>();
                var distant = far.AddComponent<Character>();
                near.transform.position = Vector3.forward;
                far.transform.position = Vector3.forward * 10;
                var sense = owner.AddComponent<FixedTargetSense>();
                sense.Targets = new[] { distant, controller.Character, close, close };
                controller.PrimarySenses = new[] { sense };
                controller.SenseInterval = 0f; // Time does not advance in this test, and it changes the sensed set between calls.
                Assert.IsTrue(controller.TrackTarget());
                Assert.AreSame(close, controller.Target);
                sense.Targets = new[] { distant };
                Assert.IsTrue(controller.TrackTarget());
                Assert.AreSame(distant, controller.Target);
                sense.Targets = System.Array.Empty<Character>();
                Assert.IsFalse(controller.TrackTarget());
                Assert.IsNull(controller.Target);
            }
            finally
            {
                Object.DestroyImmediate(owner);
                Object.DestroyImmediate(near);
                Object.DestroyImmediate(far);
            }
        }
    }
}

