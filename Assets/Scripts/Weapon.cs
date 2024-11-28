using HitDetection;
using SimpleRPG;
using UnityEngine;

public class Weapon : MonoBehaviour
{
    /// <summary>
    /// The hitbox used to determine if this weapon hit anything.
    /// </summary>
    private Hitbox hitBox;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        hitBox.HitboxCollisionEvent.AddListener(TargetHit);
    }

    // Update is called once per frame
    void Update()
    {

    }

    void TargetHit(Character character, BodyLocation location)
    {
    }
}