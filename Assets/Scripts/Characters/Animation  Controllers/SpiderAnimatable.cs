using Codice.CM.Common;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(NavMeshAgent))]
public class SpiderAnimatable : Animatable
{
    /// <summary>
    /// The <see cref="NavMeshAgent"/> used to move this object.
    /// </summary>
    private NavMeshAgent agent;

    /// <inheritdoc/>
    public override void Die()
    {
        animator.SetTrigger("Die");
    }

    /// <inheritdoc/>
    public override void Flinch()
    {
        animator.SetTrigger("Flinch");
    }

    public override void Attack()
    {
        animator.SetTrigger("Attack");
    }

    // Start is called before the first frame update
    new void Start()
    {
        base.Start();
        agent = this.GetComponent<NavMeshAgent>();
    }

    // Update is called once per frame
    void Update()
    {
        // Update the animator on the current movement speed of current object.
        animator.SetFloat("Move_Speed", agent.velocity.magnitude);
    }
}
