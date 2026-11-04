using System;
using UnityEngine;

namespace SimpleRPG.Appearance
{
    /// <summary>
    /// One renderable piece of a <see cref="BodyPartDefinition"/>: a mesh, its materials, and how it meets the rig. A piece is either skinned to
    /// the shared humanoid skeleton (bone names baked in, in the mesh's bind-pose order) or rigid, parented to one bone with its own local pose.
    /// A part has one or more pieces; each piece chooses skinned or rigid for itself, so a plate cuirass (skinned) and a shoulder buckle (rigid)
    /// can be one part. Immutable during gameplay.
    /// </summary>
    [Serializable]
    public sealed class BodyPartPiece
    {
        [SerializeField] private Mesh mesh;
        [SerializeField] private Material[] materials;

        [SerializeField, Tooltip("The rig bone for each of the mesh's bind poses, in order. Leave empty for a rigid piece.")]
        private string[] boneNames;
        [SerializeField] private string rootBoneName;
        [SerializeField] private Bounds localBounds;

        [SerializeField, Tooltip("The bone a rigid piece is parented to. Ignored when the piece is skinned.")]
        private string attachBoneName;
        [SerializeField] private Vector3 attachPosition;
        [SerializeField] private Quaternion attachRotation = Quaternion.identity;
        [SerializeField] private Vector3 attachScale = Vector3.one;

        public BodyPartPiece()
        {
        }

        public BodyPartPiece(Mesh mesh, Material[] materials, string[] boneNames, string rootBoneName, Bounds localBounds,
            string attachBoneName, Vector3 attachPosition, Quaternion attachRotation, Vector3 attachScale)
        {
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

        /// <summary>Why this piece cannot be built, or null if it can. <paramref name="owner"/> names the part in the message.</summary>
        public string Problem(string owner)
        {
            if (mesh == null) return owner + " has a piece with no mesh.";
            if (materials == null || materials.Length == 0) return owner + " has a piece with no materials (" + mesh.name + ").";
            if (IsSkinned)
            {
                if (boneNames.Length != mesh.bindposes.Length) return owner + " lists " + boneNames.Length + " bones for " + mesh.bindposes.Length + " bind poses (" + mesh.name + ").";
                return string.IsNullOrEmpty(rootBoneName) ? owner + " has a skinned piece with no root bone (" + mesh.name + ")." : null;
            }

            return string.IsNullOrEmpty(attachBoneName) ? owner + " has a piece that is neither skinned nor attached to a bone (" + mesh.name + ")." : null;
        }
    }
}
