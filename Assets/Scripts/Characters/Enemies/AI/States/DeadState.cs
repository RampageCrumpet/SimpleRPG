using System.Collections;
using System.Collections.Generic;
using UnityEngine;


namespace AI
{
    public class DeadState : IState
    {
        private AIController aiController;

        public DeadState() { }

        /// <summary>
        /// Initialize the dead state.
        /// </summary>
        /// <param name="aiController"> The <see cref="AIController"/> in charge of driving this state.</param>
        public void Initialize(AIController aiController)
        {
            this.aiController = aiController;
        }

        /// <inheritdoc/>
        public void OnStateEnter()
        {
            aiController.Character.Die();
        }

        /// <inheritdoc/>
        public void OnStateExit()
        {
        }

        /// <inheritdoc/>
        public void OnStateUpdate()
        {
        }
    }
}
