using System;
using Unity.Netcode;
using UnityEngine;

namespace SimpleRPG
{
    public abstract class NetworkSerializableScriptableObject : ScriptableObject, INetworkSerializable
    {
        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            if (serializer.IsWriter)
            {
                SerializeData(serializer);
            }
            else
            if (serializer.IsReader)
            {
                DeserializeData(serializer);
            }
        }

        protected abstract void SerializeData<T>(BufferSerializer<T> serializer) where T : IReaderWriter;
        protected abstract void DeserializeData<T>(BufferSerializer<T> serializer) where T : IReaderWriter;

        public static T CreateInstanceFromSerializedData<T>(BufferSerializer<IReaderWriter> serializer) where T : NetworkSerializableScriptableObject
        {
            string typeName = default;
            serializer.SerializeValue(ref typeName);

            Type type = Type.GetType(typeName);
            if (type == null || !typeof(ScriptableObject).IsAssignableFrom(type))
            {
                Debug.LogError($"Invalid type for deserialization: {typeName}");
                return null;
            }

            var instance = (T)ScriptableObject.CreateInstance(type);
            instance.NetworkSerialize(serializer); // Deserialize the data into the new instance, we can't use the new keyword to create new instances of a scriptable object.
            return instance;
        }
    }
}
