using HitDetection;
using SimpleRPG.Abilities.Integration;
using SimpleRPG.Gameplay;
using NUnit.Framework;
using SimpleRPG.Articulation;
using SimpleRPG.Animations;
using SimpleRPG.UI;
using System.Collections;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.TestTools;

namespace SimpleRPG.Abilities.Tests.PlayMode
{
    /// <summary>
    /// Runs the real player prefab on a host with its real rig and animator and checks that the view moves the body, the sword and the hurtboxes,
    /// that nothing drifts, and that the camera stays on the view whatever the body does.
    /// </summary>
    public class UpperBodyArticulationPlayModeTests
    {
        private const string PlayerPrefab = "Assets/Prefabs/Characters/Player/TestCharacter.prefab";
        private const string MeleeAttackPath = "Assets/Abilities/Abilities/MeleeAttack.asset";
        private const string WeaponPath = "Assets/Items/Weapon Data/TestWeaponData.asset";

        private GameObject managerObject;
        private GameObject cameraObject;
        private GameObject floorObject;
        private GameObject playerObject;
        private NetworkManager manager;
        private UpperBodyArticulation articulation;
        private Hitbox sword;
        private HurtBox head;
        private Transform spineTop;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            GameplayServices.Reset();
            AbilityServices.Reset();
            floorObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
            floorObject.transform.position = new Vector3(0f, -1.13f, 0f);
            floorObject.transform.localScale = new Vector3(200f, 1f, 200f);
            cameraObject = new GameObject("TestMainCamera", typeof(Camera)) { tag = "MainCamera" };

            managerObject = new GameObject("TestNetworkManager");
            UnityTransport transport = managerObject.AddComponent<UnityTransport>();
            manager = managerObject.AddComponent<NetworkManager>();
            manager.NetworkConfig = new NetworkConfig { NetworkTransport = transport };
            Assert.IsTrue(manager.StartHost(), "The test host should start.");
            yield return null;

            GameObject prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefab);
            Assert.IsNotNull(prefab);
            playerObject = Object.Instantiate(prefab, Vector3.zero, Quaternion.identity);
            foreach (System.Type type in new[] { typeof(RegisterWithUI) })
            {
                Component component = playerObject.GetComponent(type);
                if (component != null) Object.DestroyImmediate(component);
            }

            Character character = playerObject.GetComponent<Character>();
            character.Initialize();
            character.WeaponData = UnityEditor.AssetDatabase.LoadAssetAtPath<WeaponData>(WeaponPath);
            playerObject.GetComponent<NetworkObject>().Spawn();
            articulation = playerObject.GetComponent<UpperBodyArticulation>();
            sword = playerObject.GetComponentInChildren<Hitbox>(true);
            foreach (HurtBox box in playerObject.GetComponentsInChildren<HurtBox>(true))
            {
                if (box.BodyLocation == BodyLocation.Head) head = box;
            }

            spineTop = playerObject.GetComponent<Animator>().GetBoneTransform(HumanBodyBones.UpperChest);
            yield return Frames(5);
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (manager != null && manager.IsListening)
            {
                manager.Shutdown();
                yield return new WaitUntil(() => !manager.IsListening);
            }

            if (playerObject != null) Object.Destroy(playerObject);
            if (cameraObject != null) Object.Destroy(cameraObject);
            if (floorObject != null) Object.Destroy(floorObject);
            if (managerObject != null) Object.Destroy(managerObject);
            GameplayServices.Reset();
            AbilityServices.Reset();
            yield return null;
        }

        private static IEnumerator Frames(int count)
        {
            for (int i = 0; i < count; i++) yield return null;
        }

        private static IEnumerator Seconds(float seconds)
        {
            float end = Time.time + seconds;
            while (Time.time < end) yield return null;
        }

        private Vector3 SwordInBody() => playerObject.transform.InverseTransformPoint(sword.transform.position);

        private Vector3 HeadInBody() => playerObject.transform.InverseTransformPoint(head.transform.position);

        // The host is its own owner, so this is the call the mouse makes. The look controller is switched off so it does not report its own
        // (centred) view over the one the test sets.
        private void Look(float yawDelta, float pitch)
        {
            playerObject.GetComponent<FirstPersonCharacterController>().enabled = false;
            articulation.SubmitLook(yawDelta, pitch);
        }

        [UnityTest]
        public IEnumerator LookingRight_TwistsTheTorsoAndCarriesTheSwordAcrossTheBody()
        {
            Vector3 before = SwordInBody();

            Look(60f, 0f);
            yield return Frames(2);

            Assert.Greater(articulation.AppliedTwist, 5f, "The spine twists toward the view while the pelvis is still behind.");
            Assert.Greater((SwordInBody() - before).magnitude, 0.02f, "The sword hangs below the spine, so it moves with the twist.");
        }

        [UnityTest]
        public IEnumerator Pelvis_FollowsTheViewSoTheTwistFadesAway()
        {
            Look(50f, 0f);
            yield return Seconds(1.5f);

            Assert.AreEqual(50f, Mathf.DeltaAngle(0f, playerObject.transform.eulerAngles.y), 1f);
            Assert.AreEqual(0f, articulation.AppliedTwist, 1f);
        }

        [UnityTest]
        public IEnumerator Twist_NeverPassesTheAnatomicalLimit()
        {
            float limit = articulation.Settings.maxTwist;
            for (int i = 0; i < 40; i++)
            {
                Look(25f, 0f);
                yield return null;
                Assert.LessOrEqual(Mathf.Abs(articulation.AppliedTwist), limit + 0.01f);
            }
        }

        [UnityTest]
        public IEnumerator LookingDown_TipsTheHeadForwardAndDown()
        {
            Vector3 before = HeadInBody();

            Look(0f, 50f);
            yield return Seconds(0.3f);
            Vector3 after = HeadInBody();

            Assert.Greater(articulation.AppliedPitch, 10f);
            Assert.Greater(after.z, before.z + 0.05f, "the head hurtbox moves forward");
            Assert.Less(after.y, before.y - 0.02f, "and down, so a high blow can miss it");
        }

        [UnityTest]
        public IEnumerator LookingUp_LeansTheHeadBack()
        {
            Vector3 before = HeadInBody();

            Look(0f, -50f);
            yield return Seconds(0.3f);

            Assert.Less(articulation.AppliedPitch, -10f);
            Assert.Less(HeadInBody().z, before.z - 0.02f);
        }

        [UnityTest]
        public IEnumerator ReturningTheViewToCentre_LeavesNoOffsetBehind()
        {
            // Idle animations vary, so bones are not compared with an earlier pose; what must hold is that the added twist and tilt are gone,
            // so nothing the rig added in the earlier rounds is still in the spine.
            for (int round = 0; round < 3; round++)
            {
                Look(70f, 40f);
                yield return Seconds(0.3f);
                Look(-70f, 0f);
                yield return Seconds(1.2f);
            }

            Assert.AreEqual(0f, articulation.AppliedTwist, 0.5f);
            Assert.AreEqual(0f, articulation.AppliedPitch, 0.5f);
            Assert.AreEqual(0f, articulation.Solver.Twist, 0.5f);
            Assert.AreEqual(0f, articulation.Solver.TorsoPitch, 0.5f);
        }

        [UnityTest]
        public IEnumerator TheBladeHitboxStaysOnTheRenderedSword()
        {
            Transform model = sword.transform.Find("Longsword Model");
            Assert.IsNotNull(model);

            Look(60f, 30f);
            yield return Frames(3);

            Assert.AreEqual(0f, Vector3.Distance(sword.transform.position, model.position), 0.001f, "the collider's object and the mesh share one transform chain");
            Assert.AreEqual(0f, Quaternion.Angle(sword.transform.rotation, model.rotation), 0.01f);
        }

        // Mouse look as the owner does it: the controller keeps the pitch and yaw it was given on every later frame.
        private FirstPersonLook OwnerLook() => (FirstPersonLook)typeof(FirstPersonCharacterController)
            .GetField("look", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
            .GetValue(playerObject.GetComponent<FirstPersonCharacterController>());

        private Transform Eye() => (Transform)typeof(FirstPersonCharacterController)
            .GetField("eye", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
            .GetValue(playerObject.GetComponent<FirstPersonCharacterController>());

        private Transform HeadBone() => playerObject.GetComponent<Animator>().GetBoneTransform(HumanBodyBones.Head);

        [UnityTest]
        public IEnumerator Camera_HangsOffTheCharacter_AndPointsExactlyWhereTheViewDoes()
        {
            yield return Frames(2);
            Assert.AreSame(playerObject.transform, cameraObject.transform.parent, "the camera is only parented to the character, not to a bone");

            FirstPersonLook look = OwnerLook();
            CameraViewProbe probe = cameraObject.AddComponent<CameraViewProbe>();
            probe.Look = look;
            look.Look(0.5f, 1f, 0.15f);
            for (int i = 0; i < 40; i++)
            {
                // Wait for a LateUpdate that ran after the controller's, which measured the camera against the view at that same moment.
                // Not WaitForEndOfFrame (never fires while the Game view is not rendering), and not a bare yield, which resumes before LateUpdate
                // so the view could change (mouse input, a server reset) after the camera was last placed.
                int samples = probe.Samples;
                while (probe.Samples == samples) yield return null;
                Assert.AreEqual(0f, probe.AngleToView, 0.001f, "the camera is aimed exactly at the view on every frame, whatever the head's animation does");
            }
        }

        /// <summary>
        /// Measures the camera against the view after the character controller has placed it this frame.
        /// </summary>
        [DefaultExecutionOrder(10000)]
        private sealed class CameraViewProbe : MonoBehaviour
        {
            public FirstPersonLook Look;
            public int Samples { get; private set; }
            public float AngleToView { get; private set; }

            private void LateUpdate()
            {
                if (Look == null) return;
                AngleToView = Quaternion.Angle(Quaternion.Euler(Look.Pitch, Look.Yaw, 0f), transform.rotation);
                Samples++;
            }
        }

        [UnityTest]
        public IEnumerator Camera_DoesNotFeedBackIntoTheView()
        {
            FirstPersonLook look = OwnerLook();
            look.Look(0.5f, 0.5f, 0.15f);
            float yaw = look.Yaw;
            float pitch = look.Pitch;

            yield return Seconds(1f);

            Assert.AreEqual(yaw, look.Yaw, 0.0001f, "with no input the view does not drift, whatever the camera does");
            Assert.AreEqual(pitch, look.Pitch, 0.0001f);
        }

        [UnityTest]
        public IEnumerator Camera_StaysOnTheRestEye_AtIdleAndWhileWalking()
        {
            yield return Seconds(0.5f);
            Vector3 idle = playerObject.transform.InverseTransformPoint(cameraObject.transform.position);

            Keyboard keyboard = InputSystem.AddDevice<Keyboard>();
            try
            {
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.W));
                yield return Seconds(1f);
                Assert.Greater(playerObject.GetComponent<CharacterController>().velocity.magnitude, 0.5f, "the character is walking");

                float headSpread = 0f;
                float cameraSpread = 0f;
                Vector3 headMin = Vector3.one * 100f, headMax = Vector3.one * -100f, camMin = headMin, camMax = headMax;
                float headRoll = 0f;
                int frames = 90;
                for (int i = 0; i < frames; i++)
                {
                    yield return null;
                    Vector3 head = playerObject.transform.InverseTransformPoint(Eye().position);
                    Vector3 cam = playerObject.transform.InverseTransformPoint(cameraObject.transform.position);
                    headMin = Vector3.Min(headMin, head); headMax = Vector3.Max(headMax, head);
                    camMin = Vector3.Min(camMin, cam); camMax = Vector3.Max(camMax, cam);
                    headRoll = Mathf.Max(headRoll, Quaternion.Angle(Quaternion.Euler(OwnerLook().Pitch, OwnerLook().Yaw, 0f), Eye().rotation));
                }

                headSpread = (headMax - headMin).magnitude;
                cameraSpread = (camMax - camMin).magnitude;
                Assert.Less(cameraSpread, 0.01f, "walking does not move the camera relative to the body");
                Assert.AreEqual(0f, Vector3.Distance(idle, playerObject.transform.InverseTransformPoint(cameraObject.transform.position)), 0.01f, "and it is where it was at rest");
            }
            finally
            {
                InputSystem.QueueStateEvent(keyboard, new KeyboardState());
                InputSystem.RemoveDevice(keyboard);
            }
        }

        [UnityTest]
        public IEnumerator Camera_LeansWithTheArticulation_ButNotWithTheAnimation()
        {
            yield return Seconds(0.5f);
            Vector3 rest = playerObject.transform.InverseTransformPoint(cameraObject.transform.position);

            // Look hard down: the torso bends over, which is deliberate motion the camera must show.
            FirstPersonLook look = OwnerLook();
            for (int i = 0; i < 60; i++)
            {
                look.Look(0f, 1f, 0.02f);
                yield return null;
            }

            Vector3 bent = playerObject.transform.InverseTransformPoint(cameraObject.transform.position);
            Assert.Greater(Vector3.Distance(rest, bent), 0.05f, "bending over with the view moves the eye");
        }

        // Measurement, not a regression test: writes where the guard, middle and tip of the blade are in camera space through a chained combo.
        [UnityTest]
        public IEnumerator Probe_SwordInCameraSpace()
        {
            AbilityDefinition melee = UnityEditor.AssetDatabase.LoadAssetAtPath<AbilityDefinition>(MeleeAttackPath);
            Transform model = sword.transform.Find("Longsword Model");
            AbilityUser user = playerObject.GetComponent<AbilityUser>();
            Camera cam = cameraObject.GetComponent<Camera>();
            var lines = new System.Text.StringBuilder();
            lines.AppendLine("t,hit,guard_x,guard_y,guard_z,mid_x,mid_y,mid_z,tip_x,tip_y,tip_z");
            yield return Seconds(0.5f);
            float start = Time.time;
            float next = 0f;
            while (Time.time - start < 6f)
            {
                float t = Time.time - start;
                if (t >= next && next <= 5.5f && (next == 0f || Mathf.Repeat(next, 1.3f) < 0.06f)) user.TryActivateOnServer(melee);
                Vector3 g = cam.transform.InverseTransformPoint(model.position);
                Vector3 m = cam.transform.InverseTransformPoint(model.TransformPoint(new Vector3(0f, 0.00613f, 0f)));
                Vector3 e = cam.transform.InverseTransformPoint(model.TransformPoint(new Vector3(0f, 0.01226f, 0f)));
                bool live = sword.gameObject.activeInHierarchy && sword.enabled;
                lines.AppendLine($"{t:F3},{(live ? 1 : 0)},{g.x:F3},{g.y:F3},{g.z:F3},{m.x:F3},{m.y:F3},{m.z:F3},{e.x:F3},{e.y:F3},{e.z:F3}");
                if (t >= next) next += 0.05f;
                yield return null;
            }

            System.IO.File.WriteAllText(System.Environment.GetEnvironmentVariable("SWORD_PROBE_OUT") ?? "C:/Users/ALEXVA~1/AppData/Local/Temp/claude/I--Unity-Projects-SimpleRPG/368dfdec-0804-4c41-a1ee-87bb5dfba007/scratchpad/sword.csv", lines.ToString());
        }

        [UnityTest]
        public IEnumerator Camera_UsesTheShippedNearPlane_AndAnEyeInsideTheHead()
        {
            yield return Frames(2);

            Assert.AreEqual(0.04f, cameraObject.GetComponent<Camera>().nearClipPlane, 0.0001f);
            Vector3 inHead = HeadBone().InverseTransformPoint(Eye().position);
            Assert.That(inHead.y, Is.InRange(0.05f, 0.2f), "the eye is above the head bone's pivot");
            Assert.That(inHead.z, Is.InRange(0.03f, 0.14f), "and in front of it, behind the face");
            Assert.AreEqual(0f, inHead.x, 0.001f, "and centred");
        }

        [UnityTest]
        public IEnumerator Eye_ComesBackToWhereItWasOnceTheViewIsLevel()
        {
            yield return Seconds(0.5f);
            Vector3 rest = playerObject.transform.InverseTransformPoint(Eye().position);

            FirstPersonLook look = OwnerLook();
            for (int round = 0; round < 3; round++)
            {
                look.Look(0f, 1f, 0.15f);
                yield return Seconds(0.3f);
                look.Look(0f, -2f, 0.15f);
                yield return Seconds(0.3f);
                for (int guard = 0; guard < 5000 && Mathf.Abs(look.Pitch) > 0.05f; guard++) look.Look(0f, -Mathf.Sign(look.Pitch), 0.0005f);
                yield return Seconds(0.8f);
            }

            Vector3 after = playerObject.transform.InverseTransformPoint(Eye().position);
            Assert.AreEqual(0f, Vector3.Distance(rest, after), 0.05f, "nothing was added to the eye that stays when the view returns; only the idle animation moves it");
        }

        /// <summary>
        /// Plays the shipped first attack on the real rig and animator. The view is either held or swung hard during the windup; the sample is the
        /// blade's world position and whether the hit window is open a fixed time after the blade went live.
        /// </summary>
        private IEnumerator Swing(float viewTurnDuringWindup, System.Action<Vector3, double, bool> result)
        {
            AbilityDefinition melee = UnityEditor.AssetDatabase.LoadAssetAtPath<AbilityDefinition>(MeleeAttackPath);
            Assert.IsNotNull(melee);
            yield return Seconds(0.3f);
            float pressedAt = Time.time;
            Assert.IsTrue(playerObject.GetComponent<AbilityUser>().TryActivateOnServer(melee), "the attack starts");

            // Let the windup get going, then turn.
            yield return Seconds(0.5f);
            if (viewTurnDuringWindup != 0f) Look(viewTurnDuringWindup, 0f);

            float opened = -1f;
            float deadline = Time.time + 5f;
            while (opened < 0f && Time.time < deadline)
            {
                if (sword.enabled) opened = Time.time - pressedAt;
                yield return null;
            }

            Assert.GreaterOrEqual(opened, 0f, "the hit window opens");
            yield return Seconds(0.12f);
            result(sword.transform.position - playerObject.transform.position, opened, sword.enabled);
        }

        [UnityTest]
        public IEnumerator TurningDuringTheSwing_ChangesTheBladesWorldPath_NotWhenTheWindowOpens()
        {
            Vector3 held = default;
            double heldOpened = 0;
            bool heldLive = false;
            yield return Swing(0f, (p, t, live) => { held = p; heldOpened = t; heldLive = live; });
            yield return Seconds(3f);

            // A second, identical swing with the view swung 80 degrees.
            Vector3 turned = default;
            double turnedOpened = 0;
            bool turnedLive = false;
            yield return Swing(80f, (p, t, live) => { turned = p; turnedOpened = t; turnedLive = live; });

            Assert.IsTrue(heldLive && turnedLive, "the blade is live in both");
            Assert.AreEqual(heldOpened, turnedOpened, 0.1, "the authored timing is untouched: the window opens when it always does");
            Assert.Greater(Vector3.Distance(held, turned), 0.3f, "but the blade is somewhere else in the world: the swing was dragged");
        }

        [UnityTest]
        public IEnumerator Eye_LeansForwardAndDownWhenLookingDown_AndBackWhenLookingUp()
        {
            yield return Frames(3);
            Vector3 rest = playerObject.transform.InverseTransformPoint(cameraObject.transform.position);

            FirstPersonLook look = OwnerLook();
            look.Look(0f, 1f, 0.15f);
            yield return Seconds(0.4f);
            Vector3 down = playerObject.transform.InverseTransformPoint(cameraObject.transform.position);

            look.Look(0f, -2f, 0.15f);
            yield return Seconds(0.4f);
            Vector3 up = playerObject.transform.InverseTransformPoint(cameraObject.transform.position);

            Assert.Greater(down.z, rest.z + 0.03f, "looking down, the eye moves forward");
            Assert.Less(down.y, rest.y - 0.01f, "and lower");
            Assert.Less(up.z, rest.z - 0.01f, "looking up, the eye moves back");
        }
    }
}
