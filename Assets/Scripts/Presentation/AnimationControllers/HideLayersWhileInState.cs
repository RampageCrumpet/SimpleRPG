using UnityEngine;

namespace SimpleRPG
{
    /// <summary>
    /// Fades animator layers out on entry, keeps them off while dead, and fades them back in on return to a living state.
    /// Put it on the skeleton's Die and Rise states so the weapon and off-hand layers do not hold the arms in the ready pose while the bones fall apart.
    /// </summary>
    public class HideLayersWhileInState : StateMachineBehaviour
    {
        private const string DeadTag = "Dead";

        [SerializeField]
        private string[] layerNames = { "Weapon_Arm", "Weapon_Attack", "OffHand_Attack" };

        /// <inheritdoc/>
        public override void OnStateEnter(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
        {
            UpdateWeights(animator, layerIndex);
        }

        /// <inheritdoc/>
        public override void OnStateUpdate(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
        {
            UpdateWeights(animator, layerIndex);
        }

        /// <inheritdoc/>
        public override void OnStateExit(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
        {
            // Going from Die to Rise keeps the layers hidden.
            if (!animator.GetCurrentAnimatorStateInfo(layerIndex).IsTag(DeadTag))
            {
                SetWeights(animator, 1f);
            }
        }

        private void SetWeights(Animator animator, float weight)
        {
            foreach (string layerName in layerNames)
            {
                int index = animator.GetLayerIndex(layerName);
                if (index >= 0)
                {
                    animator.SetLayerWeight(index, weight);
                }
            }
        }

        private void UpdateWeights(Animator animator, int layerIndex)
        {
            float weight = 0f;
            if (animator.IsInTransition(layerIndex))
            {
                float progress = Mathf.Clamp01(animator.GetAnimatorTransitionInfo(layerIndex).normalizedTime);
                if (!animator.GetNextAnimatorStateInfo(layerIndex).IsTag(DeadTag))
                    weight = progress;
                else if (!animator.GetCurrentAnimatorStateInfo(layerIndex).IsTag(DeadTag))
                    weight = 1f - progress;
            }
            SetWeights(animator, weight);
        }
    }
}
