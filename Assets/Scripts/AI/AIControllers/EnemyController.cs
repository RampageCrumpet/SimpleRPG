using System.Collections.Generic;
using UnityEngine;

namespace SimpleRPG.AI
{
    public enum EnemyState { Idle, Chase, Attack }

    /// <summary>Conventional melee behavior composed from shared states. Other attack styles can supply a different controller.</summary>
    public class EnemyController : AIController
    {
        [SerializeField, Min(0)] private float activationDuration;
        private double readyAt;
        private Dictionary<EnemyState, IState> states;
        public EnemyState CurrentState { get; private set; }

        protected override StateMachine CreateStateMachine()
        {
            states = new Dictionary<EnemyState, IState>
            {
                { EnemyState.Idle, new IdleState(this) },
                { EnemyState.Chase, new ChaseState(this) },
                { EnemyState.Attack, new MeleeAttackState(this, () => Enter(EnemyState.Chase)) }
            };
            readyAt = ServerClock.Now + activationDuration;
            var machine = new StateMachine();
            machine.InitializeStateMachine(states[EnemyState.Idle]);
            return machine;
        }

        protected override void Update()
        {
            if (ServerClock.Now >= readyAt) base.Update();
        }

        public void Enter(EnemyState state)
        {
            if (IsDead || CurrentState == state) return;
            CurrentState = state;
            ChangeState(states[state]);
        }
    }
}
