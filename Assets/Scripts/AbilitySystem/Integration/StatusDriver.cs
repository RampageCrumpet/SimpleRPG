using UnityEngine;

namespace SimpleRPG.Abilities.Integration
{
    /// <summary>
    /// Advances the server's statuses every frame, so timers run and periodic effects happen without any character needing a status component.
    /// It is created with the ability services and destroyed with them.
    /// </summary>
    public sealed class StatusDriver : MonoBehaviour
    {
        private StatusManager statuses;

        /// <summary>
        /// Creates a driver for a status manager.
        /// </summary>
        public static StatusDriver Create(StatusManager statuses)
        {
            GameObject host = new GameObject("AbilityStatusDriver") { hideFlags = HideFlags.HideAndDontSave };
            StatusDriver driver = host.AddComponent<StatusDriver>();
            driver.statuses = statuses;
            return driver;
        }

        private void Update()
        {
            statuses?.Process();
        }

        /// <summary>
        /// Stops the driver and removes it.
        /// </summary>
        public void Remove()
        {
            statuses = null;
            if (Application.isPlaying)
            {
                Destroy(gameObject);
            }
            else
            {
                DestroyImmediate(gameObject);
            }
        }
    }
}
