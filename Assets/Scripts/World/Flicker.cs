using UnityEngine;

namespace SimpleRPG
{
    /// <summary>
    /// Simulates a flickering effect on a Light by varying its intensity over time.
    /// </summary>
    [RequireComponent(typeof(Light))]
    public class Flicker : MonoBehaviour
    {
        [Header("Flicker Settings")]

        /// <summary> The minimum intensity multiplier for the flicker effect. </summary>
        [Tooltip("Minimum intensity multiplier for flicker effect.")]
        [SerializeField] private float minIntensity = 0.8f;

        /// <summary> The maximum intensity multiplier for the flicker effect. </summary>
        [Tooltip("Maximum intensity multiplier for flicker effect.")]
        [SerializeField] private float maxIntensity = 1.2f;

        /// <summary> Speed at which the light flickers. This determines how often the light's intensity changes. </summary>
        [Tooltip("Speed at which the light flickers.")]
        [SerializeField] private float flickerSpeed = 0.1f;

        /// <summary>
        /// The light component that will flicker.
        /// </summary>
        private Light flickerLight;

        /// <summary>
        /// Base intensity of the light when not flickering.
        /// </summary>
        private float baseIntensity;

        /// <summary>
        /// Timer to track the flicker speed and intensity changes.
        /// </summary>
        private float timer;

        /// <summary>
        /// Target intensity for the flicker effect, which is calculated based on the base intensity and the min/max multipliers.
        /// </summary>
        private float targetIntensity;

        void Start()
        {
            flickerLight = GetComponent<Light>();
            baseIntensity = flickerLight.intensity;
            targetIntensity = baseIntensity;
            timer = 0f;
        }

        void Update()
        {
            timer += Time.deltaTime;
            if (timer >= flickerSpeed)
            {
                targetIntensity = baseIntensity * Random.Range(minIntensity, maxIntensity);
                timer = 0f;
            }
            flickerLight.intensity = Mathf.Lerp(flickerLight.intensity, targetIntensity, Time.deltaTime / flickerSpeed);
        }
    }
}
