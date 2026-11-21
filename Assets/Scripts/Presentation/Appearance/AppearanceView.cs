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
        private readonly Dictionary<BodyPartSlot, BodyPartDefinition> parts = new Dictionary<BodyPartSlot, BodyPartDefinition>();

        /// <summary>Whether worn cloth is simulated. Off for a dedicated server and for still previews; set before parts are shown.</summary>
        public bool SimulateCloth { get; set; } = true;

        /// <summary>The parts currently shown, so another view (a UI preview) can show the same ones.</summary>
        public IEnumerable<BodyPartDefinition> Parts => parts.Values;

        public AppearanceView(RigBoneIndex rig, Transform partsRoot)
        {
            this.rig = rig;
            this.partsRoot = partsRoot;
        }

        public bool IsShowing(BodyPartSlot slot) => shown.ContainsKey(slot);

        /// <summary>The object showing the slot's part, or null if the slot is empty.</summary>
        public GameObject ShownIn(BodyPartSlot slot) => shown.TryGetValue(slot, out GameObject current) ? current : null;

        /// <summary>Shows the part in its slot, replacing whatever was there.</summary>
        public void Show(BodyPartDefinition part)
        {
            Clear(part.Slot);
            shown[part.Slot] = BodyPartBinder.Create(part, rig, partsRoot, SimulateCloth);
            parts[part.Slot] = part;
            RefreshCordVisibility();
        }

        public void Clear(BodyPartSlot slot)
        {
            if (!shown.TryGetValue(slot, out GameObject current)) return;

            shown.Remove(slot);
            parts.Remove(slot);
            if (current == null) return;
            if (current.TryGetComponent(out BodyPartInstance instance))
            {
                instance.Remove();
            }
            else if (Application.isPlaying) Object.Destroy(current);
            else Object.DestroyImmediate(current);
            RefreshCordVisibility();
        }

        private void RefreshCordVisibility()
        {
            bool torsoArmor = parts.TryGetValue(BodyPartSlot.Torso, out var torso) && torso.DressesItem != null;
            foreach (var root in shown.Values)
                if (root != null)
                    foreach (var cord in root.GetComponentsInChildren<CordBoneMotion>(true)) cord.SetTorsoArmorVisible(torsoArmor);
        }

        public void ClearAll()
        {
            foreach (BodyPartSlot slot in new List<BodyPartSlot>(shown.Keys)) Clear(slot);
        }
    }
}
