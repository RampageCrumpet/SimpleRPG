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
    public class AnimatableCharacter : MonoBehaviour
    {
        private IEnumerable<Animator> animator;
        private Character character;

        // Start is called once before the first execution of Update after the MonoBehaviour is created
        void Start()
        {
            animator = this.gameObject.GetComponentsInChildren<Animator>();
            character.GetComponent<Character>();
            character.TakeDamage += new Character.NotifyDamageTaken(UpdateTakeDamage);
        }

        // Update is called once per frame
        void Update()
        {
            UpdateAnimationValues();
        }

        /// <summary>
        /// Sets the forward speed of all <see cref="AnimatiorCOntroller"/>'s.
        /// </summary>
        private void UpdateAnimationValues()
        {
            foreach(Animator controller in animator)
            {
                controller.SetFloat("ForwardMovementSpeed", 0);
            }
        }

        private void UpdateTakeDamage()
        {
            foreach (Animator controller in animator)
            {
                controller.SetTrigger("TakeDamage");
            }
        }
    }
}
