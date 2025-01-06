using NUnit.Framework;
using PlasticPipe.PlasticProtocol.Messages;
using SimpleRPG;
using SimpleRPG.AI;
using SimpleRPG.Animations;
using System.Collections.Generic;
using System.Linq;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.AI;

namespace AI
{
    /// <summary>
    /// This class represents the base class for an AI controlled by a state machine.
    /// </summary>
    [RequireComponent(typeof(AnimatableCharacter))]
    [RequireComponent(typeof(Character))]
    public abstract class AIController : NetworkBehaviour
    {

        /// <summary>
        /// The <see cref="Character"/> responsible for handling our health and abilities.
        /// </summary>
        public Character Character { get; set; }

        /// <summary>
        /// The <see cref="StateMachine"/> responsible for driving our AI.
        /// </summary>
        protected StateMachine AIStateMachine { get; set; }

        /// <summary>
        /// The <see cref="AnimatableCharacter"/> responsible for handling our animations.
        /// </summary>
        protected AnimatableCharacter AnimatableCharacter { get; set; }

        /// <summary>
        /// The <see cref="NavMeshAgent"/> responsible for controlling our pathing.
        /// </summary>
        protected NavMeshAgent NavMeshAgent { get; set; }

        /// <summary>
        /// A collection of our senses capable of actually detecting the player.
        /// </summary>
        public IEnumerable<PrimarySense> PrimarySenses { get; set; }

        /// <summary>
        /// A collection of our senses capable of detecting the player indirectly.
        /// </summary>
        public IEnumerable<SecondarySense> SecondarySenses { get; set; }

        /// <summary>
        /// A place holder for attack range.
        /// TODO: This should be replaced by having a collection of weapons that can be used.
        /// </summary>
        public float AttackRange;

        // Start is called once before the first execution of Update after the MonoBehaviour is created
        protected virtual void Start()
        {
            PrimarySenses = GetComponents<PrimarySense>();
            SecondarySenses = GetComponents<SecondarySense>();
            AnimatableCharacter = GetComponent<AnimatableCharacter>();
            NavMeshAgent = GetComponent<NavMeshAgent>();
            Character = GetComponent<Character>();
            Character.Initialize();
            AIStateMachine = CreateStateMachine();
        }

        // Update is called once per frame
        protected virtual void Update()
        {
            if (this.IsServer || this.IsHost)
            {
                AIStateMachine.Update();
            }
        }

        /// <summary>
        /// Changes the state of our state machine to the given state.
        /// </summary>
        /// <param name="nextState"> The state we want to change to.</param>
        public void ChangeState(IState nextState)
        {
            AIStateMachine.ChangeState(nextState);
        }

        /// <summary>
        /// Searches for targets with our primary senses.
        /// </summary>
        public IEnumerable<Character> SenseTargets()
        {
            IEnumerable<Character> sensedTargets = PrimarySenses.SelectMany(x => x.Sense());
            return sensedTargets;
        }

        /// <summary>
        /// Sets the destination of the navigationAgent to the targeted destination.
        /// </summary>
        /// <param name="position"> The location we want to go to in world space.</param>
        public void SetDestination(Vector3 position)
        {
            NavMeshAgent.SetDestination(position);
        }

        /// <summary>
        /// Create's the state machine that will drive our AI.
        /// </summary>
        /// <returns> The state machine this controller will use to drive it's behavior.</returns>
        protected abstract StateMachine CreateStateMachine();
    }
}
