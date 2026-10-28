using NUnit.Framework;
using UnityEngine;

namespace SimpleRPG.Tests.EditMode
{
    public class ServerClockEditModeTests
    {
        [Test]
        public void WithNoNetworkManager_TheClockIsUnityTime_SoOfflineCodeAndTestsStillRun()
        {
            Assert.IsNull(Unity.Netcode.NetworkManager.Singleton, "this test needs no session");

            Assert.AreEqual(Time.timeAsDouble, ServerClock.Now, 0.5);
        }
    }
}
