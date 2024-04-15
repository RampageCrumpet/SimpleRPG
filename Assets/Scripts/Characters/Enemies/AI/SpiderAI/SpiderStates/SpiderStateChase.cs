using SimpleRPG;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace AI
{
    /// <summary>
    /// This <see cref="IState"/> handles spiders chasing prey that they've seen.
    /// </summary>
    public class SpiderStateChase : SpiderState
    {
        /// <summary>
        /// The character our spider is currently chasing.
        /// </summary>
        Character targetCharacter;

        /// <summary>
        /// Constructor for the <see cref="SpiderStateChase"/> state.
        /// </summary>
        /// <param name="spiderAI"> The AI controlling this spider.</param>
        /// <param name="targetCharacter"> The character this spider is chasing.</param>
        public SpiderStateChase(SpiderAI spiderAI, Character targetCharacter) : base(spiderAI)
        {
            this.targetCharacter = targetCharacter;
        }

        /// <inheritdoc/>
        public override void OnStateEnter()
        {
            SpiderAI.NavigationAgent.SetDestination(targetCharacter.transform.position);
        }

        /// <inheritdoc/>
        public override void OnStateExit()
        {
        }

        /// <inheritdoc/>
        public override void OnStateUpdate()
        {
            if (SpiderAI.Health < 0)
            {
                SpiderAI.SpiderStateMachine.ChangeState(new SpiderStateDead(SpiderAI));
                return;
            }

            float distanceToTarget = (SpiderAI.transform.position - targetCharacter.transform.position).magnitude;

            // If we're close enough to attack transition to the attack state.
            if (distanceToTarget < SpiderAI.Attack.AttackDistance)
            {
                SpiderAI.SpiderStateMachine.ChangeState(new SpiderStateAttack(SpiderAI, targetCharacter));
                return;
            }

            
            // If we're far enough away from the target character to lose them return to the nest.
            if (distanceToTarget < SpiderAI.MaximumFollowDistance)
            {
                SpiderAI.SpiderStateMachine.ChangeState(new SpiderStateFindNest(SpiderAI));
                return;
            }
            //Otheriwse continue to follow the target character
            SpiderAI.NavigationAgent.SetDestination(targetCharacter.transform.position);
        }
    }
}