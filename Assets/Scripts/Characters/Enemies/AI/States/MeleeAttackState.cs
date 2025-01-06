using SimpleRPG;
using SimpleRPG.Abilities;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace AI
{
    /// <summary>
    /// This <see cref="IState"/> handles spiders attacking prey that they've caught up to.
    /// </summary>
    public class MeleeAttackState : IState
    {
        private Character targetCharacter;

        private IState chase;

        private IState dead;

        private AIController aiController;

        public MeleeAttackState()
        {
        }

        /// <inheritdoc/>
        public void OnStateEnter()
        {
            targetCharacter = aiController.SenseTargets().OrderByDescending(x => (aiController.transform.position - x.transform.position).magnitude).First();
        }

        /// <inheritdoc/>
        public void OnStateExit()
        {
        }

        /// <summary>
        /// Initialize the melee attack state.
        /// </summary>
        /// <param name="aiController"> The AIController in charge of driving this state.</param>
        /// <param name="outsideRange"> The state we want to go to when the target is outside of our range.</param>
        /// <param name="dead"> The state we want to go to when the target is dead.</param>
        public void Initialize(AIController aiController, IState outsideRange, IState dead)
        {
            this.aiController = aiController;
            this.chase = outsideRange;
            this.dead = dead;
        }

        /// <inheritdoc/>
        public void OnStateUpdate()
        {

            //Check if we're dead.
            if (aiController.Character.Health <= 0)
            {
                aiController.ChangeState(dead);
                return;
            }

            float distanceFromTarget = (aiController.transform.position - targetCharacter.transform.position).magnitude;

            // If we're too far from our target go back to chasing it.
            if (distanceFromTarget > aiController.AttackRange)
            {
                aiController.ChangeState(chase);
                return;
            }

            IEnumerable<AbilityInstance> meleeAttackAbilities = aiController.Character.PersonalAbilities.Where(x => x.Ability is MeleeAttackAbility);

            AbilityInstance chosenInstance = meleeAttackAbilities.FirstOrDefault(x => x.CooldownTimeLeft <= 0);

            if(chosenInstance != null)
            {
                chosenInstance.Activate();
            }
        }
    }
}