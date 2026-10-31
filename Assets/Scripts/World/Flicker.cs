using UnityEngine;
using UnityEngine.Rendering;

namespace SimpleRPG
{
    /// <summary>
    /// Makes a Light burn like a flame: its intensity, colour, range and position drift on layered noise. Torches, braziers, candles and
    /// lanterns all use it with different numbers.
    /// </summary>
    /// <remarks>
    /// The flicker is cosmetic and runs on each client alone. It reads no random numbers and no network state: the pattern comes from the
    /// current time and a per-light seed taken from the light's world position, so neighbouring torches burn out of step yet the same torch
    /// burns the same way on every machine. A dedicated server (no graphics device) switches it off.
    /// </remarks>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Light))]
    public sealed class Flicker : MonoBehaviour
    {
        private const float NoiseLow = 0.25f;
        private const float NoiseHigh = 0.75f;

        [Header("Lighting")]

        [Tooltip("Whether the light is burning when it appears. Off leaves it dark until Ignite or SetLit(true).")]
        [SerializeField] private bool startLit = true;

        [Tooltip("Seconds the light takes to fade up when ignited. 0 is instant.")]
        [Min(0f)]
        [SerializeField] private float fadeInSeconds = 0.4f;

        [Tooltip("Seconds the light takes to fade out when extinguished. 0 is instant.")]
        [Min(0f)]
        [SerializeField] private float fadeOutSeconds = 0.8f;

        [Header("Intensity")]

        [Tooltip("Intensity multiplier at the dimmest point of the flicker. 1 is the light's own intensity.")]
        [SerializeField] private float minIntensity = 0.85f;

        [Tooltip("Intensity multiplier at the brightest point of the flicker.")]
        [SerializeField] private float maxIntensity = 1.08f;

        [Header("Motion")]

        [Tooltip("Swells per second of the slow drift that gives the flame its breathing.")]
        [SerializeField] private float swaySpeed = 0.7f;

        [Tooltip("Flickers per second of the fast crackle laid over the sway.")]
        [SerializeField] private float crackleSpeed = 3.5f;

        [Tooltip("How much of the flicker is crackle rather than sway, from 0 (a slow breathing glow) to 1 (all crackle).")]
        [Range(0f, 1f)]
        [SerializeField] private float crackleAmount = 0.3f;

        [Tooltip("Seconds the flame takes to follow the noise. Higher is smoother and calmer; 0 follows the noise exactly.")]
        [Min(0f)]
        [SerializeField] private float smoothing = 0.12f;

        [Header("Colour")]

        [Tooltip("Colour tint when the flame is at its dimmest. Leave white to skip tinting.")]
        [SerializeField] private Color dimTint = new Color(0.95f, 0.5f, 0.2f, 1f);

        [Tooltip("Colour tint when the flame is at its brightest.")]
        [SerializeField] private Color brightTint = new Color(1f, 0.68f, 0.34f, 1f);

        [Tooltip("Tint the light between the two colours with the flicker. Off keeps the light's own colour.")]
        [SerializeField] private bool tintWithFlicker = true;

        [Header("Shape")]

        [Tooltip("How far the light's range swells and shrinks with the flicker, as a fraction of its range. The glow edge on the walls breathes.")]
        [Range(0f, 0.3f)]
        [SerializeField] private float rangeVariation = 0.06f;

        [Tooltip("Radius in metres the light wanders around where it sits. Moves the shadows slightly, as a dancing flame does.")]
        [Min(0f)]
        [SerializeField] private float wander = 0.03f;

        [Header("Seed")]

        [Tooltip("Derive the seed from the light's world position so each placed torch burns differently. Off uses the seed below.")]
        [SerializeField] private bool seedFromPosition = true;

        [Tooltip("Seed used when it is not taken from the world position.")]
        [SerializeField] private float seed;

        private Light flickerLight;
        private bool captured;
        private float baseIntensity;
        private float baseRange;
        private Color baseColor;
        private Vector3 baseLocalPosition;
        private float activeSeed;
        private float smoothedFlame;
        private bool smoothedReady;
        private bool baseLightEnabled;
        private bool lit;
        private float fade;

        /// <summary>Whether the light is burning or fading up. False once it is put out, even while it is still fading down.</summary>
        public bool IsLit => lit;

        /// <summary>True once the light has fully faded out and its Light component is switched off.</summary>
        public bool IsDark => captured && !lit && fade <= 0f;

        /// <summary>Lights the flame, fading up over <c>fadeInSeconds</c>.</summary>
        public void Ignite() => SetLit(true);

        /// <summary>Puts the flame out, fading down over <c>fadeOutSeconds</c>.</summary>
        public void Extinguish() => SetLit(false);

        /// <summary>
        /// Lights or puts out the flame. This is the local visual only: whatever owns the light source's state (an interactable, replicated
        /// from the server) calls it when that state changes.
        /// </summary>
        public void SetLit(bool value)
        {
            lit = value;
            if (value && captured)
            {
                flickerLight.enabled = true;
            }
        }

        /// <summary>
        /// A flame value between 0 (dimmest) and 1 (brightest) for the given time. Slow sway and fast crackle are mixed by
        /// <paramref name="crackleAmount"/>, and the lights are told apart by <paramref name="seed"/>.
        /// </summary>
        public static float Sample(float time, float seed, float swaySpeed, float crackleSpeed, float crackleAmount)
        {
            float sway = Mathf.PerlinNoise(seed, time * swaySpeed);
            float crackle = Mathf.PerlinNoise(seed + 37.1f, time * crackleSpeed);
            float mixed = Mathf.Lerp(sway, crackle, crackleAmount);
            // Perlin noise clusters around the middle; stretch it so the flame uses its whole range.
            return Mathf.Clamp01(Mathf.InverseLerp(NoiseLow, NoiseHigh, mixed));
        }

        /// <summary>The seed a light at this world position burns with.</summary>
        public static float SeedFromPosition(Vector3 position)
        {
            return Mathf.Abs(position.x * 12.9898f + position.y * 78.233f + position.z * 37.719f) % 997f;
        }

        private void Awake()
        {
            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null)
            {
                enabled = false;
            }
        }

        private void OnEnable()
        {
            Capture();
        }

        private void OnDisable()
        {
            Restore();
        }

        private void Update()
        {
            Apply(Time.time, Time.deltaTime);
        }

        /// <summary>
        /// Sets the light as it looks at the given time, easing the flame toward the noise over <c>smoothing</c> seconds so it never jumps.
        /// Update calls this every frame.
        /// </summary>
        public void Apply(float time, float deltaTime)
        {
            if (!captured)
            {
                Capture();
            }

            fade = Mathf.MoveTowards(fade, lit ? 1f : 0f, FadeStep(deltaTime));
            if (fade <= 0f)
            {
                flickerLight.enabled = false;
                transform.localPosition = baseLocalPosition;
                return;
            }

            flickerLight.enabled = true;
            float target = Sample(time, activeSeed, swaySpeed, crackleSpeed, crackleAmount);
            float flame = Smooth(target, deltaTime);
            flickerLight.intensity = baseIntensity * Mathf.Lerp(minIntensity, maxIntensity, flame) * Mathf.SmoothStep(0f, 1f, fade);
            flickerLight.range = baseRange * (1f + rangeVariation * (flame * 2f - 1f));

            if (tintWithFlicker)
            {
                flickerLight.color = Color.Lerp(dimTint, brightTint, flame);
            }

            if (wander > 0f)
            {
                float x = Mathf.PerlinNoise(activeSeed + 101.3f, time * swaySpeed) - 0.5f;
                float y = Mathf.PerlinNoise(activeSeed + 211.7f, time * swaySpeed) - 0.5f;
                float z = Mathf.PerlinNoise(activeSeed + 307.9f, time * swaySpeed) - 0.5f;
                transform.localPosition = baseLocalPosition + new Vector3(x, y, z) * (2f * wander);
            }
        }

        private float FadeStep(float deltaTime)
        {
            float seconds = lit ? fadeInSeconds : fadeOutSeconds;
            return seconds <= 0f ? 1f : deltaTime / seconds;
        }

        private float Smooth(float target, float deltaTime)
        {
            if (!smoothedReady || smoothing <= 0f)
            {
                smoothedFlame = target;
                smoothedReady = true;
            }
            else
            {
                // Frame-rate independent exponential ease.
                smoothedFlame = Mathf.Lerp(smoothedFlame, target, 1f - Mathf.Exp(-deltaTime / smoothing));
            }
            return smoothedFlame;
        }

        private void Capture()
        {
            flickerLight = GetComponent<Light>();
            baseIntensity = flickerLight.intensity;
            baseRange = flickerLight.range;
            baseColor = flickerLight.color;
            baseLightEnabled = flickerLight.enabled;
            lit = startLit;
            fade = startLit ? 1f : 0f;
            flickerLight.enabled = baseLightEnabled && startLit;
            baseLocalPosition = transform.localPosition;
            activeSeed = seedFromPosition ? SeedFromPosition(transform.position) : seed;
            smoothedReady = false;
            captured = true;
        }

        private void Restore()
        {
            if (!captured)
            {
                return;
            }

            flickerLight.intensity = baseIntensity;
            flickerLight.range = baseRange;
            flickerLight.color = baseColor;
            flickerLight.enabled = baseLightEnabled;
            transform.localPosition = baseLocalPosition;
            captured = false;
        }

        private void OnValidate()
        {
            minIntensity = Mathf.Max(0f, minIntensity);
            maxIntensity = Mathf.Max(minIntensity, maxIntensity);
            swaySpeed = Mathf.Max(0f, swaySpeed);
            crackleSpeed = Mathf.Max(0f, crackleSpeed);
            smoothing = Mathf.Max(0f, smoothing);
            fadeInSeconds = Mathf.Max(0f, fadeInSeconds);
            fadeOutSeconds = Mathf.Max(0f, fadeOutSeconds);
        }
    }
}
