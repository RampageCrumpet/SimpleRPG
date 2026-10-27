using System;
using Unity.Netcode;
using UnityEngine;

namespace SimpleRPG.Articulation
{
    /// <summary>
    /// The torso twist and tilt as they travel over the network: the twist relative to the pelvis, the tilt and the pelvis's own world yaw, in
    /// hundredths of a degree. The pelvis yaw also rides the NetworkTransform; it is repeated here so the owner can check its prediction against it.
    /// </summary>
    public struct ArticulationPose : INetworkSerializable, IEquatable<ArticulationPose>
    {
        public short Twist;
        public short Pitch;
        public short PelvisYaw;

        public float TwistDegrees => Twist * 0.01f;

        public float PitchDegrees => Pitch * 0.01f;

        public float PelvisYawDegrees => PelvisYaw * 0.01f;

        public static ArticulationPose From(float twistDegrees, float pitchDegrees, float pelvisYawDegrees = 0f)
        {
            return new ArticulationPose
            {
                Twist = (short)Mathf.Clamp(Mathf.RoundToInt(twistDegrees * 100f), short.MinValue, short.MaxValue),
                Pitch = (short)Mathf.Clamp(Mathf.RoundToInt(pitchDegrees * 100f), short.MinValue, short.MaxValue),
                PelvisYaw = (short)Mathf.Clamp(Mathf.RoundToInt(Mathf.DeltaAngle(0f, pelvisYawDegrees) * 100f), short.MinValue, short.MaxValue),
            };
        }

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref Twist);
            serializer.SerializeValue(ref Pitch);
            serializer.SerializeValue(ref PelvisYaw);
        }

        public bool Equals(ArticulationPose other) => Twist == other.Twist && Pitch == other.Pitch && PelvisYaw == other.PelvisYaw;
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
    /// The owning client (when it is not the server) predicts its whole solution with the same solver, pelvis included: it turns its own copy of
    /// the character root each frame, over what the NetworkTransform last wrote, so the torso, the weapon and the camera answer the mouse at once.
    /// The prediction chases absolute targets rather than integrating, so it cannot accumulate error. Only the root's yaw is predicted: nobody
    /// else sees it, and the server's pose is the one hits are tested against. If the owner has been still long enough for the server to have
    /// caught up and the server's pelvis still disagrees, the server holds a different view than the owner, and the owner sends it the absolute
    /// yaw.
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

        // Owner prediction: when the view last moved, and when the prediction last told the server where the owner really looks.
        private float lastLookTime = float.NegativeInfinity;
        private float lastResyncTime = float.NegativeInfinity;
        private float lastPoseChangeTime = float.NegativeInfinity;
        private ArticulationPose lastPublished;

        // What other players see, eased toward the replicated value.
        private float remoteTwist;
        private float remotePitch;

        // 1 when alive, 0 when dead; the pose fades with it.
        private float poseWeight = 1f;

        // The pose last applied, for tests and tools.
        public float AppliedTwist { get; private set; }

        public float AppliedPitch { get; private set; }

        /// <summary>
        /// How far this frame's twist and tilt moved the tracked point (see <see cref="TrackPoint"/>) from where the animation alone left it, in
        /// world space. This is the deliberate part of the point's motion; everything else the animation does to it is the clip's own.
        /// </summary>
        public Vector3 TrackedDisplacement { get; private set; }

        private Transform trackedPoint;

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

        /// <summary>How many times the owner has had to tell the server where it really looks. Zero in normal play.</summary>
        public int ViewResyncCount { get; private set; }

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
            if (!rig.IsUsable)
            {
                Debug.LogWarning(name + " has no humanoid spine bones, so its upper body will not follow the view.", this);
            }
        }

        public override void OnNetworkSpawn()
        {
            character.OnRevive += HandleRevive;
            networkPose.OnValueChanged += HandlePoseChanged;
            InitialiseView(transform.eulerAngles.y, 0f);

            if (!IsServer)
            {
                remoteTwist = networkPose.Value.TwistDegrees;
                remotePitch = networkPose.Value.PitchDegrees;
            }
        }

        public override void OnNetworkDespawn()
        {
            networkPose.OnValueChanged -= HandlePoseChanged;
            if (character != null)
            {
                character.OnRevive -= HandleRevive;
            }
        }

        /// <summary>
        /// Asks for <see cref="TrackedDisplacement"/> of a transform under the spine, such as the eye. Valid after this component's LateUpdate.
        /// </summary>
        public void TrackPoint(Transform point)
        {
            trackedPoint = point;
        }

        private void HandlePoseChanged(ArticulationPose previous, ArticulationPose current)
        {
            lastPoseChangeTime = Time.time;
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
            if (yawDelta != 0f || !Mathf.Approximately(pitch, lastSentPitch))
            {
                lastLookTime = Time.time;
            }

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

        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
        private void ResyncViewServerRpc(float yaw)
        {
            // The same information the yaw deltas carry, as an absolute. It moves the view, never the body, which still chases it at the phase's speed.
            if (!character.IsAlive || float.IsNaN(yaw) || float.IsInfinity(yaw))
            {
                return;
            }

            viewYaw = Mathf.Repeat(yaw + 180f, 360f) - 180f;
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

            ArticulationPose pose = ArticulationPose.From(solver.Twist, solver.TorsoPitch, solver.PelvisYaw);
            if (Mathf.Abs(pose.TwistDegrees - lastPublished.TwistDegrees) >= settings.replicationThreshold
                || Mathf.Abs(pose.PitchDegrees - lastPublished.PitchDegrees) >= settings.replicationThreshold
                || Mathf.Abs(Mathf.DeltaAngle(pose.PelvisYawDegrees, lastPublished.PelvisYawDegrees)) >= settings.replicationThreshold)
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
                CheckAgainstServer();
            }
        }

        // The prediction and the server run the same solver on the same view, so once the view and the server's pose have been still for longer than
        // a round trip the two pelvises must agree. If they do not, the server was told a different view (a lost or clamped message): tell it where the owner looks.
        private void CheckAgainstServer()
        {
            float now = Time.time;
            // The server has had its say once its pose has changed since the view last moved and then held still. If it never changed at all (the view
            // moved too little to matter, or the message was lost) wait out the longer silence instead.
            float quietSince = Mathf.Max(lastLookTime, lastPoseChangeTime);
            float wait = lastPoseChangeTime >= lastLookTime ? settings.reconcileStillSeconds : settings.reconcileSilenceSeconds;
            bool stillLongEnough = now - quietSince >= wait;
            bool settled = Mathf.Abs(Mathf.DeltaAngle(solver.PelvisYaw, viewYaw)) < 0.01f;
            bool throttled = now - lastResyncTime < settings.reconcileSilenceSeconds;
            if (!stillLongEnough || !settled || throttled)
            {
                return;
            }

            if (Mathf.Abs(Mathf.DeltaAngle(solver.PelvisYaw, networkPose.Value.PelvisYawDegrees)) > settings.reconcileThreshold)
            {
                lastResyncTime = now;
                ViewResyncCount++;
                ResyncViewServerRpc(viewYaw);
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
                // The prediction's own pelvis, not the replicated one, so the root and the torso twist agree with the view the owner just gave.
                if (character.IsAlive)
                {
                    transform.rotation = Quaternion.Euler(0f, solver.PelvisYaw, 0f);
                }

                twist = solver.Twist;
                pitch = solver.TorsoPitch;
            }
            else
            {
                twist = remoteTwist;
                pitch = remotePitch;
            }

            AppliedTwist = twist * poseWeight;
            AppliedPitch = pitch * poseWeight;
            rig.Apply(transform, AppliedTwist, AppliedPitch, trackedPoint, out Vector3 displacement);
            TrackedDisplacement = displacement;
        }
    }
}
