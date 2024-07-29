using SimpleRPG;
using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

public class Attack : MonoBehaviour
{
    /// <summary>
    /// The distance this attack can land at.
    /// </summary>
    [Tooltip("The distance this spider can attack at.")]
    [field: SerializeField]
    public float AttackDistance { get; private set; }

    /// <summary>
    /// The time before this attack lands.
    /// </summary>
    [Tooltip("The time before an attack where a character has to wind up.")]
    [field: SerializeField]
    public float WindUpTime { get; private set; }

    /// <summary>
    /// The time after this attack lands before another attack can be made.
    /// </summary>
    [Tooltip("The time after an attack lands before another attack can be made.")]
    [field: SerializeField]
    public float CooldownTime { get; private set; }

    [Rpc(SendTo.Everyone)]
    public void AttackCharacterRPC(Character character)
    {
        Debug.Log(character.gameObject.name + " has been attacked!");
    }
}
