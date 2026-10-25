using System.Collections.Generic;
using UnityEngine;

namespace SimpleRPG.Audio
{
    public enum Foot
    {
        Left = 0,
        Right = 1,
    }

    /// <summary>
    /// Notices a foot touching down from the foot's height above the character's root: down when it drops to the contact height, up again
    /// only after it rises past the release height, so one footfall is one contact.
    /// </summary>
    public struct FootContactDetector
    {
        private bool down;

        /// <summary>
        /// Feeds one frame's foot height. Returns true on the frame the foot lands.
        /// </summary>
        public bool Update(float height, float contactHeight, float releaseHeight)
        {
            if (!down && height <= contactHeight)
            {
                down = true;
                return true;
            }

            if (down && height >= releaseHeight)
            {
                down = false;
            }

            return false;
        }
    }

    /// <summary>
    /// Plays a character's footsteps from real foot contacts. A contact comes from an animation event (<see cref="OnFootDown"/>, int parameter 0
    /// for left and 1 for right, authored on the locomotion clips) or, for rigs without events, from watching the foot bones. The step finds the
    /// surface under the foot, adds the footwear and how hard the character is moving, and asks the footstep table for the sound; shallow water
    /// or other overlays, the object's own contact sounds and the equipment layers join it. It runs on each machine from its own animation, so
    /// no step is ever sent over the network and none can play twice.
    /// </summary>
    public sealed class FootstepEmitter : MonoBehaviour
    {
        [SerializeField]
        [Tooltip("Footstep table. Leave empty to use the project default from the routing asset.")]
        private AudioEventBase footsteps;

        [Header("Foot contact")]
        [SerializeField] private Transform leftFoot;
        [SerializeField] private Transform rightFoot;

        [SerializeField]
        [Tooltip("Detect contacts from the foot bones' height. Leave off when the locomotion clips carry foot-down events.")]
        private bool detectFromBones;

        [SerializeField]
        [Tooltip("How far a planted foot bone sits below the character's root, in meters (0 when the root is at the soles). Measured from the rig: the lowest the foot bone gets relative to the root.")]
        private float rootToSoleOffset;

        [SerializeField, Min(0f)]
        [Tooltip("A foot lower than this above the sole counts as touching down.")]
        private float contactHeight = 0.04f;

        [SerializeField, Min(0f)]
        [Tooltip("A touching foot must rise past this before it can touch down again.")]
        private float releaseHeight = 0.09f;

        [SerializeField, Min(0f)]
        [Tooltip("Foot-bone contacts only count while the character moves at least this fast, so a standing character makes no steps.")]
        private float minBoneSpeed = 0.5f;

        [Header("Step")]
        [SerializeField]
        [Tooltip("Movement speed that counts as full intensity.")]
        private float fullIntensitySpeed = 5f;

        [SerializeField]
        [Tooltip("Shortest time between two steps of the same foot. Blended animations can fire an event from each clip.")]
        private float minStepInterval = 0.12f;

        [SerializeField] private LayerMask groundMask = ~0;

        private readonly float[] lastStepTime = { float.NegativeInfinity, float.NegativeInfinity };
        private readonly List<AudioEventBase> scratch = new List<AudioEventBase>(4);
        private FootContactDetector leftDetector;
        private FootContactDetector rightDetector;
        private MovementAudioState movement;
        private IAudioRandom random = new SystemAudioRandom();
        private Vector3 lastPosition;
        private float speed;
        private bool feetResolved;

        /// <summary>Raised for every step that is taken, whether or not its sound exists.</summary>
        public event System.Action<Foot> Stepped;

        public AudioEventBase Footsteps
        {
            get => footsteps;
            set => footsteps = value;
        }

        /// <summary>How hard the character is moving, 0 to 1.</summary>
        public float Intensity => fullIntensitySpeed > 0f ? Mathf.Clamp01(speed / fullIntensitySpeed) : 0f;

        private void Awake()
        {
            movement = GetComponent<MovementAudioState>();
            lastPosition = transform.position;
        }

        // A humanoid rig knows its own feet. Resolved on first use, not in Awake, so settings applied after the component is added count.
        private void ResolveFeet()
        {
            feetResolved = true;
            if (detectFromBones && (leftFoot == null || rightFoot == null) && TryGetComponent(out Animator animator) && animator.isHuman)
            {
                if (leftFoot == null) leftFoot = animator.GetBoneTransform(HumanBodyBones.LeftFoot);
                if (rightFoot == null) rightFoot = animator.GetBoneTransform(HumanBodyBones.RightFoot);
            }
        }

        public void Configure(Transform left, Transform right, bool fromBones)
        {
            leftFoot = left;
            rightFoot = right;
            detectFromBones = fromBones;
        }

        private void Update()
        {
            float delta = Time.deltaTime;
            if (delta > 0f)
            {
                // Measured from the transform so it also works for remote characters, whose velocity is only the network's smoothed position.
                float measured = (transform.position - lastPosition).magnitude / delta;
                speed = Mathf.Lerp(speed, measured, 0.5f);
            }

            lastPosition = transform.position;

            if (detectFromBones)
            {
                if (!feetResolved) ResolveFeet();

                // The detectors keep tracking while standing so the first stride after standing still is not a double step.
                float soleHeight = transform.position.y - rootToSoleOffset;
                bool moving = speed >= minBoneSpeed;
                if (leftFoot != null && leftDetector.Update(leftFoot.position.y - soleHeight, contactHeight, releaseHeight) && moving)
                {
                    FootDown(Foot.Left);
                }

                if (rightFoot != null && rightDetector.Update(rightFoot.position.y - soleHeight, contactHeight, releaseHeight) && moving)
                {
                    FootDown(Foot.Right);
                }
            }
        }

        /// <summary>
        /// Animation event receiver. Set the event's function to OnFootDown and its Int parameter to 0 (left) or 1 (right).
        /// </summary>
        public void OnFootDown(int foot) => FootDown(foot == 0 ? Foot.Left : Foot.Right);

        public void FootDown(Foot foot)
        {
            Transform bone = foot == Foot.Left ? leftFoot : rightFoot;
            Vector3 position = bone != null ? bone.position : transform.position;
            float now = Time.unscaledTime;
            if (now - lastStepTime[(int)foot] < minStepInterval)
            {
                return;
            }

            lastStepTime[(int)foot] = now;
            Stepped?.Invoke(foot);
            SurfaceSample sample = SurfaceQuery.Below(position, 0.8f, groundMask, transform);
            Step(position, sample, Intensity, now);
        }

        /// <summary>
        /// Plays one footstep on a known surface. Public so a test or a scripted step can supply the surface.
        /// </summary>
        public void Step(Vector3 position, in SurfaceSample sample, float intensity, float now)
        {
            AudioEventBase table = footsteps != null ? footsteps : (AudioRouting.Instance != null ? AudioRouting.Instance.DefaultFootstepTable : null);
            AudioContext context = BuildContext(sample.Surface, movement, intensity);

            if (table != null)
            {
                GameAudio.Play(table, context, position);
                if (sample.Overlay != null)
                {
                    GameAudio.Play(table, context.With(AudioFacet.Surface, sample.Overlay), position);
                }
            }

            SurfaceQuery.PlayContactSounds(sample.BaseSource, AudioContactKind.Footstep, context, position, random);
            SurfaceQuery.PlayContactSounds(sample.OverlaySource, AudioContactKind.Footstep, context, position, random);

            if (movement != null)
            {
                List<AudioEventBase> layers = movement.PickStepLayers(intensity, now, random);
                scratch.Clear();
                scratch.AddRange(layers);
                for (int i = 0; i < scratch.Count; i++)
                {
                    GameAudio.PlayAttached(scratch[i], context, transform);
                }
            }
        }

        /// <summary>
        /// The context a step is resolved against.
        /// </summary>
        public static AudioContext BuildContext(AudioTag surface, MovementAudioState state, float intensity)
        {
            return new AudioContext
            {
                Surface = surface,
                Footwear = state != null ? state.Footwear : null,
                Armor = state != null ? state.Armor : null,
                Intensity = intensity,
            };
        }
    }
}
