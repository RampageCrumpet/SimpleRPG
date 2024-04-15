using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public abstract class Animatable : MonoBehaviour
{
    protected Animator animator;

    public void Start()
    {
        this.animator = GetComponentInChildren<Animator>();

        if (animator == null)
        {
            Debug.LogError("No " + nameof(Animator) + " found in " + gameObject.name + "'s children");
        }
    }

    /// <summary>
    /// Call the death animation to play.
    /// </summary>
    public abstract void Die();

    /// <summary>
    /// Call the flinch animation to play.
    /// </summary>
    public abstract void Flinch();

    /// <summary>
    /// Call the flinch animation to play.
    /// </summary>
    public abstract void Attack();
}
