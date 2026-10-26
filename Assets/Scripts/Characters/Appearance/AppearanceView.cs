using System.Collections.Generic;
using UnityEngine;

namespace SimpleRPG.Appearance
{
    /// <summary>
    /// What a character looks like right now: the part shown in each slot. Pure presentation with no network or gameplay knowledge, so it can be
    /// driven by replicated state, by a preview, or by a test.
    /// </summary>
    public sealed class AppearanceView
    {
        private readonly RigBoneIndex rig;
        private readonly Transform partsRoot;
        private readonly Dictionary<BodyPartSlot, GameObject> shown = new Dictionary<BodyPartSlot, GameObject>();

        public AppearanceView(RigBoneIndex rig, Transform partsRoot)
        {
            this.rig = rig;
            this.partsRoot = partsRoot;
        }

        public bool IsShowing(BodyPartSlot slot) => shown.ContainsKey(slot);

        /// <summary>Shows the part in its slot, replacing whatever was there.</summary>
        public void Show(BodyPartDefinition part)
        {
            Clear(part.Slot);
            shown[part.Slot] = BodyPartBinder.Create(part, rig, partsRoot);
        }

        public void Clear(BodyPartSlot slot)
        {
            if (!shown.TryGetValue(slot, out GameObject current)) return;

            shown.Remove(slot);
            if (current == null) return;
            if (Application.isPlaying) Object.Destroy(current);
            else Object.DestroyImmediate(current);
        }

        public void ClearAll()
        {
            foreach (BodyPartSlot slot in new List<BodyPartSlot>(shown.Keys)) Clear(slot);
        }
    }
}
