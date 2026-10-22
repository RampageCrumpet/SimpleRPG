using NUnit.Framework;
using SimpleRPG.Abilities.Integration;
using SimpleRPG.Gameplay;
using System.Collections;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;
using UnityEngine.TestTools;

namespace SimpleRPG.Abilities.Tests.PlayMode
{
    /// <summary>
    /// Checks the gameplay clock against a real host NetworkManager and Netcode's tick system.
    /// </summary>
    public class NetworkGameplayClockTests
    {
        private GameObject managerObject;
        private NetworkManager manager;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            managerObject = new GameObject("TestNetworkManager");
            UnityTransport transport = managerObject.AddComponent<UnityTransport>();
            manager = managerObject.AddComponent<NetworkManager>();
            manager.NetworkConfig = new NetworkConfig { NetworkTransport = transport };

            Assert.IsTrue(manager.StartHost(), "The test host should start.");
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (manager != null && manager.IsListening)
            {
                manager.Shutdown();
                yield return new WaitUntil(() => !manager.IsListening);
            }

            if (managerObject != null) Object.Destroy(managerObject);

            yield return null;
        }

        [Test]
        public void Clock_BeforeTheNetworkRuns_ReportsTickZero()
        {
            IGameplayClock clock = new NetworkGameplayClock(null);

            Assert.AreEqual(0, clock.Tick);
            Assert.AreEqual(0, clock.ElapsedSeconds);
        }

        [Test]
        public void Clock_TickRate_MatchesTheNetworkConfiguration()
        {
            IGameplayClock clock = new NetworkGameplayClock(manager);

            Assert.AreEqual((int)manager.NetworkConfig.TickRate, clock.TickRate);
            Assert.Greater(clock.TickRate, 0);
        }

        [UnityTest]
        public IEnumerator Clock_AdvancesWithRealTimeAtTheTickRate()
        {
            IGameplayClock clock = new NetworkGameplayClock(manager);
            long startTick = clock.Tick;
            double startSeconds = clock.ElapsedSeconds;

            yield return new WaitForSeconds(1f);

            long ticksPassed = clock.Tick - startTick;
            double secondsPassed = clock.ElapsedSeconds - startSeconds;

            Assert.AreEqual(clock.TickRate, ticksPassed, clock.TickRate * 0.2, "About one second of ticks should pass in one second.");
            Assert.AreEqual(1.0, secondsPassed, 0.2);
        }

        [UnityTest]
        public IEnumerator Clock_NeverRunsBackwards()
        {
            IGameplayClock clock = new NetworkGameplayClock(manager);
            long last = clock.Tick;

            for (int frame = 0; frame < 60; frame++)
            {
                yield return null;
                Assert.GreaterOrEqual(clock.Tick, last);
                last = clock.Tick;
            }
        }

        [UnityTest]
        public IEnumerator Cooldown_OnTheNetworkClock_ExpiresAfterItsDuration()
        {
            IGameplayClock clock = new NetworkGameplayClock(manager);
            CooldownTracker cooldowns = new CooldownTracker(clock);
            DefinitionId ability = DefinitionId.NewId();

            Assert.IsTrue(cooldowns.TryStart(ability, 0.5));
            Assert.IsFalse(cooldowns.IsReady(ability));

            yield return new WaitForSeconds(0.8f);

            Assert.IsTrue(cooldowns.IsReady(ability));
        }
    }
}
