using SimpleRPG;
using SimpleRPG.Abilities;
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
        private MeleeDeliveryBase melee;
        private Vector3 lastPosition;

        /// <summary>The state an attack layer rests in when no attack is playing.</summary>
        private const string AttackLayerEmptyState = "Empty";

        /// <summary>The state an attack layer holds while a weapon is carried and no attack is playing; attacks recover into it.</summary>
        private const string AttackLayerReadyState = "Longsword_Ready";

        private const float CancelBlendSeconds = 0.15f;
        private const float StanceBlendSeconds = 0.2f;
        private const string AttackStatePrefix = "Longsword_A";
        private Equipment equipment;

        private void Start()
        {
            character = GetComponent<Character>();
            animators = GetComponentsInChildren<Animator>(true);
            characterController = GetComponent<CharacterController>();
            agent = GetComponent<NavMeshAgent>();
            melee = GetComponent<MeleeDeliveryBase>();
            equipment = GetComponent<Equipment>();
            if (equipment != null) equipment.Changed += RefreshStance;
            lastPosition = transform.position;
            character.OnTakeDamage += UpdateTakeDamage;
            character.OnDeath += Die;
            character.OnRevive += Revive;
            if (melee != null)
            {
                melee.MeleeAttackStarted += MeleeAttack;
                melee.MeleeAttackCancelled += CancelAttack;
            }
            if (!character.IsAlive) Die();
            else
            {
                Trigger(activationTrigger);
                RefreshStance();
            }
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
        // A new life starts from the animators' default state, with no death pose or half-played attack left over.
        private void Revive()
        {
            foreach (Animator animator in animators)
            {
                animator.Rebind();
                animator.Update(0f);
            }

            Trigger(activationTrigger);
            RefreshStance();
        }

        private void MeleeAttack(int attackNumber, float secondsIntoAttack)
        {
            if (!character.IsAlive) return;

            AttackAnimation attack = melee.AnimationFor(attackNumber);
            if (attack.IsState) PlayState(attack, secondsIntoAttack);
            else Trigger(attack.Trigger);
        }

        // Plays an attack state directly, blending from whatever the layer holds now, so a chained attack cuts into the next without waiting for
        // transitions. An attack that is already part-way through (the news was late) joins the clip that far in.
        private void PlayState(AttackAnimation attack, float secondsIntoAttack)
        {
            int hash = Animator.StringToHash(attack.State);
            foreach (Animator animator in animators)
            {
                for (int layer = 0; layer < animator.layerCount; layer++)
                {
                    if (!animator.HasState(layer, hash)) continue;

                    animator.CrossFadeInFixedTime(hash, attack.BlendIn, layer, attack.StartOffset + secondsIntoAttack);
                    break;
                }
            }
        }

        // An attack shown ahead of the server's word did not happen: fade the layer that was playing it back to its empty state.
        private void CancelAttack()
        {
            int rest = RestStateHash();
            int empty = Animator.StringToHash(AttackLayerEmptyState);
            foreach (Animator animator in animators)
            {
                for (int layer = 0; layer < animator.layerCount; layer++)
                {
                    if (animator.HasState(layer, rest)) animator.CrossFadeInFixedTime(rest, CancelBlendSeconds, layer);
                    else if (animator.HasState(layer, empty)) animator.CrossFadeInFixedTime(empty, CancelBlendSeconds, layer);
                }
            }
        }

        // The ready stance while a weapon is carried, nothing otherwise. A character with no weapon gets no weapon posture.
        private int RestStateHash() => Animator.StringToHash(character.WeaponData != null ? AttackLayerReadyState : AttackLayerEmptyState);

        // Moves every attack layer that is resting (not mid-attack) to the stance that matches what the character carries.
        private void RefreshStance()
        {
            if (!character.IsAlive) return;

            int rest = RestStateHash();
            foreach (Animator animator in animators)
            {
                for (int layer = 0; layer < animator.layerCount; layer++)
                {
                    if (!animator.HasState(layer, rest)) continue;

                    AnimatorStateInfo now = animator.GetCurrentAnimatorStateInfo(layer);
                    bool inAttack = now.IsName(AttackStatePrefix + "1") || now.IsName(AttackStatePrefix + "2") || now.IsName(AttackStatePrefix + "3");
                    bool heading = animator.IsInTransition(layer) ? animator.GetNextAnimatorStateInfo(layer).shortNameHash == rest : now.shortNameHash == rest;
                    if (!inAttack && !heading) animator.CrossFadeInFixedTime(rest, StanceBlendSeconds, layer);
                }
            }
        }

        private void OnDestroy()
        {
            if (equipment != null) equipment.Changed -= RefreshStance;
            if (character != null)
            {
                character.OnTakeDamage -= UpdateTakeDamage;
                character.OnDeath -= Die;
                character.OnRevive -= Revive;
            }
            if (melee != null)
            {
                melee.MeleeAttackStarted -= MeleeAttack;
                melee.MeleeAttackCancelled -= CancelAttack;
            }
        }
    }
}
