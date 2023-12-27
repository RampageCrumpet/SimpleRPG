using SimpleRPG;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace AI
{
    /// <summary>
    /// This <see cref="IState"/> handles spiders attacking prey that they've caught up to.
    /// </summary>
    public class SpiderStateAttack : SpiderState
    {
        private Character targetCharacter;

        /// <summary>
        /// The time the spider has already spent winding up for an attack.
        /// </summary>
        private float windUpTime;

        /// <summary>
        /// The time the spider has already spent cooling down from an attack.
        /// </summary>
        private float cooldownTime;

        public SpiderStateAttack(SpiderAI spiderAI, Character targetCharacter) : base(spiderAI)
        {
            this.targetCharacter = targetCharacter;
        }

        /// <inheritdoc/>
        public override void OnStateEnter()
        {
        }

        /// <inheritdoc/>
        public override void OnStateExit()
        {
        }

        /// <inheritdoc/>
        public override void OnStateUpdate()
        {
            float distanceFromTarget = (SpiderAI.transform.position - targetCharacter.transform.position).magnitude;

            windUpTime += Time.deltaTime;
            cooldownTime += Time.deltaTime;

            // If we're too far from our target go back to chasing it.
            if (distanceFromTarget > SpiderAI.Attack.AttackDistance)
            {
                SpiderAI.SpiderStateMachine.ChangeState(new SpiderStateChase(SpiderAI, targetCharacter));
                return;
            }

            // If we've surpassed our wind up time. 
            if(windUpTime > SpiderAI.Attack.WindUpTime) 
            {
                windUpTime = 0;
                SpiderAI.Attack.AttackCharacter(targetCharacter);
                cooldownTime = 0;
            }
        }
    }
}