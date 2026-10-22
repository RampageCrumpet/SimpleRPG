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
        private MeleeDelivery meleeAttackAbility;


        // Start is called once before the first execution of Update after the MonoBehaviour is created
        void Start()
        {
            character = this.gameObject.GetComponent<Character>();
            animator = character.gameObject.GetComponentsInChildren<Animator>();
            character.OnTakeDamage += new Character.NotifyDamageTaken(UpdateTakeDamage);
            characterController = character.gameObject.GetComponent<CharacterController>();
            meleeAttackAbility = character.gameObject.GetComponent<MeleeDelivery>();
            meleeAttackAbility.MeleeAttackStarted += new MeleeDelivery.NotifyMeleeAttackStarted(MeleeAttack);
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
        /// Plays the attack the server started. Attack 1 starts a new swing; attacks 2 and 3 are queued from the previous swing's combo.
        /// </summary>
        /// <param name="attackNumber">The attack in the chain the server started (1, 2 or 3).</param>
        private void MeleeAttack(int attackNumber)
        {
            string trigger = GetAttackTrigger(attackNumber);

            foreach (Animator controller in animator)
            {
                controller.SetTrigger(trigger);
            }
        }

        /// <summary>
        /// Chooses which animator trigger an attack from the server should set.
        /// Attack 1 starts a new swing. Any later attack is the next swing of the combo, which the previous swing's animator state picks up.
        /// </summary>
        /// <param name="attackNumber">The attack in the chain the server started (1, 2 or 3).</param>
        /// <returns>The trigger to set.</returns>
        public static string GetAttackTrigger(int attackNumber)
        {
            return attackNumber <= 1 ? "Attack_OneHanded" : "ComboQueued";
        }
    }
}
