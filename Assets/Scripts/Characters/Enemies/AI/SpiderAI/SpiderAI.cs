using NUnit.Framework.Internal;
using SimpleRPG;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.AI;

namespace AI
{
    /// <summary>
    /// AI controller for spiders.
    /// </summary>
    [RequireComponent(typeof(NavMeshAgent))]
    [RequireComponent (typeof(Attack))]
    public class SpiderAI : Character
    {
        /// <summary>
        /// The state machine used for the spiders decision making.
        /// </summary>
        public StateMachine SpiderStateMachine { get; private set; }

        /// <summary>
        /// The distance away from a character the player has to get before the spider gives.
        /// </summary>
        [field: SerializeField]
        [Tooltip("The maximum distance away from a character the spider can be before it gives up.")]
        public float MaximumFollowDistance {  get; private set; }

        /// <summary>
        /// The maximum distance a spider can see a character from.
        /// </summary>
        [field: SerializeField]
        [Tooltip("The maximum distance the spider can see a character from.")]
        public float VisionDistance { get; private set; }

        /// <summary>
        /// The spiderweb we want to spawn when placing webs.
        /// </summary>
        [field: SerializeField]
        [Tooltip("The spiderweb object we want to place for our webs.")]
        public GameObject SpiderwebPrefab { get; private set; }

        /// <summary>
        /// The minimum number of spider webs our spider will have before it will try to build more webs.
        /// </summary>
        [field: SerializeField]
        [Tooltip("The minimum number of spider webs that we want to build.")]
        public int MinimumNumberOfSpiderWebs { get; private set; }

        /// <summary>
        /// The <see cref="Attack"/> that this spider uses.
        /// </summary>
        public Attack Attack { get; private set; }

        /// <summary>
        /// The nest that the spider returns to.
        /// </summary>
        public SpiderNest Nest { get; private set; }

        /// <summary>
        /// The <see cref="NavMeshAgent"/> we use to navigate our <see cref="NavMesh"/>.
        /// </summary>
        public NavMeshAgent NavigationAgent { get; private set; }

        /// <summary>
        /// The list of targets within the spiders local area.
        /// </summary>
        public Dictionary<Collider, Character> TargetList { get; private set; } = new Dictionary<Collider, Character>();

        /// <summary>
        /// The spiderwebs this spider is monitoring.
        /// </summary>
        public List<Spiderweb> spiderwebs = new List<Spiderweb>();

        public Animatable animatable
        {
            get;
            private set;
        }

        // Start is called before the first frame update
        void Start()
        {
            NavigationAgent = GetComponent<NavMeshAgent>();
            Attack = GetComponent<Attack>();
            animatable = GetComponent<Animatable>();

            SpiderStateMachine = new StateMachine();
            SpiderStateMachine.InitializeStateMachine(new SpiderStateMakeNewNest(this));
        }

        // Update is called once per frame
        void Update()
        {
            SpiderStateMachine.Update();
        }

        /// <summary>
        /// Track objects inside of the spiders local area.
        /// </summary>
        /// <param name="other">The collider of the object who's entered the local area.</param>
        public void OnTriggerEnter(Collider other)
        {
            Character otherCharacter = other.gameObject.GetComponent<Character>();

            // Add the character within our trigger distance to our list of possible targets.
            if(otherCharacter != null)
            {
                TargetList.Add(other, otherCharacter);
            }
        }

        /// <summary>
        /// Track objects leaving the spiders local area.
        /// </summary>
        /// <param name="other">The collider of the object that left the local area.</param>
        public void OnTriggerExit(Collider other)
        {
            Character otherCharacter = other.gameObject.GetComponent<Character>();

            // Remove the character that left our trigger from our list of possible targets.
            if(otherCharacter != null)
            {
                TargetList.Remove(other);
            }
        }

        /// <summary>
        /// Finds the most suitable location within range..
        /// </summary>
        /// <returns> Returns the most suitable <see cref="SpiderNest"/> within range of the spider. If no nest is found it will return null.</returns>
        public void FindNestLocationWithinRange(float nestDetectionRange)
        {
            // Find all nests within range.
            IEnumerable<SpiderNest> nestLocations = Physics.OverlapSphere(this.transform.position, nestDetectionRange, LayerMask.GetMask("TriggerLocations"), QueryTriggerInteraction.Collide).Select(x => x.gameObject.GetComponent<SpiderNest>()).Where(x => x != null);

            Nest = nestLocations.Where(x => x != null).OrderByDescending(x => x.NestSuitability).Where(x => !x.IsOwned).FirstOrDefault();
        }

        [Rpc(SendTo.Everyone)]
        public void SetDestinationRPC(Vector3 targetLocation)
        {
            NavigationAgent.SetDestination(targetLocation);
        }
    }
}