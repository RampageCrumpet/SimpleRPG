using AI;
using Unity.Netcode;
using UnityEngine;

namespace SimpleRPG
{
    public class GremlinController : AIController
    {
        // Start is called once before the first execution of Update after the MonoBehaviour is created
        protected override void Start() 
        {
            base.Start();
            this.GetComponent<NetworkObject>().Spawn();
        }

        // Update is called once per frame
        protected override void Update()
        {
            base.Update();
        }

        /// <inheritdoc/>
        protected override StateMachine CreateStateMachine()
        {
            ChaseState chaseState = new ChaseState();
            MeleeAttackState attackState = new MeleeAttackState();
            DeadState deadState = new DeadState();
            IdleState idleState = new IdleState();

            chaseState.Initialize(this, deadState, attackState, idleState);
            attackState.Initialize(this, deadState, idleState);
            deadState.Initialize(this);
            idleState.Initialize(this, deadState, chaseState);

            StateMachine stateMachine = new StateMachine();
            stateMachine.InitializeStateMachine(idleState);

            return stateMachine;
        }
    }
}
