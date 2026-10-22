using UnityEngine;
using UnityEngine.AI;

namespace SimpleRPG.Animations
{
    /// <summary>Aligns an actor's feet with geometry instead of the voxelized navigation surface.</summary>
    [RequireComponent(typeof(NavMeshAgent))]
    public sealed class NavigationGrounding : MonoBehaviour
    {
        [SerializeField] private LayerMask groundLayers;
        [SerializeField, Min(.01f)] private float maximumCorrection = .3f;
        private NavMeshAgent agent;

        private void Awake() => agent = GetComponent<NavMeshAgent>();

        private void LateUpdate()
        {
            if (!agent.enabled || !agent.isOnNavMesh) return;
            Vector3 position = transform.position;
            if (!Physics.Raycast(position + Vector3.up * maximumCorrection, Vector3.down,
                    out RaycastHit hit, maximumCorrection * 2, groundLayers, QueryTriggerInteraction.Ignore)) return;
            float correction = hit.point.y - position.y;
            if (Mathf.Abs(correction) > .001f && Mathf.Abs(correction) < maximumCorrection)
                agent.baseOffset += correction;
        }
    }
}
