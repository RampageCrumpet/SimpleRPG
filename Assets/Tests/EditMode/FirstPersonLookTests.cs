using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace SimpleRPG.Tests.EditMode
{
    /// <summary>
    /// Checks first-person look without a network: pitch limits, time scaling, direction, yaw routing, camera attachment,
    /// movement direction and who is allowed to look. The network-backed yaw is replaced by <see cref="FakeViewSink"/>.
    /// </summary>
    public class FirstPersonLookTests
    {
        private const float RotationSpeed = 100f;
        private const float MinimumPitch = -60f;
        private const float MaximumPitch = 60f;

        private GameObject cameraObject;
        private GameObject bodyObject;
        private GameObject anchorObject;
        private FakeViewSink viewSink;
        private FirstPersonLook look;

        [SetUp]
        public void SetUp()
        {
            cameraObject = new GameObject("Camera");
            bodyObject = new GameObject("Body");
            anchorObject = new GameObject("Anchor");
            viewSink = new FakeViewSink();
            look = new FirstPersonLook(viewSink, RotationSpeed, MinimumPitch, MaximumPitch);
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(cameraObject);
            Object.DestroyImmediate(bodyObject);
            Object.DestroyImmediate(anchorObject);
        }

        // --- Pitch ---

        [Test]
        public void Pitch_RepeatedUpwardInputStopsAtTheMaximum()
        {
            for (int i = 0; i < 100; i++)
            {
                look.Look(0f, 1f, 0.1f);
            }

            Assert.AreEqual(MaximumPitch, look.Pitch, 0.0001f);
        }

        [Test]
        public void Pitch_RepeatedDownwardInputStopsAtTheMinimum()
        {
            for (int i = 0; i < 100; i++)
            {
                look.Look(0f, -1f, 0.1f);
            }

            Assert.AreEqual(MinimumPitch, look.Pitch, 0.0001f);
        }

        [Test]
        public void Pitch_StepScalesWithDeltaTime()
        {
            look.Look(0f, 2f, 0.05f);

            // 2 * 100 deg/s * 0.05 s = 10 degrees.
            Assert.AreEqual(10f, look.Pitch, 0.0001f);
        }

        [Test]
        public void Pitch_TotalIsTheSameRegardlessOfFrameRate()
        {
            // Ten frames at 0.01 s and five frames at 0.02 s cover the same time, so they must move the same amount.
            var tenFramesObject = new GameObject("TenFrames");
            var fiveFramesObject = new GameObject("FiveFrames");
            try
            {
                var tenFrames = new FirstPersonLook(new FakeViewSink(), RotationSpeed, MinimumPitch, MaximumPitch);
                var fiveFrames = new FirstPersonLook(new FakeViewSink(), RotationSpeed, MinimumPitch, MaximumPitch);

                for (int i = 0; i < 10; i++) tenFrames.Look(0f, 1f, 0.01f);
                for (int i = 0; i < 5; i++) fiveFrames.Look(0f, 1f, 0.02f);

                Assert.AreEqual(tenFrames.Pitch, fiveFrames.Pitch, 0.0001f);
            }
            finally
            {
                Object.DestroyImmediate(tenFramesObject);
                Object.DestroyImmediate(fiveFramesObject);
            }
        }

        [Test]
        public void Pitch_PositiveMouseYIncreasesPitch()
        {
            // Documents the current sign: positive mouse Y adds to the pitch, which is applied to the camera's x rotation.
            look.Look(0f, 1f, 0.1f);

            Assert.Greater(look.Pitch, 0f);
        }

        [Test]
        public void Look_NeverTouchesTheCameraOrTheBody()
        {
            bodyObject.transform.rotation = Quaternion.Euler(0f, 45f, 0f);
            cameraObject.transform.rotation = Quaternion.Euler(10f, 20f, 30f);
            Quaternion bodyBefore = bodyObject.transform.rotation;
            Quaternion cameraBefore = cameraObject.transform.rotation;

            look.Look(3f, 1f, 0.1f);
            look.ResetYaw(75f);

            Assert.AreEqual(bodyBefore, bodyObject.transform.rotation);
            Assert.AreEqual(cameraBefore, cameraObject.transform.rotation, "the view is input state; the camera's pose is the head's");
        }

        // --- Yaw routing ---

        [Test]
        public void Yaw_IsSentToTheSinkScaledByDeltaTimeAndRotationSpeed()
        {
            look.Look(2f, 0f, 0.05f);

            Assert.AreEqual(1, viewSink.Looks.Count);
            Assert.AreEqual(10f, viewSink.Looks[0].YawDelta, 0.0001f);
        }

        [Test]
        public void Yaw_DoesNotChangeThePitch()
        {
            look.Look(5f, 0f, 0.1f);

            Assert.AreEqual(0f, look.Pitch, 0.0001f);
        }

        [Test]
        public void Yaw_FrameWithNoMouseMovementStillReportsZeroYaw()
        {
            look.Look(0f, 0f, 0.1f);

            Assert.AreEqual(0f, viewSink.Looks[0].YawDelta, 0.0001f);
        }

        [Test]
        public void Yaw_AccumulatesAndStartsFromTheInitialYaw()
        {
            var turned = new FirstPersonLook(new FakeViewSink(), RotationSpeed, MinimumPitch, MaximumPitch, 90f);

            turned.Look(2f, 0f, 0.05f);

            Assert.AreEqual(100f, turned.Yaw, 0.0001f);
        }

        [Test]
        public void View_ReportsThePitchAlongWithTheYaw()
        {
            look.Look(0f, 2f, 0.05f);

            Assert.AreEqual(10f, viewSink.Looks[0].Pitch, 0.0001f);
        }

        [Test]
        public void ResetYaw_AimsTheViewWithoutReportingAndClearsThePitch()
        {
            look.Look(0f, 2f, 0.05f);
            viewSink.Looks.Clear();

            look.ResetYaw(75f);

            Assert.AreEqual(0, viewSink.Looks.Count);
            Assert.AreEqual(0f, look.Pitch);
            Assert.AreEqual(75f, look.Yaw, 0.0001f);
        }

        // --- Gating ---

        [Test]
        public void ShouldLook_OnlyTheOwnerWhileFocusedLooks()
        {
            Assert.IsTrue(FirstPersonLookMath.ShouldLook(isOwner: true, isFocused: true));
        }

        [Test]
        public void ShouldLook_NonOwnerNeverLooks()
        {
            Assert.IsFalse(FirstPersonLookMath.ShouldLook(isOwner: false, isFocused: true));
            Assert.IsFalse(FirstPersonLookMath.ShouldLook(isOwner: false, isFocused: false));
        }

        [Test]
        public void ShouldLook_UnfocusedApplicationNeverLooks()
        {
            Assert.IsFalse(FirstPersonLookMath.ShouldLook(isOwner: true, isFocused: false));
        }

        // --- Camera attachment ---

        [Test]
        public void AttachCamera_ParentsTheCameraAndResetsItsLocalPosition()
        {
            cameraObject.transform.localPosition = new Vector3(3f, 4f, 5f);

            FirstPersonLookMath.AttachCamera(cameraObject.transform, anchorObject.transform);

            Assert.AreSame(anchorObject.transform, cameraObject.transform.parent);
            Assert.AreEqual(Vector3.zero, cameraObject.transform.localPosition);
        }

        [Test]
        public void StableEyePosition_HoldsTheRestEyePlusTheLean_AndIgnoresTheAnimatedEyeWhileAlive()
        {
            Vector3 position = FirstPersonLookMath.StableEyePosition(new Vector3(0f, 1f, 0f), new Vector3(0f, -0.1f, 0.2f), new Vector3(0.05f, 1.07f, 0.04f), 1f);

            Assert.AreEqual(0f, Vector3.Distance(new Vector3(0f, 0.9f, 0.2f), position), 0.0001f);
        }

        [Test]
        public void StableEyePosition_FollowsTheAnimatedEyeOnceStabilityIsGone()
        {
            Vector3 animated = new Vector3(0.05f, 0.3f, 0.4f);

            Assert.AreEqual(0f, Vector3.Distance(animated, FirstPersonLookMath.StableEyePosition(Vector3.up, Vector3.forward, animated, 0f)), 0.0001f);
        }

        [Test]
        public void AttachCamera_DoesNotMoveTheAnchor()
        {
            anchorObject.transform.position = new Vector3(1f, 2f, 3f);

            FirstPersonLookMath.AttachCamera(cameraObject.transform, anchorObject.transform);

            Assert.AreEqual(new Vector3(1f, 2f, 3f), anchorObject.transform.position);
        }

        // --- Movement direction ---

        [Test]
        public void MovementDirection_ForwardInputIsAlongForward()
        {
            Vector3 direction = FirstPersonLookMath.MovementDirection(Vector3.forward, Vector3.right, 1f, 0f);

            Assert.AreEqual(Vector3.forward, direction);
        }

        [Test]
        public void MovementDirection_DiagonalInputIsNormalized()
        {
            Vector3 direction = FirstPersonLookMath.MovementDirection(Vector3.forward, Vector3.right, 1f, 1f);

            Assert.AreEqual(1f, direction.magnitude, 0.0001f);
        }

        [Test]
        public void MovementDirection_IsFlatEvenWhenForwardIsTilted()
        {
            Vector3 tiltedForward = new Vector3(0f, 0.5f, 1f).normalized;

            Vector3 direction = FirstPersonLookMath.MovementDirection(tiltedForward, Vector3.right, 1f, 0f);

            Assert.AreEqual(0f, direction.y, 0.0001f);
        }

        [Test]
        public void ViewAxes_AreFlatAndFollowTheViewYawNotTheBody()
        {
            FirstPersonLookMath.ViewAxes(90f, out Vector3 forward, out Vector3 right);

            Assert.AreEqual(Vector3.right.x, forward.x, 0.0001f);
            Assert.AreEqual(0f, forward.y, 0.0001f);
            Assert.AreEqual(-1f, right.z, 0.0001f);
        }

        [Test]
        public void MovementDirection_NoInputGivesZero()
        {
            Vector3 direction = FirstPersonLookMath.MovementDirection(Vector3.forward, Vector3.right, 0f, 0f);

            Assert.AreEqual(Vector3.zero, direction);
        }

        /// <summary>
        /// Records every view change the look controller sends, in place of the server RPC.
        /// </summary>
        private sealed class FakeViewSink : IViewSink
        {
            public readonly List<(float YawDelta, float Pitch)> Looks = new List<(float YawDelta, float Pitch)>();

            public void Look(float yawDelta, float pitch)
            {
                Looks.Add((yawDelta, pitch));
            }
        }
    }
}
