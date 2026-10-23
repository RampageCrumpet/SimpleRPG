using System.Collections.Generic;
using UnityEngine;

namespace SimpleRPG.Rendering
{
    /// <summary>
    /// The renderers the local player's outline pass should highlight this frame. Only the local view uses it, so nothing here is networked.
    /// </summary>
    public static class InteractableOutline
    {
        private static readonly List<Renderer> targets = new List<Renderer>();

        public static IReadOnlyList<Renderer> Targets => targets;

        public static bool HasTargets => targets.Count > 0;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => targets.Clear();

        /// <summary>
        /// Outlines these renderers instead of whatever was outlined before. Null or empty clears the outline.
        /// </summary>
        public static void SetTargets(IReadOnlyList<Renderer> renderers)
        {
            targets.Clear();
            if (renderers != null)
            {
                targets.AddRange(renderers);
            }
        }
    }
}
