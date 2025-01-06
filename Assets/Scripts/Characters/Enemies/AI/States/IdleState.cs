using SimpleRPG;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace AI
{
    /// <summary>
    /// This <see cref="IState"/> handles spiders waiting in nests until something happens.
    /// </summary>
    public class IdleState : IState 
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
        /// The state to transition to if we find a target.
        /// </summary>
        private IState targetFound;

        public IdleState() { }

        /// <summary>
        /// Initializes this state with the given values.
        /// </summary>
        /// <param name="aiController"> The AI controller providing context to this state.</param>
        /// <param name="dead"> The state we want to transition to when we're dead.</param>
        /// <param name="targetFound"> The state we want to transition to when we've found a target.</param>
        public void Initialize(AIController aiController, IState dead, IState targetFound)
        {
            this.aiController = aiController;
            this.dead = dead;
            this.targetFound = targetFound;
        }

        /// <inheritdoc/>
        public void OnStateEnter()
        {
        }

        /// <inheritdoc/>
        public void OnStateExit()
        {
        }

        /// <inheritdoc/>
        public void OnStateUpdate()
        {
            if(aiController.Character.Health <= 0)
            {
                aiController.ChangeState(dead);
                return;
            }

            IEnumerable<Character> sensedTargets = aiController.SenseTargets();

            // If we can see a player chase that.
            if(sensedTargets.Any())
            {
                aiController.ChangeState(targetFound);
                return;
            }
        }
    }
}