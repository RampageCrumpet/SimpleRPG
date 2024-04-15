using SimpleRPG;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.AI;

namespace AI
{
    /// <summary>
    /// This <see cref="IState"/> handles spiders returning to their nest.
    /// </summary>
    public class SpiderStateFindNest : SpiderState
    {
        /// <summary>
        /// The distance from the nest before the spider decides to rest.
        /// </summary>
        private float restingDistanceFromNest = 0.25f;

        /// <summary>
        /// Constructor for the <see cref="SpiderStateFindNest"/> state.
        /// </summary>
        /// <param name="spiderAI"></param>
        public SpiderStateFindNest(SpiderAI spiderAI) : base(spiderAI)
        {
        }

        /// <inheritdoc/>
        public override void OnStateEnter()
        {
            // If we don't have a nest we need to go make one.
            if (SpiderAI.Nest == null)
            {
                SpiderAI.SpiderStateMachine.ChangeState(new SpiderStateMakeNewNest(SpiderAI));
                return;
            }

            // Find the spiders nest.
            SpiderAI.SetDestinationRPC(SpiderAI.Nest.transform.position);
        }

        /// <inheritdoc/>
        public override void OnStateExit()
        {
        }

        /// <inheritdoc/>
        public override void OnStateUpdate()
        {
            //Check if we're dead.
            if (SpiderAI.Health < 0)
            {
                SpiderAI.SpiderStateMachine.ChangeState(new SpiderStateDead(SpiderAI));
                return;
            }

            // Check to see if anyone has wandered into our sight.
            IOrderedEnumerable<Character> visibleCharacters = FindVisibleTargets();
            if (visibleCharacters.Any())
            {
                SpiderAI.SpiderStateMachine.ChangeState(new SpiderStateChase(SpiderAI, visibleCharacters.First()));
                return;
            }

            // If the spider reaches the nest enter an idle state.
            if (SpiderAI.NavigationAgent.pathStatus != NavMeshPathStatus.PathInvalid && SpiderAI.NavigationAgent.remainingDistance <= restingDistanceFromNest)
            {
                SpiderAI.SpiderStateMachine.ChangeState(new SpiderStateIdle(SpiderAI));
                return;
            }
        }
    }
}