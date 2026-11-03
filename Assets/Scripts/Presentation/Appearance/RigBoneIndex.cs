using System;
using System.Collections.Generic;
using UnityEngine;

namespace SimpleRPG.Appearance
{
    /// <summary>
    /// Finds a rig's bones by name. Built once from the rig root, so attaching a part is a lookup, not a hierarchy search.
    /// </summary>
    public sealed class RigBoneIndex
    {
        private readonly Dictionary<string, Transform> byName = new Dictionary<string, Transform>();

        public RigBoneIndex(Transform rigRoot)
        {
            if (rigRoot == null) throw new ArgumentNullException(nameof(rigRoot));
            foreach (Transform t in rigRoot.GetComponentsInChildren<Transform>(true))
            {
                if (!byName.ContainsKey(t.name)) byName.Add(t.name, t);
            }
        }

        public bool TryGet(string boneName, out Transform bone) => byName.TryGetValue(boneName, out bone);

        /// <summary>The bone, or a descriptive error: a part naming a bone the rig lacks is a content mistake that must be loud.</summary>
        public Transform Require(string boneName)
        {
            if (!byName.TryGetValue(boneName, out Transform bone))
            {
                throw new InvalidOperationException("The rig has no bone named '" + boneName + "'.");
            }

            return bone;
        }
    }
}
