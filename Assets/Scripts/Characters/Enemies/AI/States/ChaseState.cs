using SimpleRPG;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace AI
{
    /// <summary>
    /// This <see cref="IState"/> handles spiders chasing prey that they've seen.
    /// </summary>
    public class ChaseState : IState
    {
        /// <summary>
        /// The AI controller acting as a blackboard for this state.
        /// </summary>
        private AIController aiController;


        /// <summary>
        /// The state to transition to if we die.
        /// </summary>
        private IState dead;

        /// <summary>
        /// The state to transition to if we caught the target.
        /// </summary>
        private IState targetCaught;

        /// <summary>
        /// The state to transition to if we lost the target.
        /// </summary>
        private IState targetLost;

        /// <summary>
        /// The character we're actively pursuing.
        /// </summary>
        Character targetCharacter;

        public ChaseState() { }

        /// <summary>
        /// Initialize the chase state.
        /// </summary>
        /// <param name="aIController"> The AI controller in charge of driving this state.</param>
        /// <param name="dead"> The state to transition to if we die.</param>
        /// <param name="targetCaught"> The state to transition to if we catch our target.</param>
        /// <param name="targetLost"> The state to transition to if we lose our target.</param>
        public void Initialize(AIController aiController, IState dead, IState targetCaught, IState targetLost)
        {
            this.aiController = aiController;
            this.dead = dead;
            this.targetCaught = targetCaught;
            this.targetLost = targetLost;
        }

        /// <inheritdoc/>
        public void OnStateEnter()
        {
            targetCharacter = aiController.SenseTargets().OrderByDescending(x => Vector3.Distance(aiController.transform.position, x.transform.position)).First();
        }

        /// <inheritdoc/>
        public void OnStateExit()
        {
        }

        /// <inheritdoc/>
        public void OnStateUpdate()
        {
            if (aiController.Character.Health <= 0)
            {
                aiController.ChangeState(dead);
                return;
            }

            float distanceToTarget = (aiController.transform.position - targetCharacter.transform.position).magnitude;

            // If we're close enough to attack transition to the attack state.
            if (distanceToTarget < aiController.AttackRange)
            {
                aiController.ChangeState(targetCaught);
                return;
            }


            // If we've lost sight of the character transition to the target lost state.
            if (!aiController.PrimarySenses.Any(x => x.CanSense(targetCharacter)))
            {
                aiController.ChangeState(targetLost);
                return;
            }

            //Otheriwse continue to follow the target character
            aiController.SetDestination(targetCharacter.transform.position);
        }
    }
}