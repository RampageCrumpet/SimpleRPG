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

        [Test]
        public void ALateObserver_JoinsTheStartingAnimationByHowLongTheLifeHasRun()
        {
            Assert.AreEqual(2.0f, SimpleRPG.Animations.AnimatableCharacter.SecondsIntoStart(10.0, 12.0), 1e-4f, "a life begun 2s ago is 2s into its rise");
            Assert.AreEqual(0f, SimpleRPG.Animations.AnimatableCharacter.SecondsIntoStart(10.0, 9.9), "a clock skew never seeks backwards");
        }
    }
}
