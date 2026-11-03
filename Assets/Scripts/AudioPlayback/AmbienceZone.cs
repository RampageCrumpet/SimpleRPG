using System.Collections.Generic;
using UnityEngine;

namespace SimpleRPG.Audio
{
    /// <summary>
    /// Marks an area of the dungeon with an ambience profile. Put one on a room prefab with a trigger collider covering the room; the
    /// <see cref="AmbienceController"/> uses the profile of the zone the listener is in.
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public sealed class AmbienceZone : MonoBehaviour
    {
        private static readonly List<AmbienceZone> Zones = new List<AmbienceZone>();

        [SerializeField] private AmbienceProfile profile;

        private Collider area;

        public AmbienceProfile Profile
        {
            get => profile;
            set => profile = value;
        }

        private void Awake() => area = GetComponent<Collider>();

        private void OnEnable() => Zones.Add(this);

        private void OnDisable() => Zones.Remove(this);

        /// <summary>The zone containing a point, or null.</summary>
        public static AmbienceZone At(Vector3 point)
        {
            foreach (AmbienceZone zone in Zones)
            {
                if (zone.area != null && zone.area.bounds.Contains(point))
                {
                    return zone;
                }
            }

            return null;
        }
    }
}
