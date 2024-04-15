using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Experimental.AI;
using static UnityEngine.UI.Image;

namespace AI
{
    /// <summary>
    /// This <see cref="IState"/> handles spiders building new nests.
    /// </summary>
    public class SpiderStateMakeNewNest : SpiderState
    {
        /// <summary>
        /// The range at which we can detect a good nest location.
        /// </summary>
        private const float NestDetectionRange = 25.0f;

        /// <summary>
        /// The maximum distance we'll wander before we stop to look for a new nest.
        /// </summary>
        private const float WanderDistance = 50.0f;

        public SpiderStateMakeNewNest(SpiderAI spiderAI) : base(spiderAI)
        {
        }

        /// <inheritdoc/>
        public override void OnStateEnter()
        {
            //Find a valid location to build a nest.
            SpiderAI.FindNestLocationWithinRange(NestDetectionRange);

            if(SpiderAI.Nest != null)
            {
                this.SpiderAI.NavigationAgent.SetDestination(SpiderAI.Nest.transform.position);
            }
            // If no nest is found we want to wander.
            else
            {
                RandomWander();
            }
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

            if (this.SpiderAI.NavigationAgent.pathStatus == NavMeshPathStatus.PathComplete && this.SpiderAI.NavigationAgent.remainingDistance < 0.25f)
            {
                if(SpiderAI.Nest != null && (this.SpiderAI.NavigationAgent.destination - SpiderAI.Nest.transform.position).magnitude < 0.5f && SpiderAI.Nest.IsOwned == false)
                {
                    SpiderAI.Nest.ActivateNest(this.SpiderAI);
                    SpiderAI.SpiderStateMachine.ChangeState(new SpiderStateIdle(this.SpiderAI));
                    return;
                }
                else
                {
                    SpiderAI.FindNestLocationWithinRange(NestDetectionRange);

                    // If we found a nest go to it.
                    if(SpiderAI.Nest != null)
                    {
                        this.SpiderAI.NavigationAgent.SetDestination(SpiderAI.Nest.transform.position);
                    }
                    // If we didn't find a valid nest continue to wander.
                    else
                    {
                        RandomWander();
                    }
                }
            }
        }



        /// <summary>
        /// Tell the Spider to randomly wander to somewhere else.
        /// </summary>
        private void RandomWander()
        {
            // The location we want to wander to.
            Vector3 desiredWanderLocation = Random.insideUnitSphere * WanderDistance + SpiderAI.transform.position;

            NavMeshHit navHit;
            NavMesh.SamplePosition(desiredWanderLocation, out navHit, WanderDistance, NavMesh.AllAreas);

            SpiderAI.NavigationAgent.SetDestination(navHit.position);
        }
    }
}