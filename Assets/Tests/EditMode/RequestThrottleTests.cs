using NUnit.Framework;
using SimpleRPG.Networking;
using System;

namespace SimpleRPG.Tests.EditMode
{
    /// <summary>
    /// A client may send a burst, and keep up a steady rate, but cannot make the server work faster than that.
    /// </summary>
    public class RequestThrottleTests
    {
        [Test]
        public void ABurstUpToTheBucketSizeIsAllowed_ThenRefused()
        {
            var throttle = new RequestThrottle(perSecond: 10d, burst: 5d);

            for (int i = 0; i < 5; i++)
            {
                Assert.IsTrue(throttle.TryAllow(1, 0d), "Request " + i + " is inside the burst.");
            }

            Assert.IsFalse(throttle.TryAllow(1, 0d));
        }

        [Test]
        public void TokensComeBackAtTheSteadyRate()
        {
            var throttle = new RequestThrottle(perSecond: 10d, burst: 5d);
            for (int i = 0; i < 5; i++) throttle.TryAllow(1, 0d);

            // A tenth of a second earns one request back.
            Assert.IsTrue(throttle.TryAllow(1, 0.1d));
            Assert.IsFalse(throttle.TryAllow(1, 0.1d));
        }

        [Test]
        public void ASteadyRateAtTheLimitIsNeverRefused()
        {
            var throttle = new RequestThrottle(perSecond: 10d, burst: 1d);

            for (int i = 0; i < 100; i++)
            {
                Assert.IsTrue(throttle.TryAllow(1, i * 0.1d), "Request " + i);
            }
        }

        [Test]
        public void ARateTwiceTheLimit_IsHalfRefused()
        {
            var throttle = new RequestThrottle(perSecond: 10d, burst: 1d);
            int allowed = 0;

            for (int i = 0; i < 200; i++)
            {
                if (throttle.TryAllow(1, i * 0.05d)) allowed++;
            }

            Assert.AreEqual(100, allowed, 2);
        }

        [Test]
        public void ClientsDoNotShareABucket()
        {
            var throttle = new RequestThrottle(perSecond: 1d, burst: 1d);

            Assert.IsTrue(throttle.TryAllow(1, 0d));
            Assert.IsFalse(throttle.TryAllow(1, 0d));
            Assert.IsTrue(throttle.TryAllow(2, 0d), "Another client has its own allowance.");
        }

        [Test]
        public void TheBucketNeverHoldsMoreThanTheBurst()
        {
            var throttle = new RequestThrottle(perSecond: 10d, burst: 3d);

            // A long quiet spell does not save up more than a burst.
            int allowed = 0;
            for (int i = 0; i < 10; i++)
            {
                if (throttle.TryAllow(1, 1000d)) allowed++;
            }

            Assert.AreEqual(3, allowed);
        }

        [Test]
        public void AClockThatStepsBackwardsEarnsNothingAndCostsNothing()
        {
            var throttle = new RequestThrottle(perSecond: 10d, burst: 2d);
            throttle.TryAllow(1, 10d);
            throttle.TryAllow(1, 10d);

            Assert.IsFalse(throttle.TryAllow(1, 5d), "Going back in time does not refill it.");
            Assert.IsTrue(throttle.TryAllow(1, 10.1d), "And the real time still counts afterwards.");
        }

        [Test]
        public void ForgettingAClientGivesItAFreshBucket()
        {
            var throttle = new RequestThrottle(perSecond: 1d, burst: 1d);
            throttle.TryAllow(1, 0d);

            throttle.Forget(1);

            Assert.IsTrue(throttle.TryAllow(1, 0d));
        }

        [Test]
        public void ANonsenseRateOrBurstIsRefused()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new RequestThrottle(0d, 5d));
            Assert.Throws<ArgumentOutOfRangeException>(() => new RequestThrottle(-1d, 5d));
            Assert.Throws<ArgumentOutOfRangeException>(() => new RequestThrottle(5d, 0.5d));
        }
    }
}
