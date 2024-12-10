using HitDetection;
using Inventory;
using SimpleRPG;
using System;
using Unity.Netcode;
using UnityEngine;

/// <summary>
/// This scriptable object represents a weapons stat sheet.
/// </summary>
[CreateAssetMenu(fileName = "WeaponData", menuName = "Items/WeaponData")]
[System.Serializable]
public class WeaponData : Item, INetworkSerializable
{
    /// <summary>
    /// The total damage dealt by this weapon.
    /// </summary>
    [SerializeField]
    public DamageInfo damage;


    /// <summary>
    /// Serializes data for writing.
    /// </summary>
    protected override void SerializeData<T>(BufferSerializer<T> serializer)
    {
        base.SerializeData(serializer);
        serializer.SerializeNetworkSerializable(ref damage);
    }

    /// <summary>
    /// Deserializes data for reading.
    /// </summary>
    protected override void DeserializeData<T>(BufferSerializer<T> serializer)
    {
        base.DeserializeData(serializer);
        serializer.SerializeNetworkSerializable(ref damage);
    }
}