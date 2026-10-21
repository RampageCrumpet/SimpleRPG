using NUnit.Framework;
using SimpleRPG.NetworkTools;
using Unity.Netcode;
using UnityEditor;
using UnityEngine;

namespace SimpleRPG.Tests.EditMode
{
    /// <summary>
    /// The default network prefab list must not keep test-only prefabs or repeated entries.
    /// </summary>
    public class NetworkPrefabListGuardTests
    {
        private const string KeyPickup = "Assets/Prefabs/Interactables/KeyPickup.prefab";
        private const string TestKeyRing = "Assets/Tests/PlayMode/Prefabs/TestKeyRing.prefab";

        [Test]
        public void Clean_RemovesTestPrefabs_AndRepeats_ButKeepsOneOfEach()
        {
            var list = ScriptableObject.CreateInstance<NetworkPrefabsList>();
            try
            {
                var key = AssetDatabase.LoadAssetAtPath<GameObject>(KeyPickup);
                var ring = AssetDatabase.LoadAssetAtPath<GameObject>(TestKeyRing);
                list.Add(new NetworkPrefab { Prefab = key });
                list.Add(new NetworkPrefab { Prefab = key });
                list.Add(new NetworkPrefab { Prefab = ring });

                Assert.IsTrue(NetworkPrefabListGuard.Clean(list));

                Assert.AreEqual(1, list.PrefabList.Count);
                Assert.AreSame(key, list.PrefabList[0].Prefab);
                Assert.IsFalse(NetworkPrefabListGuard.Clean(list), "A clean list should be left alone.");
            }
            finally
            {
                Object.DestroyImmediate(list);
            }
        }
    }
}
