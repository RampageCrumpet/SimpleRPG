using UnityEngine;

/// <summary>
/// Switches animator layers off while this state plays and fades them back in when the state hands over to a state that is not tagged "Dead".
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
        SetWeights(animator, 0f);
    }

    /// <inheritdoc/>
    public override void OnStateUpdate(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
    {
        // While this state blends out into a living state, bring the layers back in step with the blend.
        if (animator.IsInTransition(layerIndex) && !animator.GetNextAnimatorStateInfo(layerIndex).IsTag(DeadTag))
        {
            SetWeights(animator, animator.GetAnimatorTransitionInfo(layerIndex).normalizedTime);
        }
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
}
