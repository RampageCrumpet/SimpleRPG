using Unity.Netcode;
using UnityEngine;
using UnityEngine.Rendering;

namespace SimpleRPG.Appearance
{
    /// <summary>
    /// Keeps the owning player from seeing the inside of their own head. The first-person camera sits at the eyes, so the parts worn on the head
    /// would fill the view. On the owner's machine those parts stop drawing but keep casting shadows; their meshes, skinning, the skeleton and the
    /// replicated appearance are untouched, so everyone else still sees the full head. Other machines never run this.
    /// </summary>
    [RequireComponent(typeof(CharacterAppearance))]
    public sealed class OwnerViewBodyHider : NetworkBehaviour
    {
        [SerializeField, Tooltip("The slots the owner does not see from inside.")]
        private BodyPartSlot[] hiddenSlots = { BodyPartSlot.Head, BodyPartSlot.Eyebrows, BodyPartSlot.Headgear };

        private CharacterAppearance appearance;

        public override void OnNetworkSpawn()
        {
            if (!IsOwner)
            {
                return;
            }

            appearance = GetComponent<CharacterAppearance>();
            appearance.Changed += Apply;
            Apply();
        }

        public override void OnNetworkDespawn()
        {
            if (appearance != null)
            {
                appearance.Changed -= Apply;
            }
        }

        private void Apply()
        {
            foreach (BodyPartSlot slot in hiddenSlots)
            {
                GameObject shown = appearance.ShownIn(slot);
                if (shown == null || !shown.TryGetComponent(out BodyPartInstance instance))
                {
                    continue;
                }

                // Every piece of the part, including rigid ones parented to bones outside the part's own object.
                foreach (Renderer renderer in instance.Renderers)
                {
                    renderer.shadowCastingMode = ShadowCastingMode.ShadowsOnly;
                }
            }
        }
    }
}
