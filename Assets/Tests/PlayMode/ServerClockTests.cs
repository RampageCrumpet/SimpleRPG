using NUnit.Framework;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

namespace SimpleRPG.Tests.PlayMode
{
    /// <summary>
    /// The shared timeline is server time while a session runs and Unity time otherwise, and a server-side timer that reads it (here a
    /// projectile's lifetime) is measured on it.
    /// </summary>
    public class ServerClockTests
    {
        private const string FireballPrefab = "Assets/Prefabs/Projectiles/Fireball.prefab";

        private GameObject managerObject;
        private NetworkManager manager;
        private readonly List<GameObject> spawned = new List<GameObject>();

        [TearDown]
        public void TearDown()
        {
            foreach (GameObject go in spawned) if (go != null) Object.DestroyImmediate(go);
            spawned.Clear();

            if (manager != null && manager.IsListening) manager.Shutdown();
            if (managerObject != null) Object.DestroyImmediate(managerObject);
            manager = null;
            managerObject = null;
        }

        private void StartHost(GameObject registerPrefab = null)
        {
            managerObject = new GameObject("TestNetworkManager");
            UnityTransport transport = managerObject.AddComponent<UnityTransport>();
            manager = managerObject.AddComponent<NetworkManager>();
            manager.NetworkConfig = new NetworkConfig { NetworkTransport = transport };
            if (registerPrefab != null) manager.AddNetworkPrefab(registerPrefab);
            Assert.IsTrue(manager.StartHost(), "The test host should start.");
        }

        [UnityTest]
        public IEnumerator WithNoSession_TheClockFollowsUnityTime()
        {
            yield return null;

            Assert.IsNull(NetworkManager.Singleton);
            Assert.AreEqual(Time.timeAsDouble, ServerClock.Now, 0.5, "with no session there is no server time, so timers still run on Unity's");
        }

        [UnityTest]
        public IEnumerator WithASessionRunning_TheClockIsServerTime_AndItAdvances()
        {
            StartHost();
            yield return null;

            Assert.AreEqual(manager.ServerTime.Time, ServerClock.Now, 0.2, "the shared timeline is the server's time");

            double before = ServerClock.Now;
            yield return new WaitForSeconds(0.5f);

            Assert.Greater(ServerClock.Now - before, 0.3, "and it keeps moving");
        }

        [UnityTest]
        public IEnumerator WhenTheSessionEnds_TheClockFallsBackToUnityTime()
        {
            StartHost();
            yield return null;
            manager.Shutdown();
            yield return new WaitUntil(() => !manager.IsListening);

            Assert.AreEqual(Time.timeAsDouble, ServerClock.Now, 0.5);
        }

        [UnityTest]
        public IEnumerator AProjectile_ExpiresAfterItsLifeSpan_MeasuredOnServerTime()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(FireballPrefab);
            Assert.IsNotNull(prefab, "the fireball prefab loads");
            StartHost(prefab);
            yield return null;

            const float LifeSpan = 0.5f;
            GameObject instance = Object.Instantiate(prefab, new Vector3(0f, 5f, 0f), Quaternion.identity);
            spawned.Add(instance);
            typeof(Projectile).GetField("lifeSpan", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(instance.GetComponent<Projectile>(), LifeSpan);
            instance.GetComponent<NetworkObject>().Spawn();

            Projectile projectile = instance.GetComponent<Projectile>();
            bool expired = false;
            double expiredAt = 0;
            float ageAtExpiry = 0;
            projectile.Expired += () =>
            {
                expired = true;
                expiredAt = ServerClock.Now;
                ageAtExpiry = projectile.Age;
            };
            double spawnedAt = ServerClock.Now;

            float deadline = Time.realtimeSinceStartup + 4f;
            while (!expired && Time.realtimeSinceStartup < deadline) yield return null;

            Assert.IsTrue(expired, "the projectile ran out of lifetime and reported it");
            Assert.GreaterOrEqual(expiredAt - spawnedAt, LifeSpan - 0.15, "not early: a lifetime is a lifetime on the shared clock");
            Assert.Less(expiredAt - spawnedAt, LifeSpan + 1.0, "and not long after");
            Assert.GreaterOrEqual(ageAtExpiry, LifeSpan - 0.05f, "its age is read on the same clock");
        }
    }
}
