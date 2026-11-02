using System;
using System.Collections.Generic;

namespace SimpleRPG.Networking
{
    /// <summary>
    /// Limits how often one client may ask the server for something, so a modified client cannot make the server do work as fast as it can send
    /// messages. A token bucket per client: requests spend tokens, tokens come back at a steady rate, and a burst up to the bucket's size is
    /// allowed so a genuine flurry (dragging several items in a second) is never refused. Plain code with the time passed in, so it is exact to test.
    /// </summary>
    public sealed class RequestThrottle
    {
        private struct Bucket
        {
            public double Tokens;
            public double LastSeen;
        }

        private readonly Dictionary<ulong, Bucket> buckets = new Dictionary<ulong, Bucket>();
        private readonly double perSecond;
        private readonly double burst;

        /// <param name="perSecond">How many requests per second a client may keep up.</param>
        /// <param name="burst">How many it may send at once after being quiet.</param>
        public RequestThrottle(double perSecond, double burst)
        {
            if (perSecond <= 0d)
            {
                throw new ArgumentOutOfRangeException(nameof(perSecond));
            }

            if (burst < 1d)
            {
                throw new ArgumentOutOfRangeException(nameof(burst), "A burst below one would refuse every request.");
            }

            this.perSecond = perSecond;
            this.burst = burst;
        }

        /// <summary>Spends a token for a request from <paramref name="client"/> at time <paramref name="now"/> (seconds). False means refuse it.</summary>
        public bool TryAllow(ulong client, double now)
        {
            if (!buckets.TryGetValue(client, out Bucket bucket))
            {
                bucket = new Bucket { Tokens = burst, LastSeen = now };
            }

            // A clock that steps backwards earns nothing; it never costs tokens either.
            double elapsed = Math.Max(0d, now - bucket.LastSeen);
            bucket.Tokens = Math.Min(burst, bucket.Tokens + elapsed * perSecond);
            bucket.LastSeen = Math.Max(now, bucket.LastSeen);

            bool allowed = bucket.Tokens >= 1d;
            if (allowed)
            {
                bucket.Tokens -= 1d;
            }

            buckets[client] = bucket;
            return allowed;
        }

        /// <summary>Drops what is remembered about a client, for when it disconnects.</summary>
        public void Forget(ulong client) => buckets.Remove(client);
    }
}
