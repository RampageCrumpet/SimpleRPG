using SimpleRPG;
using UnityEngine;
using UnityEngine.AI;

namespace SimpleRPG.Animations
{
    /// <summary>Adapts character events to configured animators; gameplay and animation state remain independent.</summary>
    [RequireComponent(typeof(Character))]
    public class AnimatableCharacter : MonoBehaviour
    {
        [SerializeField] private string activationTrigger;
        [SerializeField] private string deathTrigger;
        private Animator[] animators;
        private Character character;
        private CharacterController characterController;
        private NavMeshAgent agent;
        private MeleeDelivery melee;
        private Vector3 lastPosition;

        private void Start()
        {
            character = GetComponent<Character>();
            animators = GetComponentsInChildren<Animator>(true);
            characterController = GetComponent<CharacterController>();
            agent = GetComponent<NavMeshAgent>();
            melee = GetComponent<MeleeDelivery>();
            lastPosition = transform.position;
            character.OnTakeDamage += UpdateTakeDamage;
            character.OnDeath += Die;
            if (melee != null) melee.MeleeAttackStarted += MeleeAttack;
            if (!character.IsAlive) Die();
            else Trigger(activationTrigger);
        }

        private void Update()
        {
            Vector3 velocity = Time.deltaTime > 0 ? (transform.position - lastPosition) / Time.deltaTime : Vector3.zero;
            if (agent != null && agent.enabled && agent.isOnNavMesh) velocity = agent.velocity;
            else if (characterController != null && characterController.enabled) velocity = characterController.velocity;
            lastPosition = transform.position;
            foreach (Animator animator in animators)
            {
                SetFloat(animator, "ForwardMovementSpeed", Vector3.Dot(velocity, transform.forward));
                SetFloat(animator, "HorizontalMovementSpeed", Vector3.Dot(velocity, transform.right));
                SetFloat(animator, "Speed", character.IsAlive ? velocity.magnitude : 0);
                foreach (var parameter in animator.parameters)
                    if (parameter.name == "Walk" && parameter.type == AnimatorControllerParameterType.Bool)
                        animator.SetBool("Walk", character.IsAlive && velocity.sqrMagnitude > 0.0025f);
            }
        }

        private static void SetFloat(Animator animator, string name, float value)
        {
            foreach (var parameter in animator.parameters)
                if (parameter.name == name && parameter.type == AnimatorControllerParameterType.Float) animator.SetFloat(name, value);
        }

        private void Trigger(string name)
        {
            if (string.IsNullOrEmpty(name)) return;
            foreach (Animator animator in animators)
                foreach (var parameter in animator.parameters)
                    if (parameter.name == name && parameter.type == AnimatorControllerParameterType.Trigger) animator.SetTrigger(name);
        }

        private void UpdateTakeDamage() { if (character.IsAlive) Trigger("TakeDamage"); }
        private void Die()
        {
            // Discard queued attack triggers so a death blend cannot restart an attack.
            foreach (Animator animator in animators)
                foreach (var parameter in animator.parameters)
                    if (parameter.type == AnimatorControllerParameterType.Trigger) animator.ResetTrigger(parameter.name);
            Trigger(deathTrigger);
        }
        private void MeleeAttack(int attackNumber) { if (character.IsAlive) Trigger(melee.AnimationTrigger(attackNumber)); }
        public static string GetAttackTrigger(int attackNumber) => attackNumber <= 1 ? "Attack_OneHanded" : "ComboQueued";

        private void OnDestroy()
        {
            if (character != null)
            {
                character.OnTakeDamage -= UpdateTakeDamage;
                character.OnDeath -= Die;
            }
            if (melee != null) melee.MeleeAttackStarted -= MeleeAttack;
        }
    }
}
