using System.Collections.Generic;

namespace SimpleRPG.Abilities
{
    /// <summary>
    /// Server-side record of when each ability was last accepted for a character.
    /// The owning client also tracks cooldowns in <see cref="AbilityInstance"/>, but that is only a UI/prediction aid;
    /// the server uses this tracker so a modified client cannot fire faster than the ability's cooldown allows.
    /// </summary>
    public class ServerCooldownTracker
    {
        /// <summary>
        /// Seconds of slack allowed below the full cooldown so network jitter between the client's clock and the server's
        /// doesn't reject an attack that was legitimately ready on the client.
        /// </summary>
        public const float DefaultTolerance = 0.1f;

        private readonly Dictionary<string, float> lastAcceptedTimes = new Dictionary<string, float>();

        private readonly float tolerance;

        public ServerCooldownTracker(float tolerance = DefaultTolerance)
        {
            this.tolerance = tolerance;
        }

        /// <summary>
        /// Records an activation if the ability's cooldown has elapsed.
        /// </summary>
        /// <param name="abilityName">The name of the ability being activated.</param>
        /// <param name="cooldownTime">The ability's cooldown in seconds.</param>
        /// <param name="now">The current server time in seconds.</param>
        /// <returns>True if the activation is accepted, false if the ability is still on cooldown.</returns>
        public bool TryActivate(string abilityName, float cooldownTime, float now)
        {
            if (lastAcceptedTimes.TryGetValue(abilityName, out float last) && now < last + cooldownTime - tolerance)
            {
                return false;
            }

            lastAcceptedTimes[abilityName] = now;
            return true;
        }
    }
}
