using HitDetection;
using SimpleRPG.InventorySystem;
using SimpleRPG;
using System;
using UnityEngine;

/// <summary>
/// This scriptable object represents a weapons stat sheet.
/// </summary>
[CreateAssetMenu(fileName = "WeaponData", menuName = "Items/WeaponData")]
[System.Serializable]
public class WeaponData : Item
{
    /// <summary>
    /// The total damage dealt by this weapon.
    /// </summary>
    [SerializeField]
    public DamageInfo damage;

}
