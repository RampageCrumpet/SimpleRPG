using Unity.Netcode;
using UnityEngine;

[System.Serializable]
public class DamageInfo : INetworkSerializable
{
    /// <summary>
    /// The damage value to be applied.
    /// </summary>
    [Tooltip("The damage value to apply.")]
    [SerializeField]
    public int Damage;

    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
    {
        serializer.SerializeValue(ref Damage);
    }
}
