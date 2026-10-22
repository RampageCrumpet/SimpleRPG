using SimpleRPG;
using SimpleRPG.AI;
using System.Collections.Generic;
using System.Linq;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.AI;

namespace AI
{
    /// <summary>Shared server-owned perception, target tracking, navigation and terminal lifecycle.</summary>
    [RequireComponent(typeof(Character))]
    public abstract class AIController : NetworkBehaviour
    {
        public Character Character { get; set; }
        protected StateMachine AIStateMachine { get; set; }
        protected NavMeshAgent NavMeshAgent { get; set; }
        public IEnumerable<PrimarySense> PrimarySenses { get; set; }
        public IEnumerable<SecondarySense> SecondarySenses { get; set; }
        [Min(0.1f)] public float AttackRange = 1.5f;
        public Character Target { get; private set; }
        public bool IsDead { get; private set; }

        protected virtual void Start()
        {
            PrimarySenses = GetComponents<PrimarySense>();
            SecondarySenses = GetComponents<SecondarySense>();
            NavMeshAgent = GetComponent<NavMeshAgent>();
            Character = GetComponent<Character>();
            if (!IsServer)
            {
                if (NavMeshAgent != null) NavMeshAgent.enabled = false;
                return;
            }
            if (NavMeshAgent != null) NavMeshAgent.enabled = true;
            Character.OnDeath += Die;
            AIStateMachine = CreateStateMachine();
            if (!Character.IsAlive) Die();
        }

        protected virtual void Update()
        {
            if (!IsServer || AIStateMachine == null || IsDead) return;
            if (!Character.IsAlive) { Die(); return; }
            AIStateMachine.Update();
        }

        public override void OnNetworkDespawn()
        {
            if (Character != null) Character.OnDeath -= Die;
            StopMoving();
        }

        private void Die()
        {
            if (IsDead) return;
            IsDead = true;
            Target = null;
            StopMoving();
            if (NavMeshAgent != null) NavMeshAgent.enabled = false;
            var body = GetComponent<CapsuleCollider>();
            if (body != null) body.enabled = false;
            AIStateMachine?.ChangeState(new DeadState());
        }

        public void ChangeState(IState state)
        {
            if (!IsDead) AIStateMachine.ChangeState(state);
        }

        public IEnumerable<Character> SenseTargets() => PrimarySenses
            .SelectMany(s => s.Sense()).Where(c => c != null && c != Character && c.IsAlive).Distinct();

        public bool TrackTarget()
        {
            var sensed = SenseTargets().ToArray();
            if (Target == null || !Target.IsAlive || !sensed.Contains(Target))
                Target = sensed.OrderBy(c => (c.transform.position - transform.position).sqrMagnitude).FirstOrDefault();
            return Target != null;
        }

        public bool InAttackRange => Target != null && Vector3.Distance(transform.position, Target.transform.position) <= AttackRange;
        public bool IsFacingTarget
        {
            get
            {
                if (Target == null) return false;
                Vector3 direction = Target.transform.position - transform.position;
                direction.y = 0;
                return direction.sqrMagnitude < 0.001f || Vector3.Dot(transform.forward, direction.normalized) > 0.95f;
            }
        }

        public void SetDestination(Vector3 position)
        {
            if (NavMeshAgent != null && NavMeshAgent.enabled && NavMeshAgent.isOnNavMesh)
            {
                NavMeshAgent.isStopped = false;
                NavMeshAgent.SetDestination(position);
            }
        }

        public void StopMoving()
        {
            if (NavMeshAgent != null && NavMeshAgent.enabled && NavMeshAgent.isOnNavMesh)
            {
                NavMeshAgent.isStopped = true;
                NavMeshAgent.ResetPath();
            }
        }

        public void FaceTarget()
        {
            if (Target == null) return;
            Vector3 direction = Target.transform.position - transform.position;
            direction.y = 0;
            if (direction.sqrMagnitude > 0.001f)
                transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(direction), 180f * Time.deltaTime);
        }

        protected abstract StateMachine CreateStateMachine();
    }
}
