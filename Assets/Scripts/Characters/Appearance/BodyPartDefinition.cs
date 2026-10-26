using SimpleRPG.Abilities;
using UnityEngine;

namespace SimpleRPG.Appearance
{
    /// <summary>
    /// One swappable visual piece of a character: a mesh, its materials and how it attaches to the rig. A piece is either skinned to the
    /// shared humanoid skeleton (bone names are baked in, in the mesh's bind-pose order) or rigid, parented to a single bone.
    /// Immutable during gameplay. Like other gameplay definitions it has a permanent ID and a short network ID in the
    /// <see cref="DefinitionRegistry"/>, so the wire carries a number, never the asset.
    /// </summary>
    [CreateAssetMenu(menuName = "SimpleRPG/Appearance/Body Part")]
    public sealed class BodyPartDefinition : GameplayDefinition
    {
        [SerializeField] private BodyPartSlot slot;
        [SerializeField] private Mesh mesh;
        [SerializeField] private Material[] materials;

        [Header("Skinned parts")]
        [SerializeField, Tooltip("The rig bone for each of the mesh's bind poses, in order. Leave empty for a rigid part.")]
        private string[] boneNames;
        [SerializeField] private string rootBoneName;
        [SerializeField] private Bounds localBounds;

        [Header("Rigid parts")]
        [SerializeField, Tooltip("The bone a rigid part is parented to. Ignored when the part is skinned.")]
        private string attachBoneName;
        [SerializeField] private Vector3 attachPosition;
        [SerializeField] private Quaternion attachRotation = Quaternion.identity;
        [SerializeField] private Vector3 attachScale = Vector3.one;

        public BodyPartSlot Slot => slot;
        public Mesh Mesh => mesh;
        public Material[] Materials => materials;
        public string[] BoneNames => boneNames;
        public string RootBoneName => rootBoneName;
        public Bounds LocalBounds => localBounds;
        public string AttachBoneName => attachBoneName;
        public Vector3 AttachPosition => attachPosition;
        public Quaternion AttachRotation => attachRotation;
        public Vector3 AttachScale => attachScale;

        public bool IsSkinned => boneNames != null && boneNames.Length > 0;

        /// <summary>Why this part cannot be built, or null if it can. Used by tests and the definition check.</summary>
        public string Problem()
        {
            if (mesh == null) return name + " has no mesh.";
            if (materials == null || materials.Length == 0) return name + " has no materials.";
            if (IsSkinned)
            {
                if (boneNames.Length != mesh.bindposes.Length) return name + " lists " + boneNames.Length + " bones for " + mesh.bindposes.Length + " bind poses.";
                if (string.IsNullOrEmpty(rootBoneName)) return name + " has no root bone.";
                return null;
            }

            return string.IsNullOrEmpty(attachBoneName) ? name + " is neither skinned nor attached to a bone." : null;
        }

#if UNITY_EDITOR
        /// <summary>Fills the part from authored data. Editor tooling only; definitions are never written at runtime.</summary>
        public void Configure(BodyPartSlot slot, Mesh mesh, Material[] materials, string[] boneNames, string rootBoneName, Bounds localBounds,
            string attachBoneName, Vector3 attachPosition, Quaternion attachRotation, Vector3 attachScale)
        {
            this.slot = slot;
            this.mesh = mesh;
            this.materials = materials;
            this.boneNames = boneNames;
            this.rootBoneName = rootBoneName;
            this.localBounds = localBounds;
            this.attachBoneName = attachBoneName;
            this.attachPosition = attachPosition;
            this.attachRotation = attachRotation;
            this.attachScale = attachScale;
        }
#endif
    }
}
