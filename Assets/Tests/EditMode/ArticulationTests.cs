using NUnit.Framework;
using SimpleRPG.Abilities;
using SimpleRPG.Articulation;
using UnityEngine;

namespace SimpleRPG.Tests.EditMode
{
    /// <summary>
    /// Checks view-driven articulation without a scene or a network: how the torso and pelvis follow the view, how the attack phase limits that,
    /// the anatomical limits, how the spine chain is posed, the combo phase it reads, and the wire format of the pose.
    /// </summary>
    public class ArticulationTests
    {
        private const float Dt = 0.01f;

        private ArticulationSettings settings;
        private UpperBodySolver solver;

        [SetUp]
        public void SetUp()
        {
            settings = new ArticulationSettings();
            solver = new UpperBodySolver(settings);
        }

        private void Run(float viewYaw, float viewPitch, ArticulationPhase phase, float seconds)
        {
            for (float t = 0f; t < seconds; t += Dt)
            {
                solver.Step(viewYaw, viewPitch, phase, Dt);
            }
        }

        // --- Horizontal ---

        [Test]
        public void Yaw_FreeTorsoFollowsTheViewAtOnce()
        {
            solver.Step(30f, 0f, ArticulationPhase.Free, Dt);

            Assert.AreEqual(30f, solver.TorsoYaw, 0.001f);
        }

        [Test]
        public void Yaw_PelvisTrailsTheTorsoSoTheSpineTwists()
        {
            solver.Step(40f, 0f, ArticulationPhase.Free, Dt);

            Assert.Greater(solver.Twist, 30f, "the pelvis cannot have caught up in one step");
            Assert.Less(solver.PelvisYaw, solver.TorsoYaw);
        }

        [Test]
        public void Yaw_PelvisEventuallyFacesTheView()
        {
            Run(50f, 0f, ArticulationPhase.Free, 3f);

            Assert.AreEqual(50f, solver.PelvisYaw, 0.01f);
            Assert.AreEqual(0f, solver.Twist, 0.01f);
        }

        [Test]
        public void Yaw_TwistNeverExceedsTheAnatomicalLimit()
        {
            for (float yaw = 0f; yaw < 720f; yaw += 7f)
            {
                solver.Step(yaw, 0f, ArticulationPhase.Free, Dt);
                Assert.LessOrEqual(Mathf.Abs(solver.Twist), settings.maxTwist + 0.001f);
                Assert.LessOrEqual(Mathf.Abs(Mathf.DeltaAngle(solver.PelvisYaw, solver.TorsoYaw)), settings.maxTwist + 0.001f);
            }
        }

        [Test]
        public void Yaw_PastTheLimitThePelvisIsDraggedAlongWithoutSnapping()
        {
            // Turn 8 degrees a step, faster than the pelvis can follow: the twist reaches the limit and from then on the pelvis is
            // dragged at the view's own pace, never jumping ahead of it.
            float previousPelvis = 0f;
            bool dragged = false;
            for (int i = 1; i <= 100; i++)
            {
                solver.Step(i * 8f, 0f, ArticulationPhase.Free, Dt);
                Assert.LessOrEqual(Mathf.Abs(Mathf.DeltaAngle(previousPelvis, solver.PelvisYaw)), 8.01f);
                Assert.LessOrEqual(Mathf.Abs(Mathf.DeltaAngle(solver.PelvisYaw, solver.TorsoYaw)), settings.maxTwist + 0.001f);
                dragged |= Mathf.Abs(Mathf.DeltaAngle(solver.PelvisYaw, solver.TorsoYaw)) > settings.maxTwist - 0.01f;
                previousPelvis = solver.PelvisYaw;
            }

            Assert.IsTrue(dragged);
        }

        [Test]
        public void Yaw_WrapsThroughMinusOneEighty()
        {
            solver.Reset(170f, 0f);
            Run(-170f, 0f, ArticulationPhase.Free, 2f);

            Assert.AreEqual(0f, Mathf.DeltaAngle(solver.TorsoYaw, -170f), 0.01f);
            Assert.AreEqual(0f, Mathf.DeltaAngle(solver.PelvisYaw, -170f), 0.01f);
        }

        // --- Vertical ---

        [Test]
        public void Pitch_LookingDownTiltsForward()
        {
            Run(0f, 40f, ArticulationPhase.Free, 1f);

            Assert.AreEqual(40f * settings.pitchGain, solver.TorsoPitch, 0.01f);
        }

        [Test]
        public void Pitch_LookingUpTiltsBackward()
        {
            Run(0f, -30f, ArticulationPhase.Free, 1f);

            Assert.AreEqual(-30f * settings.pitchGain, solver.TorsoPitch, 0.01f);
        }

        [Test]
        public void Pitch_IsClampedToTheAnatomicalLimits()
        {
            Run(0f, 90f, ArticulationPhase.Free, 1f);
            Assert.AreEqual(settings.maxPitchDown, solver.TorsoPitch, 0.01f);

            Run(0f, -90f, ArticulationPhase.Free, 1f);
            Assert.AreEqual(-settings.maxPitchUp, solver.TorsoPitch, 0.01f);
        }

        // --- Attack phases ---

        [Test]
        public void Phase_ActiveStrikeLimitsHowFastTheTorsoFollowsTheView()
        {
            solver.Reset(0f, 0f);
            Run(0f, 0f, ArticulationPhase.Active, 0.5f);

            solver.Step(90f, 0f, ArticulationPhase.Active, 0.1f);

            Assert.LessOrEqual(solver.TorsoYaw, settings.active.yawSpeed * 0.1f + 0.01f);
            Assert.Less(solver.TorsoYaw, 90f);
        }

        [Test]
        public void Phase_TorsoReachesTheViewUnderTheActiveLimitOverTime()
        {
            Run(60f, 0f, ArticulationPhase.Active, 2f);

            Assert.AreEqual(60f, solver.TorsoYaw, 0.01f);
        }

        [Test]
        public void Phase_ActivePitchIsLimitedToo()
        {
            Run(0f, 0f, ArticulationPhase.Active, 0.5f);
            solver.Step(0f, 45f, ArticulationPhase.Active, 0.1f);

            Assert.LessOrEqual(solver.TorsoPitch, settings.active.pitchSpeed * 0.1f + 0.01f);
        }

        [Test]
        public void Phase_WindupAllowsMoreControlThanTheStrike()
        {
            var windup = new UpperBodySolver(settings);
            var active = new UpperBodySolver(settings);
            for (int i = 0; i < 50; i++)
            {
                windup.Step(0f, 0f, ArticulationPhase.Windup, Dt);
                active.Step(0f, 0f, ArticulationPhase.Active, Dt);
            }

            for (int i = 0; i < 20; i++)
            {
                windup.Step(120f, 0f, ArticulationPhase.Windup, Dt);
                active.Step(120f, 0f, ArticulationPhase.Active, Dt);
            }

            Assert.Greater(windup.TorsoYaw, active.TorsoYaw * 1.5f);
        }

        [Test]
        public void Phase_PelvisNeverTurnsFasterThanTheTorsoMay()
        {
            solver.Reset(0f, 0f);
            Run(0f, 0f, ArticulationPhase.Active, 0.5f);
            float before = solver.PelvisYaw;

            solver.Step(170f, 0f, ArticulationPhase.Active, 0.1f);

            Assert.LessOrEqual(Mathf.Abs(solver.PelvisYaw - before), settings.active.yawSpeed * 0.1f + 0.01f);
        }

        [Test]
        public void Phase_ControlReturnsSmoothlyWhenTheStrikeEnds()
        {
            Run(0f, 0f, ArticulationPhase.Active, 1f);
            float before = solver.TorsoYaw;

            // The limit eases from the strike's to recovery's: the first step after is not already at the faster speed.
            solver.Step(170f, 0f, ArticulationPhase.Recovery, Dt);
            float firstStep = solver.TorsoYaw - before;
            Run(170f, 0f, ArticulationPhase.Recovery, 0.1f);

            Assert.Less(firstStep, settings.recovery.yawSpeed * Dt);
            Assert.Greater(solver.TorsoYaw, 12f, "control has returned");
        }

        // --- Determinism and stability ---

        [Test]
        public void Step_TwoSolversGivenTheSameInputsAgree()
        {
            var other = new UpperBodySolver(settings);
            for (int i = 0; i < 300; i++)
            {
                float yaw = Mathf.Sin(i * 0.05f) * 120f;
                ArticulationPhase phase = (ArticulationPhase)(i / 50 % 4);
                solver.Step(yaw, yaw * 0.3f, phase, Dt);
                other.Step(yaw, yaw * 0.3f, phase, Dt);
            }

            Assert.AreEqual(solver.TorsoYaw, other.TorsoYaw);
            Assert.AreEqual(solver.PelvisYaw, other.PelvisYaw);
            Assert.AreEqual(solver.TorsoPitch, other.TorsoPitch);
        }

        [Test]
        public void Step_ZeroTimeChangesNothing()
        {
            Run(30f, 20f, ArticulationPhase.Free, 0.5f);
            float torso = solver.TorsoYaw;

            solver.Step(-100f, 0f, ArticulationPhase.Free, 0f);

            Assert.AreEqual(torso, solver.TorsoYaw);
        }

        // --- Owner prediction ---
        // The owning client and the server each run their own solver on the same view. These prove the prediction carries the whole solution
        // (pelvis included), that frame rate does not change where it ends up, and that it chases absolute targets so nothing accumulates.

        private UpperBodySolver Fresh()
        {
            var other = new UpperBodySolver(settings);
            other.Reset(0f, 0f);
            return other;
        }

        [Test]
        public void Prediction_PelvisIsDraggedAlongWhenTheTorsoWouldPassTheLimit()
        {
            UpperBodySolver predicted = Fresh();

            for (int i = 1; i <= 30; i++)
            {
                predicted.Step(i * 8f, 0f, ArticulationPhase.Free, Dt);
            }

            Assert.AreEqual(240f - 360f, predicted.TorsoYaw, 0.01f);
            Assert.AreNotEqual(0f, predicted.PelvisYaw, "the owner's own pelvis turned, instead of waiting for the replicated root");
            Assert.LessOrEqual(Mathf.Abs(predicted.Twist), settings.maxTwist + 0.001f);
        }

        [Test]
        public void Prediction_AndServerAgreeFrameForFrameOnTheSameInputs()
        {
            UpperBodySolver server = Fresh();
            UpperBodySolver owner = Fresh();

            for (int i = 1; i <= 120; i++)
            {
                ArticulationPhase phase = i < 40 ? ArticulationPhase.Free : i < 70 ? ArticulationPhase.Windup : ArticulationPhase.Active;
                server.Step(i * 3f, 20f, phase, Dt);
                owner.Step(i * 3f, 20f, phase, Dt);
                Assert.AreEqual(server.PelvisYaw, owner.PelvisYaw, 0f);
                Assert.AreEqual(server.TorsoYaw, owner.TorsoYaw, 0f);
                Assert.AreEqual(server.Twist, owner.Twist, 0f);
            }
        }

        [Test]
        public void Prediction_ArrivesWhereTheServerDoesAtAnyFrameRate()
        {
            UpperBodySolver fast = Fresh();
            UpperBodySolver slow = Fresh();

            for (int i = 0; i < 400; i++) fast.Step(135f, 10f, ArticulationPhase.Free, 1f / 240f);
            for (int i = 0; i < 100; i++) slow.Step(135f, 10f, ArticulationPhase.Free, 1f / 60f);

            Assert.AreEqual(0f, Mathf.DeltaAngle(fast.PelvisYaw, slow.PelvisYaw), 0.01f);
            Assert.AreEqual(0f, Mathf.DeltaAngle(fast.TorsoYaw, slow.TorsoYaw), 0.01f);
        }

        [Test]
        public void Prediction_LateOrLostInputsLeaveNoLastingDifference()
        {
            UpperBodySolver server = Fresh();
            UpperBodySolver owner = Fresh();

            // The owner sees the view straight away; the server sees it a few steps late and misses some intermediate views. Both chase the same
            // absolute view, so once it holds still the two agree exactly.
            for (int i = 1; i <= 60; i++)
            {
                float view = i * 6f;
                owner.Step(view, 0f, ArticulationPhase.Free, Dt);
                if (i % 5 != 0) server.Step(Mathf.Max(0f, view - 24f), 0f, ArticulationPhase.Free, Dt);
            }

            for (int i = 0; i < 120; i++)
            {
                owner.Step(360f, 0f, ArticulationPhase.Free, Dt);
                server.Step(360f, 0f, ArticulationPhase.Free, Dt);
            }

            Assert.AreEqual(0f, Mathf.DeltaAngle(owner.PelvisYaw, server.PelvisYaw), 0.01f);
            Assert.AreEqual(0f, Mathf.DeltaAngle(owner.TorsoYaw, server.TorsoYaw), 0.01f);
        }

        [Test]
        public void Prediction_ASpinFullCircleNeverAccumulatesAnError()
        {
            UpperBodySolver owner = Fresh();

            for (int turn = 0; turn < 5; turn++)
            {
                for (int i = 1; i <= 60; i++) owner.Step(Mathf.Repeat(i * 6f + 180f, 360f) - 180f, 0f, ArticulationPhase.Free, Dt);
            }

            for (int i = 0; i < 120; i++) owner.Step(0f, 0f, ArticulationPhase.Free, Dt);

            Assert.AreEqual(0f, Mathf.DeltaAngle(owner.PelvisYaw, 0f), 0.01f);
            Assert.AreEqual(0f, owner.Twist, 0.01f);
        }

        // --- Spine pose ---

        private GameObject root;
        private Transform[] chain;
        private SpineRig rig;

        private void BuildChain(int count = 3)
        {
            root = new GameObject("Root");
            chain = new Transform[count];
            Transform parent = root.transform;
            for (int i = 0; i < count; i++)
            {
                var bone = new GameObject("Bone" + i).transform;
                bone.SetParent(parent, false);
                bone.localPosition = Vector3.up * 0.3f;
                chain[i] = bone;
                parent = bone;
            }

            float[] share = new float[count];
            for (int i = 0; i < count; i++) share[i] = 1f / count;
            rig = new SpineRig(chain, share, share);
        }

        [TearDown]
        public void TearDown()
        {
            if (root != null) Object.DestroyImmediate(root);
        }

        [Test]
        public void Rig_TwistTurnsTheTopOfTheChainByTheTwist()
        {
            BuildChain();
            Transform top = new GameObject("Hand").transform;
            top.SetParent(chain[2], false);
            top.localPosition = Vector3.forward;

            rig.Apply(root.transform, 60f, 0f);

            Assert.AreEqual(60f, Vector3.SignedAngle(root.transform.forward, Vector3.ProjectOnPlane(chain[2].forward, Vector3.up), Vector3.up), 0.01f);
            Assert.AreEqual(60f, Vector3.SignedAngle(Vector3.forward, top.position - chain[2].position, Vector3.up), 0.01f);
        }

        [Test]
        public void Rig_PitchTiltsTheTopOfTheChainForward()
        {
            BuildChain();

            rig.Apply(root.transform, 0f, 30f);

            Assert.AreEqual(30f, Vector3.Angle(Vector3.forward, chain[2].forward), 0.01f);
            Assert.Less(chain[2].forward.y, 0f, "positive pitch tips the chest forward and down");
        }

        [Test]
        public void Rig_NegativePitchLeansBack()
        {
            BuildChain();

            rig.Apply(root.transform, 0f, -30f);

            Assert.Greater(chain[2].forward.y, 0f);
        }

        [Test]
        public void Rig_DoesNotMovePivotsOfTheLowestBone()
        {
            BuildChain();
            Vector3 pivot = chain[0].position;

            rig.Apply(root.transform, 50f, 25f);

            Assert.AreEqual(pivot, chain[0].position);
        }

        [Test]
        public void Rig_ZeroPoseLeavesTheAnimationAlone()
        {
            BuildChain();
            Quaternion[] before = new Quaternion[chain.Length];
            for (int i = 0; i < chain.Length; i++) before[i] = chain[i].rotation;

            rig.Apply(root.transform, 0f, 0f);

            for (int i = 0; i < chain.Length; i++) Assert.That(Quaternion.Angle(before[i], chain[i].rotation), Is.LessThan(0.001f));
        }

        [Test]
        public void Rig_RepeatedApplyWithoutAnimationDoesNotAccumulate()
        {
            BuildChain();
            rig.Apply(root.transform, 40f, 20f);
            Quaternion once = chain[2].rotation;

            // No animator rewrote the bones in between, so the old offset must be undone before the new one is added.
            for (int i = 0; i < 20; i++) rig.Apply(root.transform, 40f, 20f);

            Assert.That(Quaternion.Angle(once, chain[2].rotation), Is.LessThan(0.01f));
        }

        [Test]
        public void Rig_SitsOnTopOfAnAnimatedPose()
        {
            BuildChain();
            chain[1].localRotation = Quaternion.Euler(0f, 20f, 0f);
            Quaternion animated = chain[2].rotation;

            rig.Apply(root.transform, 30f, 0f);

            Assert.AreEqual(30f, Quaternion.Angle(animated, chain[2].rotation), 0.01f, "the authored pose is kept and the twist is added to it");
        }

        [Test]
        public void Rig_MissingBonesAreSkipped()
        {
            BuildChain();
            var sparse = new SpineRig(new[] { chain[0], null, chain[2] }, new[] { 0.5f, 0.2f, 0.5f }, new[] { 0.5f, 0.2f, 0.5f });

            Assert.DoesNotThrow(() => sparse.Apply(root.transform, 30f, 10f));
            Assert.IsTrue(sparse.IsUsable);
            Assert.IsFalse(new SpineRig(new Transform[2], new float[2], new float[2]).IsUsable);
        }

        // --- Combo phase ---

        [Test]
        public void ComboPhase_FollowsTheHitWindow()
        {
            var machine = new ComboMachine(new[] { new ComboStepTiming(1f, 0.375f, 0.5f, 0f, 0f) });
            var events = new System.Collections.Generic.List<ComboEvent>();

            Assert.AreEqual(ComboPhase.None, machine.PhaseAt(5.0));

            machine.Press(10.0, events);
            Assert.AreEqual(ComboPhase.Windup, machine.PhaseAt(10.0));
            Assert.AreEqual(ComboPhase.Windup, machine.PhaseAt(10.25));
            Assert.AreEqual(ComboPhase.Active, machine.PhaseAt(10.375));
            Assert.AreEqual(ComboPhase.Active, machine.PhaseAt(10.45));
            Assert.AreEqual(ComboPhase.Recovery, machine.PhaseAt(10.5));
            Assert.AreEqual(ComboPhase.Recovery, machine.PhaseAt(10.9));
            Assert.AreEqual(ComboPhase.None, machine.PhaseAt(11.0));
        }

        // --- Wire format ---

        [Test]
        public void Pose_QuantisesToHundredthsOfADegree()
        {
            ArticulationPose pose = ArticulationPose.From(-37.456f, 12.344f);

            Assert.AreEqual(-37.46f, pose.TwistDegrees, 0.0001f);
            Assert.AreEqual(12.34f, pose.PitchDegrees, 0.0001f);
        }

        [Test]
        public void Pose_CarriesThePelvisYawAcrossTheWrapAround()
        {
            Assert.AreEqual(-179.5f, ArticulationPose.From(0f, 0f, 180.5f).PelvisYawDegrees, 0.0001f);
            Assert.AreEqual(-90.25f, ArticulationPose.From(0f, 0f, -90.25f).PelvisYawDegrees, 0.0001f);
            Assert.AreNotEqual(ArticulationPose.From(0f, 0f, 10f), ArticulationPose.From(0f, 0f, 20f));
        }

        [Test]
        public void Pose_AnythingBeyondTheRangeIsClampedNotWrapped()
        {
            ArticulationPose pose = ArticulationPose.From(100000f, -100000f);

            Assert.Greater(pose.TwistDegrees, 300f);
            Assert.Less(pose.PitchDegrees, -300f);
        }
    }
}
