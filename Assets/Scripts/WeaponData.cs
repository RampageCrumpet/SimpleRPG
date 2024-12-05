using HitDetection;
using Inventory;
using SimpleRPG;
using Unity.Netcode;
using UnityEngine;

/// <summary>
/// This scriptable object represents a weapons stat sheet.
/// </summary>
[CreateAssetMenu(fileName = "WeaponData", menuName = "Items/WeaponData")]
public class WeaponData : Item, INetworkSerializable
{
    /// <summary>
    /// The total damage dealt by this weapon.
    /// </summary>
    [SerializeField]
    public DamageInfo damage;

    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
    {
        serializer.SerializeValue(ref damage);
    }
}