using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.AI;

namespace AI
{
    /// <summary>
    /// This <see cref="IState"/> handles spiders placing new webs.
    /// </summary>
    public class SpiderStatePlaceWebs : SpiderState
    {
        /// <summary>
        /// The maximum distance our spider will wander trying to place it's webs.
        /// </summary>
        private const float wanderDistance = 25;

        /// <summary>
        /// The maximum distance the spider will check for walls when considering whether a space is enclosed or not.
        /// </summary>
        private const float distanceToCheckForWalls = 3f;

        /// <summary>
        /// How many directions the spider will check to see if they have walls when considering if a space is enclosed or not.
        /// </summary>
        private const int totalSegmentsChecked = 8;

        /// <summary>
        /// How many directions need to have a wall to consider a space enclosed or not.
        /// </summary>
        private const int requiredSegmentsOccupied = 4;

        /// <summary>
        /// The minimum distance between a new web and an existing web.
        /// </summary>
        private const float minimumDistanceToWeb = 5.0f;

        /// <summary>
        /// The minimum number of seconds the spider needs to wait before it will try to construct a new web.
        /// </summary>
        private const float minimumNumberOfSecondsBeforeWebConstruction = 0.25f;

        /// <summary>
        /// The time of our last web construction.
        /// </summary>
        private float lastWebConstructionTime;
        public SpiderStatePlaceWebs(SpiderAI spiderAI) : base(spiderAI)
        {
        }

        /// <inheritdoc/>
        public override void OnStateEnter()
        {
            //Find a spot and wander to it.
            RandomWander(wanderDistance);
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

            if (Time.time - lastWebConstructionTime > minimumNumberOfSecondsBeforeWebConstruction && IsValidSpaceForWeb())
            {
                lastWebConstructionTime = Time.time;
                GameObject spawnedWeb = Object.Instantiate(SpiderAI.SpiderwebPrefab, SpiderAI.transform.position, SpiderAI.transform.rotation);
                SpiderAI.spiderwebs.Add(spawnedWeb.GetComponent<Spiderweb>());
            }


            // If we're at the end of the path return back to the nest.
            if (this.SpiderAI.NavigationAgent.pathStatus == NavMeshPathStatus.PathComplete && this.SpiderAI.NavigationAgent.remainingDistance < 0.5f)
            {
                SpiderAI.SpiderStateMachine.ChangeState(new SpiderStateFindNest(this.SpiderAI));
                return;
            }
        }

        /// <summary>
        /// Determines if the spiders current location is a valid place to build a web or not.
        /// </summary>
        /// <returns> Returns true if the space is valid for placing a web, false otherwise.</returns>
        private bool IsValidSpaceForWeb()
        {
            // The total number of degrees we want to check.
            // In this case a full circle of 360 degrees.
            float totalAngle = 360;

            // Divide our 360 degree circle up into a collection of evenly spaced arcs.
            float delta = totalAngle / totalSegmentsChecked;

            // Perform a raycast at the end of each arc and record how many are hit. 
            int raycastHits = 0;
            for(int i = 0; i < totalSegmentsChecked; i++)
            {
                Vector3 direction = Quaternion.Euler(0, i * delta, 0) * SpiderAI.transform.forward;
                if (Physics.Raycast(SpiderAI.transform.position, direction, distanceToCheckForWalls, LayerMask.GetMask("World")))
                {
                    raycastHits++;
                }
            }

            // If we haven't hit enough wall segments we know the space isn't enclosed enough and we're done checking..
            if(raycastHits < requiredSegmentsOccupied)
            {
                return false;
            }

            // Find all objects within range and see if they're webs. If any of them are webs there's a web within range.
            bool webWithinRange = Physics.OverlapSphere(SpiderAI.transform.position, minimumDistanceToWeb, LayerMask.GetMask("TriggerLocations"), QueryTriggerInteraction.Collide).Any(x => x.gameObject.GetComponent<Spiderweb>() != null);

            // If no web is within our minimum range this must be a valid place to place a web.
            return !webWithinRange;
        }
    }
}