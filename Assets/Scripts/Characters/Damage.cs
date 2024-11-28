using Unity.Netcode;
using UnityEngine;

public class DamageInfo : INetworkSerializable
{
    /// <summary>
    /// The damage value to be applied.
    /// </summary>
    [Tooltip("The damage value to apply.")]
    [field: SerializeField]
    public int Damage;

    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
    {
        serializer.SerializeValue(ref Damage);
    }
}
