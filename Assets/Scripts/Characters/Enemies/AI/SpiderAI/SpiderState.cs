using SimpleRPG;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.AI;

namespace AI
{
    /// <summary>
    /// This class handles all of the basic information shared across all spider states.
    /// </summary>
    public abstract class SpiderState : IState
    {
        /// <summary>
        /// The <see cref="SpiderAI"/> that holds values on how this spider behaves.
        /// </summary>
        protected SpiderAI SpiderAI;

        /// <summary>
        /// Constructor for the <see cref="SpiderStateFindNest"/> state.
        /// </summary>
        /// <param name="spiderAI"></param>
        public SpiderState(SpiderAI spiderAI)
        {
            this.SpiderAI = spiderAI;
        }

        /// <inheritdoc/>
        public abstract void OnStateEnter();

        /// <inheritdoc/>
        public abstract void OnStateExit();

        /// <inheritdoc/>
        public abstract void OnStateUpdate();

        /// <summary>
        /// Finds a <see cref="IOrderedEnumerable{Character}"/> of all targets the spider can currently see.
        /// </summary>
        /// <returns> Returns a ordered <see cref="IOrderedEnumerable{Character}"/> of all of the <see cref="Characer"/>'s the spider can currently see. </returns>
        public IOrderedEnumerable<Character> FindVisibleTargets()
        {
            List<Character> visibleTargets = new List<Character>();

            foreach (Character character in SpiderAI.TargetList.Values)
            {
                Vector3 targetDirection = (character.transform.position - SpiderAI.transform.position);
                targetDirection.Normalize();

                RaycastHit raycastHit;
                Physics.Raycast(SpiderAI.transform.position, targetDirection, out raycastHit, SpiderAI.VisionDistance);

                // If the raycast saw a character and the character it saw was the one we were trying to look at.
                if (SpiderAI.TargetList.ContainsKey(raycastHit.collider) && SpiderAI.TargetList[raycastHit.collider] == character)
                {
                    visibleTargets.Add(character);
                }
            }

            return visibleTargets.OrderBy(x => (x.gameObject.transform.position - SpiderAI.gameObject.transform.position).magnitude);
        }

        /// <summary>
        /// Tell the Spider to randomly wander to somewhere else.
        /// </summary>
        public void RandomWander(float wanderDistance)
        {
            // The location we want to wander to.
            Vector3 desiredWanderLocation = Random.insideUnitSphere * wanderDistance + SpiderAI.transform.position;

            NavMeshHit navHit;
            NavMesh.SamplePosition(desiredWanderLocation, out navHit, wanderDistance, NavMesh.AllAreas);

            SpiderAI.NavigationAgent.SetDestination(navHit.position);
        }
    }
}