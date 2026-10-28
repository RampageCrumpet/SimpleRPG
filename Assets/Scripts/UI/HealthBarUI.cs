using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SimpleRPG.UI
{
    /// <summary>
    /// Shows the local character's health as a pool of blood. The blood image uses the Blood Bar shader, which this drives with the current fill and a lagging damage trail.
    /// </summary>
    public class HealthBarUI : MonoBehaviour, ILocalPlayerView
    {
        private static readonly int FillId = Shader.PropertyToID("_Fill");
        private static readonly int TrailId = Shader.PropertyToID("_Trail");
        private static readonly int AspectId = Shader.PropertyToID("_Aspect");

        /// <summary>
        /// The image drawn with the Blood Bar shader.
        /// </summary>
        [SerializeField]
        [Tooltip("The image drawn with the Blood Bar shader.")]
        private Image bloodImage;

        /// <summary>
        /// Shows the current and maximum health as numbers, if set.
        /// </summary>
        [SerializeField]
        [Tooltip("Shows current / maximum health as text. Optional.")]
        private TMP_Text valueText;

        /// <summary>
        /// How fast the blood level moves toward the real health, in fractions of the bar per second.
        /// </summary>
        [SerializeField]
        [Tooltip("How fast the blood level moves toward the real health, in fractions of the bar per second.")]
        private float fillSpeed = 2f;

        /// <summary>
        /// How fast the damage trail shrinks after a hit, in fractions of the bar per second.
        /// </summary>
        [SerializeField]
        [Tooltip("How fast the damage trail shrinks after a hit, in fractions of the bar per second.")]
        private float trailSpeed = 0.35f;

        /// <summary>
        /// How long the damage trail waits after a hit before it starts shrinking.
        /// </summary>
        [SerializeField]
        [Tooltip("Seconds the damage trail waits after a hit before it starts shrinking.")]
        private float trailDelay = 0.4f;

        /// <summary>
        /// The character whose health is shown.
        /// </summary>
        private Character character;

        /// <summary>
        /// The per-bar copy of the shader's material, so bars do not share fill values.
        /// </summary>
        private Material material;

        private float shownFill = 1f;
        private float shownTrail = 1f;
        private float trailTimer;

        /// <summary>
        /// Points the bar at the character whose health it should show and snaps it to that health.
        /// </summary>
        /// <param name="character"> The locally owned character.</param>
        public void SetCharacter(Character character)
        {
            this.character = character;
            shownFill = shownTrail = TargetFill();
            Apply();
        }

        /// <inheritdoc/>
        public void BindLocalPlayer(Character character) => SetCharacter(character);

        /// <inheritdoc/>
        public void UnbindLocalPlayer() => SetCharacter(null);

        /// <summary>
        /// Moves the displayed values one step toward the targets.
        /// </summary>
        /// <param name="fill"> The displayed fill, 0 to 1.</param>
        /// <param name="trail"> The displayed damage trail, never below the fill.</param>
        /// <param name="trailTimer"> Seconds the trail still has to wait before shrinking.</param>
        /// <param name="target"> The real health fraction, 0 to 1.</param>
        /// <param name="deltaTime"> Seconds since the last step.</param>
        public static void Step(ref float fill, ref float trail, ref float trailTimer, float target, float deltaTime, float fillSpeed, float trailSpeed, float trailDelay)
        {
            if (target < fill)
            {
                // Damage: the blood level drops quickly, and the trail stays put for a moment to show what was lost.
                if (trail < fill)
                {
                    trail = fill;
                }

                trailTimer = trailDelay;
            }

            fill = Mathf.MoveTowards(fill, target, fillSpeed * deltaTime);

            if (trailTimer > 0f)
            {
                trailTimer -= deltaTime;
            }
            else
            {
                trail = Mathf.MoveTowards(trail, fill, trailSpeed * deltaTime);
            }

            trail = Mathf.Max(trail, fill);
        }

        private void Awake()
        {
            // The image's material is a shared asset, so give this bar its own copy to write to.
            material = new Material(bloodImage.material);
            bloodImage.material = material;
        }

        private void OnDestroy()
        {
            if (material != null)
            {
                Destroy(material);
            }
        }

        private void Update()
        {
            if (character == null)
            {
                return;
            }

            Step(ref shownFill, ref shownTrail, ref trailTimer, TargetFill(), Time.unscaledDeltaTime, fillSpeed, trailSpeed, trailDelay);
            Apply();
        }

        private float TargetFill()
        {
            return character != null && character.MaxHealth > 0 ? Mathf.Clamp01((float)character.Health / character.MaxHealth) : 0f;
        }

        private void Apply()
        {
            Rect rect = bloodImage.rectTransform.rect;
            if (valueText != null)
            {
                valueText.text = character != null ? character.Health + " / " + character.MaxHealth : string.Empty;
            }

            material.SetFloat(FillId, shownFill);
            material.SetFloat(TrailId, shownTrail);
            material.SetFloat(AspectId, rect.height > 0f ? rect.width / rect.height : 1f);
        }
    }
}
