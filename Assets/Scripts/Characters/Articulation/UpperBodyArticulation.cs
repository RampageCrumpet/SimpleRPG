using System;
using Unity.Netcode;
using UnityEngine;

namespace SimpleRPG.Articulation
{
    /// <summary>
    /// The torso twist and tilt as they travel over the network: the twist relative to the pelvis and the tilt, in hundredths of a degree.
    /// </summary>
    public struct ArticulationPose : INetworkSerializable, IEquatable<ArticulationPose>
    {
        public short Twist;
        public short Pitch;

        public float TwistDegrees => Twist * 0.01f;

        public float PitchDegrees => Pitch * 0.01f;

        public static ArticulationPose From(float twistDegrees, float pitchDegrees)
        {
            return new ArticulationPose
            {
                Twist = (short)Mathf.Clamp(Mathf.RoundToInt(twistDegrees * 100f), short.MinValue, short.MaxValue),
                Pitch = (short)Mathf.Clamp(Mathf.RoundToInt(pitchDegrees * 100f), short.MinValue, short.MaxValue),
            };
        }

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref Twist);
            serializer.SerializeValue(ref Pitch);
        }

        public bool Equals(ArticulationPose other) => Twist == other.Twist && Pitch == other.Pitch;
    }

    /// <summary>
    /// Makes a humanoid character's upper body follow the player's view, on top of whatever the animator is playing.
    /// <para>
    /// <b>Authority.</b> The server owns the pose. The owning client sends only where it looks (a yaw change and a pitch); the server steps an
    /// <see cref="UpperBodySolver"/> with its own attack phase, so a client can't loosen the speed limits by sending anything. The server turns the
    /// character root (the pelvis), which replicates through the existing NetworkTransform, and writes the torso twist and tilt to one small
    /// <see cref="NetworkVariable{T}"/> that everyone else poses their spine from. The server's own pose is the one weapon hits are tested against.
    /// </para>
    /// <para>
    /// <b>Pipeline.</b> The animator evaluates after Update; this poses the spine in LateUpdate, after it, and before rendering. Physics reads the
    /// transforms at the start of the next fixed step, so the blade and the hurtboxes, which ride bones below the spine, are tested where they were drawn.
    /// </para>
    /// <para>
    /// The owning client (when it is not the server) predicts its own torso with the same solver so the weapon answers the mouse at once; its pelvis
    /// is still the server's, so its pose is only as current as the root's replication (see bead simplerpg-7du.1.6).
    /// </para>
    /// </summary>
    [RequireComponent(typeof(Animator))]
    [RequireComponent(typeof(Character))]
    public sealed class UpperBodyArticulation : NetworkBehaviour
    {
        [SerializeField]
        private ArticulationProfile profile;

        private readonly NetworkVariable<ArticulationPose> networkPose = new NetworkVariable<ArticulationPose>();

        private ArticulationSettings settings;
        private UpperBodySolver solver;
        private SpineRig rig;
        private Character character;
        private IArticulationPhaseSource phaseSource;

        // Where this machine believes the player is looking. On the server it is built from the owner's messages; on the owning client from the mouse.
        private float viewYaw;
        private float viewPitch;
        private bool viewInitialised;

        private float lastSentYaw;
        private float lastSentPitch;
        private ArticulationPose lastPublished;

        // What other players see, eased toward the replicated value.
        private float remoteTwist;
        private float remotePitch;

        // 1 when alive, 0 when dead; the pose fades with it.
        private float poseWeight = 1f;

        // The pose last applied, for tests and tools.
        public float AppliedTwist { get; private set; }

        public float AppliedPitch { get; private set; }

        /// <summary>The head bone, or null if the rig has none.</summary>
        public Transform Head { get; private set; }

        /// <summary>Where the head was (its local-to-world matrix, so any rig scale is included) this frame after the animator and before the spine was bent, so the bend alone can be measured.</summary>
        public Matrix4x4 HeadToWorldBeforeBend { get; private set; }

        /// <summary>
        /// Raised on the owning client when the server moves the view, such as on respawn, so the camera can follow. The float is the new yaw.
        /// </summary>
        public event Action<float> ViewReset;

        public ArticulationSettings Settings => settings;

        /// <summary>The solver this machine steps. Null before spawn.</summary>
        public UpperBodySolver Solver => solver;

        /// <summary>Where this machine believes the player looks, as a world yaw.</summary>
        public float ViewYaw => viewYaw;

        public float ViewPitch => viewPitch;

        private void Awake()
        {
            character = GetComponent<Character>();
            phaseSource = GetComponent<IArticulationPhaseSource>();
            settings = profile != null ? profile.Settings : new ArticulationSettings();
            solver = new UpperBodySolver(settings);
        }

        private void Start()
        {
            Animator animator = GetComponent<Animator>();
            rig = new SpineRig(animator, settings);
            Head = animator.isHuman ? animator.GetBoneTransform(HumanBodyBones.Head) : null;
            if (!rig.IsUsable)
            {
                Debug.LogWarning(name + " has no humanoid spine bones, so its upper body will not follow the view.", this);
            }
        }

        public override void OnNetworkSpawn()
        {
            character.OnRevive += HandleRevive;
            InitialiseView(transform.eulerAngles.y, 0f);

            if (!IsServer)
            {
                remoteTwist = networkPose.Value.TwistDegrees;
                remotePitch = networkPose.Value.PitchDegrees;
            }
        }

        public override void OnNetworkDespawn()
        {
            if (character != null)
            {
                character.OnRevive -= HandleRevive;
            }
        }

        // ----- Look input -----

        /// <summary>
        /// The owning player's view moved. Sent by the owner's look controller every frame; the server decides what the body does about it.
        /// </summary>
        /// <param name="yawDelta">Degrees the view turned since the last call.</param>
        /// <param name="pitch">Where the view now points vertically (positive down).</param>
        public void SubmitLook(float yawDelta, float pitch)
        {
            if (!IsSpawned || !IsOwner)
            {
                return;
            }

            if (IsServer)
            {
                if (character.IsAlive)
                {
                    ApplyLook(yawDelta, pitch);
                }

                return;
            }

            // The owning client tracks its own view for its prediction, and only talks to the server when the view actually changed.
            ApplyLook(yawDelta, pitch);
            lastSentYaw += yawDelta;
            if (Mathf.Abs(lastSentYaw) > 0f || !Mathf.Approximately(pitch, lastSentPitch))
            {
                SubmitLookServerRpc(lastSentYaw, pitch);
                lastSentYaw = 0f;
                lastSentPitch = pitch;
            }
        }

        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
        private void SubmitLookServerRpc(float yawDelta, float pitch)
        {
            // A dead character neither turns nor aims; the server decides, whatever the client sends.
            if (!character.IsAlive)
            {
                return;
            }

            ApplyLook(yawDelta, pitch);
        }

        private void ApplyLook(float yawDelta, float pitch)
        {
            if (float.IsNaN(yawDelta) || float.IsInfinity(yawDelta) || float.IsNaN(pitch) || float.IsInfinity(pitch))
            {
                return;
            }

            yawDelta = Mathf.Clamp(yawDelta, -settings.maxYawPerMessage, settings.maxYawPerMessage);
            viewYaw = Mathf.Repeat(viewYaw + yawDelta + 180f, 360f) - 180f;
            viewPitch = Mathf.Clamp(pitch, -90f, 90f);
        }

        private void InitialiseView(float yaw, float pitch)
        {
            viewYaw = yaw;
            viewPitch = pitch;
            viewInitialised = true;
            solver.Reset(yaw, pitch);
            lastSentYaw = 0f;
            lastSentPitch = pitch;
        }

        private void HandleRevive()
        {
            // A new life starts looking where the respawn point faces.
            if (IsServer)
            {
                InitialiseView(transform.eulerAngles.y, 0f);
                ResetViewClientRpc(viewYaw);
            }
        }

        [Rpc(SendTo.Owner)]
        private void ResetViewClientRpc(float yaw)
        {
            if (IsServer)
            {
                ViewReset?.Invoke(yaw);
                return;
            }

            InitialiseView(yaw, 0f);
            ViewReset?.Invoke(yaw);
        }

        // ----- Stepping -----

        private void Update()
        {
            if (!IsSpawned || !viewInitialised)
            {
                return;
            }

            float deltaTime = Time.deltaTime;
            float fadeStep = settings.deathFadeSeconds > 0f ? deltaTime / settings.deathFadeSeconds : 1f;
            poseWeight = Mathf.MoveTowards(poseWeight, character.IsAlive ? 1f : 0f, fadeStep);

            if (IsServer)
            {
                StepServer(deltaTime);
            }
            else if (IsOwner)
            {
                StepOwner(deltaTime);
            }
            else
            {
                float ease = settings.remoteSmoothingSeconds <= 0f ? 1f : 1f - Mathf.Exp(-deltaTime / settings.remoteSmoothingSeconds);
                remoteTwist = Mathf.Lerp(remoteTwist, networkPose.Value.TwistDegrees, ease);
                remotePitch = Mathf.Lerp(remotePitch, networkPose.Value.PitchDegrees, ease);
            }
        }

        private ArticulationPhase CurrentPhase => phaseSource != null ? phaseSource.CurrentPhase : ArticulationPhase.Free;

        private void StepServer(float deltaTime)
        {
            if (character.IsAlive)
            {
                solver.Step(viewYaw, viewPitch, CurrentPhase, deltaTime);
                transform.rotation = Quaternion.Euler(0f, solver.PelvisYaw, 0f);
            }

            ArticulationPose pose = ArticulationPose.From(solver.Twist, solver.TorsoPitch);
            if (Mathf.Abs(pose.TwistDegrees - lastPublished.TwistDegrees) >= settings.replicationThreshold
                || Mathf.Abs(pose.PitchDegrees - lastPublished.PitchDegrees) >= settings.replicationThreshold)
            {
                lastPublished = pose;
                networkPose.Value = pose;
            }
        }

        private void StepOwner(float deltaTime)
        {
            if (character.IsAlive)
            {
                solver.Step(viewYaw, viewPitch, CurrentPhase, deltaTime);
            }
        }

        private void LateUpdate()
        {
            if (!IsSpawned || rig == null || !rig.IsUsable)
            {
                return;
            }

            float twist;
            float pitch;
            if (IsServer)
            {
                twist = solver.Twist;
                pitch = solver.TorsoPitch;
            }
            else if (IsOwner)
            {
                twist = solver.TwistOn(transform.eulerAngles.y);
                pitch = solver.TorsoPitch;
            }
            else
            {
                twist = remoteTwist;
                pitch = remotePitch;
            }

            AppliedTwist = twist * poseWeight;
            AppliedPitch = pitch * poseWeight;
            if (Head != null)
            {
                // Measured before Apply, with the previous frame's bend undone first, so the difference is only this frame's bend.
                rig.Restore();
                HeadToWorldBeforeBend = Head.localToWorldMatrix;
            }

            rig.Apply(transform, AppliedTwist, AppliedPitch);
        }
    }
}
