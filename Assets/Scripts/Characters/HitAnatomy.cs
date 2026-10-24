using SimpleRPG;
using SimpleRPG.Gameplay;
using UnityEngine;

namespace HitDetection
{
    /// <summary>
    /// A character's anatomy for combat: the hurtboxes on its bones and what the body locations they stand for are worth. Player and monster
    /// carry the same component with different hurtboxes and profiles, so one rule resolves a blow against either.
    /// The hurtboxes are only live while the character is: death turns every region off on every machine, and the next life turns them back on.
    /// </summary>
    [RequireComponent(typeof(Character))]
    public class HitAnatomy : MonoBehaviour, IRegionDamageModifier
    {
        [SerializeField]
        [Tooltip("What a blow to each body location is worth against this character. Leave empty for every location to count the same.")]
        private HitRegionProfile profile;

        private Character character;
        private HurtBox[] hurtBoxes;

        public HitRegionProfile Profile
        {
            get => profile;
            set => profile = value;
        }

        /// <summary>
        /// Every region of this body, including any that are currently off.
        /// </summary>
        public HurtBox[] HurtBoxes => hurtBoxes ?? (hurtBoxes = GetComponentsInChildren<HurtBox>(true));

        /// <inheritdoc/>
        public float DamageMultiplier(int bodyLocation) => profile != null ? profile.MultiplierFor(bodyLocation) : 1f;

        private void Awake()
        {
            character = GetComponent<Character>();
        }

        private void OnEnable()
        {
            character.OnDeath += HandleDeath;
            character.OnRevive += HandleRevive;
            SetStrikable(character.IsAlive);
        }

        private void OnDisable()
        {
            if (character != null)
            {
                character.OnDeath -= HandleDeath;
                character.OnRevive -= HandleRevive;
            }
        }

        private void HandleDeath() => SetStrikable(false);

        private void HandleRevive() => SetStrikable(true);

        /// <summary>
        /// Turns every region on or off.
        /// </summary>
        public void SetStrikable(bool strikable)
        {
            foreach (HurtBox hurtBox in HurtBoxes)
            {
                hurtBox.SetStrikable(strikable);
            }
        }
    }
}
