using SimpleRPG;
using System.Collections;
using System.Collections.Generic;
using UnityEditor.Animations;
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

        // Start is called once before the first execution of Update after the MonoBehaviour is created
        void Start()
        {
            character = this.gameObject.GetComponent<Character>();
            animator = character.gameObject.GetComponentsInChildren<Animator>();
            character.TakeDamage += new Character.NotifyDamageTaken(UpdateTakeDamage);
            characterController = character.gameObject.GetComponent<CharacterController>();
        }

        // Update is called once per frame
        void Update()
        {
            UpdateAnimationValues();
        }

        /// <summary>
        /// Sets the forward speed of all <see cref="AnimatorController"/>'s.
        /// </summary>
        private void UpdateAnimationValues()
        {
            foreach(Animator controller in animator)
            {
                controller.SetFloat("ForwardMovementSpeed", characterController.velocity.z);
                controller.SetFloat("HorizontalMovementSpeed", characterController.velocity.x);
            }
        }

        /// <summary>
        /// Alerts all of the <see cref="AnimatorController"/>'s that we've taken damage.
        /// </summary>
        private void UpdateTakeDamage()
        {
            foreach (Animator controller in animator)
            {
                controller.SetTrigger("TakeDamage");
            }
        }
    }
}
