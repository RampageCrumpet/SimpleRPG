
using log4net.Util;
using SimpleRPG;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace AI
{
    /// <summary>
    /// This <see cref="IState"/> handles spiders checking webs that have been triggered.
    /// </summary>
    public class SpiderStateCheckWeb : SpiderState
    {
        /// <summary>
        /// The <see cref="Spiderweb"/> our spider is currently checking.
        /// </summary>
        Spiderweb spiderweb;

        /// <summary>
        /// The distance from the web before the spider decides to reset it and head back to it's nest.
        /// </summary>
        private float minimumDistanceToWeb = 0.1f;

        public SpiderStateCheckWeb(SpiderAI spiderAI, Spiderweb spiderweb) : base(spiderAI)
        {
            this.spiderweb = spiderweb;
        }

        public override void OnStateEnter()
        {
            SpiderAI.NavigationAgent.SetDestination(spiderweb.transform.position);
        }

        public override void OnStateExit()
        {
        }

        public override void OnStateUpdate()
        {
            //Check if we're dead.
            if (SpiderAI.Health < 0)
            {
                SpiderAI.SpiderStateMachine.ChangeState(new SpiderStateDead(SpiderAI));
                return;
            }

            // If we can see a player chase that.
            if (SpiderAI.TargetList.Any())
            {
                IOrderedEnumerable<Character> visibleTargets = FindVisibleTargets();
                SpiderAI.SpiderStateMachine.ChangeState(new SpiderStateChase(SpiderAI, visibleTargets.First()));
                return;
            }

            // If we're close enough to the web reset it and then go find the nest.
            float distanceToSpiderweb = (SpiderAI.transform.position - spiderweb.transform.position).magnitude;
            if (distanceToSpiderweb < minimumDistanceToWeb)
            {
                // If any other spiderwebs have been touched go there.
                spiderweb.ResetSpiderweb();
                if (SpiderAI.spiderwebs.Any(x => x.HasBeenTouched))
                {
                    IOrderedEnumerable<Spiderweb> spiderWebs = SpiderAI.spiderwebs.OrderBy(x => x.LastTouchTime);
                    SpiderAI.SpiderStateMachine.ChangeState(new SpiderStateCheckWeb(SpiderAI, spiderWebs.First()));
                    return;
                }

                // Otherwise go back to the nest.
                SpiderAI.SpiderStateMachine.ChangeState(new SpiderStateFindNest(SpiderAI));
                return;
            }
        }
    }
}