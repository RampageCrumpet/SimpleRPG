using System;
using Unity.Netcode;
using UnityEngine;

namespace SimpleRPG.Appearance
{
    /// <summary>
    /// Which part a character wears in each slot, kept by the server and replicated to everyone, late joiners included. Only the server changes
    /// it (<see cref="ServerEquip"/>, <see cref="ServerClear"/>); the spawn starts from <see cref="defaultParts"/>. Every machine shows the
    /// replicated parts on the same rig, which is animated the same way everywhere, so what the server hits is what clients see.
    /// </summary>
    public class CharacterAppearance : NetworkBehaviour
    {
        /// <summary>One worn part as replicated: a slot and the short id of the part in it.</summary>
        public struct PartEntry : INetworkSerializable, IEquatable<PartEntry>
        {
            public int Slot;
            public uint PartId;

            public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
            {
                serializer.SerializeValue(ref Slot);
                serializer.SerializeValue(ref PartId);
            }

            public bool Equals(PartEntry other) => Slot == other.Slot && PartId == other.PartId;

            public override bool Equals(object obj) => obj is PartEntry other && Equals(other);

            public override int GetHashCode() => HashCode.Combine(Slot, PartId);
        }

        [SerializeField, Tooltip("The skeleton root the parts bind to. Humanoid rig with the animated bones.")]
        private Transform rigRoot;

        [SerializeField, Tooltip("Where skinned parts are created. Outside the rig, so bone lookups never see them.")]
        private Transform partsRoot;

        [SerializeField, Tooltip("What a new character wears. At most one part per slot.")]
        private BodyPartDefinition[] defaultParts = Array.Empty<BodyPartDefinition>();

        private readonly NetworkList<PartEntry> entries = new NetworkList<PartEntry>();

        private IBodyPartCatalog catalog = new RegistryBodyPartCatalog();
        private AppearanceView view;

        /// <summary>Raised on every machine after the shown parts change.</summary>
        public event Action Changed;

        public BodyPartDefinition[] DefaultParts => defaultParts;

        /// <summary>The parts worn right now on this machine.</summary>
        public System.Collections.Generic.IEnumerable<BodyPartDefinition> CurrentParts => View.Parts;

        /// <summary>The skeleton root the parts bind to.</summary>
        public Transform RigRoot => rigRoot;

        /// <summary>Replaces how part ids are resolved. For tests.</summary>
        public void UseCatalog(IBodyPartCatalog replacement) => catalog = replacement;

        private AppearanceView View => view ??= new AppearanceView(new RigBoneIndex(rigRoot), partsRoot);

        public override void OnNetworkSpawn()
        {
            if (IsServer)
            {
                foreach (BodyPartDefinition part in defaultParts) ServerEquip(part);
                return;
            }

            entries.OnListChanged += HandleEntriesChanged;
            RebuildFromEntries();
        }

        public override void OnNetworkDespawn()
        {
            entries.OnListChanged -= HandleEntriesChanged;
            view?.ClearAll();
        }

        /// <summary>The object showing the part worn in the slot on this machine, or null if the slot is empty.</summary>
        public GameObject ShownIn(BodyPartSlot slot) => View.ShownIn(slot);

        /// <summary>Server only: wears the part in its slot, replacing the part there.</summary>
        public void ServerEquip(BodyPartDefinition part)
        {
            if (!IsServer)
            {
                Debug.LogError("Only the server changes a character's appearance.");
                return;
            }

            if (!catalog.TryGetId(part, out uint id))
            {
                Debug.LogError("Body part '" + (part != null ? part.name : "null") + "' is not in the definition registry. Run SimpleRPG > Definitions > Rebuild Definition Registry.");
                return;
            }

            PartEntry entry = new PartEntry { Slot = (int)part.Slot, PartId = id };
            int index = IndexOf(part.Slot);
            if (index >= 0) entries[index] = entry;
            else entries.Add(entry);

            View.Show(part);
            Changed?.Invoke();
        }

        /// <summary>Server only: takes off whatever is in the slot.</summary>
        public void ServerClear(BodyPartSlot slot)
        {
            if (!IsServer)
            {
                Debug.LogError("Only the server changes a character's appearance.");
                return;
            }

            int index = IndexOf(slot);
            if (index < 0) return;

            entries.RemoveAt(index);
            View.Clear(slot);
            Changed?.Invoke();
        }

        private int IndexOf(BodyPartSlot slot)
        {
            for (int i = 0; i < entries.Count; i++)
            {
                if (entries[i].Slot == (int)slot) return i;
            }

            return -1;
        }

        private void HandleEntriesChanged(NetworkListEvent<PartEntry> change) => RebuildFromEntries();

        // The list is small and changes rarely, so a client redraws it whole rather than tracking each event kind.
        private void RebuildFromEntries()
        {
            View.ClearAll();
            for (int i = 0; i < entries.Count; i++)
            {
                if (catalog.TryGetPart(entries[i].PartId, out BodyPartDefinition part)) View.Show(part);
                else Debug.LogError("Unknown body part id " + entries[i].PartId + " in slot " + (BodyPartSlot)entries[i].Slot + ".");
            }

            Changed?.Invoke();
        }

#if UNITY_EDITOR
        /// <summary>Shows the default parts in the editor, outside play mode. Never saved with the scene or prefab.</summary>
        [ContextMenu("Preview Default Parts")]
        private void PreviewDefaultParts()
        {
            ClearPreview();
            foreach (BodyPartDefinition part in defaultParts)
            {
                if (part == null) continue;
                GameObject go = BodyPartBinder.Create(part, new RigBoneIndex(rigRoot), partsRoot);
                go.hideFlags = HideFlags.DontSave;
            }
        }

        [ContextMenu("Clear Preview")]
        private void ClearPreview()
        {
            for (int i = partsRoot.childCount - 1; i >= 0; i--) DestroyImmediate(partsRoot.GetChild(i).gameObject);
        }
#endif
    }
}
