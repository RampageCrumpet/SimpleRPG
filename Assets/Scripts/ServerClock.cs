using Unity.Netcode;
using UnityEngine;

namespace SimpleRPG
{
    /// <summary>
    /// The shared timeline. While a network session is running this is the server's time, the clock the server stamps a swing with and every
    /// client joins it by, so a timer that gates or reproduces animation-driven combat reads the same time on every machine and in step with
    /// the animation. With no session (an offline scene, a unit test) it is Unity's time, so such timers still run without a NetworkManager.
    /// </summary>
    /// <remarks>
    /// Machine-local presentation, such as the interaction hold or a camera blend, does not need this and may use Unity's time.
    /// Do not slow or pause the game with <c>Time.timeScale</c>: it is local to a machine and would take it off this timeline. A hit-stop has to be
    /// something the server decides and every machine applies.
    /// </remarks>
    public static class ServerClock
    {
        /// <summary>
        /// The current time in seconds on the shared timeline.
        /// </summary>
        public static double Now
        {
            get
            {
                NetworkManager manager = NetworkManager.Singleton;
                if (manager != null && manager.IsListening)
                {
                    return manager.ServerTime.Time;
                }

                return Time.timeAsDouble;
            }
        }
    }
}
