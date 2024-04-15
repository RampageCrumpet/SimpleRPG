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
    public class SpiderStateIdle : SpiderState 
    {
        /// <summary>
        /// Constructor for the <see cref="SpiderStateIdle"/> class.
        /// </summary>
        /// <param name="spiderAI"> The SpiderAI determining how our spider behaves.</param>
        /// <param name="stateMachine"> The state machine driving our <see cref="IState"/>.</param>
        public SpiderStateIdle(SpiderAI spiderAI) : base(spiderAI)
        {
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
            if(SpiderAI.Health < 0)
            {
                SpiderAI.SpiderStateMachine.ChangeState(new SpiderStateDead(SpiderAI));
                return;
            }

            // If we can see a player chase that.
            if(SpiderAI.TargetList.Any())
            {
                IOrderedEnumerable<Character> visibleTargets = FindVisibleTargets();

                if(visibleTargets.Any())
                {
                    SpiderAI.SpiderStateMachine.ChangeState(new SpiderStateChase(SpiderAI, visibleTargets.First()));
                    return;
                }
            }

            // Otherwise check if a web has been touched and go there.
            if (SpiderAI.spiderwebs.Any(x => x.HasBeenTouched))
            {
                IOrderedEnumerable<Spiderweb> spiderWebs = SpiderAI.spiderwebs.OrderBy(x => x.LastTouchTime);
                SpiderAI.SpiderStateMachine.ChangeState(new SpiderStateCheckWeb(SpiderAI, spiderWebs.First()));
                return;
            }

            // Otherwise check to see if we need to place more webs. 
            if (SpiderAI.spiderwebs.Count < SpiderAI.MinimumNumberOfSpiderWebs)
            {
                SpiderAI.SpiderStateMachine.ChangeState(new SpiderStatePlaceWebs(SpiderAI));
                return;
            }
            // Otherwise do nothing.
        }
    }
}