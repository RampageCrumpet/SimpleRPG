using SimpleRPG;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace SimpleRPG.Animations
{
    /// <summary>
    /// This script handles reading values from the player character and sending them to the animation controllers.
    /// </summary>
    [RequireComponent(typeof(Character))]
    [RequireComponent(typeof(CharacterController))]
    [RequireComponent(typeof(Animator))]
    public class AnimatableCharacter : MonoBehaviour
    {
        private IEnumerable<Animator> animator;
        private Character character;
        private CharacterController characterController;
        private MeleeAttackAbilityBehaviour meleeAttackAbility;


        // Start is called once before the first execution of Update after the MonoBehaviour is created
        void Start()
        {
            character = this.gameObject.GetComponent<Character>();
            animator = character.gameObject.GetComponentsInChildren<Animator>();
            character.OnTakeDamage += new Character.NotifyDamageTaken(UpdateTakeDamage);
            characterController = character.gameObject.GetComponent<CharacterController>();
            meleeAttackAbility = character.gameObject.GetComponent<MeleeAttackAbilityBehaviour>();
            meleeAttackAbility.MeleeAttackStarted += new MeleeAttackAbilityBehaviour.NotifyMeleeAttackStarted(MeleeAttack);
        }

        // Update is called once per frame
        void Update()
        {
            UpdateAnimationValues();
        }

        /// <summary>
        /// Sets the forward speed of all <see cref="Animator"/>'s.
        /// </summary>
        private void UpdateAnimationValues()
        {
            foreach(Animator controller in animator)
            {
                controller.SetFloat("ForwardMovementSpeed", Vector3.Dot(characterController.velocity, this.transform.TransformDirection(Vector3.forward)));
                controller.SetFloat("HorizontalMovementSpeed", Vector3.Dot(characterController.velocity, this.transform.TransformDirection(Vector3.right)));
            }
        }

        /// <summary>
        /// Alerts all of the <see cref="Animator"/>'s that we've taken damage.
        /// </summary>
        private void UpdateTakeDamage()
        {
            foreach (Animator controller in animator)
            {
                controller.SetTrigger("TakeDamage");
            }
        }

        /// <summary>
        /// Starts a melee attack, or queues the next swing of the longsword combo while one is already playing.
        /// </summary>
        private void MeleeAttack()
        {
            foreach (Animator controller in animator)
            {
                AnimatorStateInfo state = controller.GetCurrentAnimatorStateInfo(0);
                string trigger = GetAttackTrigger(state.IsTag("Attack"), state.IsTag("Combo"));

                if (trigger != null)
                {
                    controller.SetTrigger(trigger);
                }
            }
        }

        /// <summary>
        /// Chooses which animator trigger an attack press should set.
        /// Presses during an attack clip queue the next swing; presses during a combo continuation are ignored;
        /// any other press starts a new attack.
        /// </summary>
        /// <param name="inAttack">Whether the current state is an attack clip.</param>
        /// <param name="inCombo">Whether the current state is a combo continuation.</param>
        /// <returns>The trigger to set, or null if the press should be ignored.</returns>
        public static string GetAttackTrigger(bool inAttack, bool inCombo)
        {
            if (inAttack)
            {
                return "ComboQueued";
            }

            return inCombo ? null : "Attack_OneHanded";
        }
    }
}
