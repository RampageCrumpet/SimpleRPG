using System.Collections;
using System.Collections.Generic;
using UnityEngine;


namespace AI
{
    public class SpiderStateDead : SpiderState
    {
        public SpiderStateDead(SpiderAI spiderAI) : base(spiderAI)
        {
        }

        /// <inheritdoc/>
        public override void OnStateEnter()
        {
            SpiderAI.animatable.Die();
        }

        /// <inheritdoc/>
        public override void OnStateExit()
        {
        }

        /// <inheritdoc/>
        public override void OnStateUpdate()
        {
        }
    }
}
