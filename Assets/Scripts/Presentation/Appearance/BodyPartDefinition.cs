using SimpleRPG.Abilities;
using SimpleRPG.InventorySystem;
using System.Collections.Generic;
using UnityEngine;

namespace SimpleRPG.Appearance
{
    /// <summary>
    /// One swappable visual part of a character, shown in one slot. A part is one or more <see cref="BodyPartPiece"/>s (<see cref="Pieces"/>):
    /// the primary piece is the flat mesh/material/bone fields below, and <see cref="AdditionalPieces"/> are any further pieces, each with its own
    /// mesh, materials, transform and skinned-or-rigid binding. A one-piece part, which is every part of the bare body, needs nothing more.
    /// Immutable during gameplay. Like other gameplay definitions it has a permanent ID and a short network ID in the
    /// <see cref="DefinitionRegistry"/>, so the wire carries a number, never the asset.
    /// </summary>
    [CreateAssetMenu(menuName = "SimpleRPG/Appearance/Body Part")]
    public sealed class BodyPartDefinition : GameplayDefinition
    {
        [SerializeField] private BodyPartSlot slot;
        [SerializeField] private Mesh mesh;
        [SerializeField] private Material[] materials;

        [Header("Armor")]
        [SerializeField, Tooltip("The item this part is the look of: worn armor shows its parts. Empty for a part of the bare body.")]
        private Item dressesItem;

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

        [Header("More pieces")]
        [SerializeField, Tooltip("Further pieces shown with the primary piece above, e.g. a mail undercoat under plate. Each binds on its own.")]
        private BodyPartPiece[] additionalPieces;

        private BodyPartPiece[] pieces;

        public BodyPartSlot Slot => slot;

        /// <summary>The primary piece followed by <see cref="AdditionalPieces"/>: the one view of everything this part shows.</summary>
        public IReadOnlyList<BodyPartPiece> Pieces
        {
            get
            {
                if (pieces != null) return pieces;
                int extra = additionalPieces != null ? additionalPieces.Length : 0;
                var all = new BodyPartPiece[1 + extra];
                all[0] = new BodyPartPiece(mesh, materials, boneNames, rootBoneName, localBounds, attachBoneName, attachPosition, attachRotation, attachScale);
                for (int i = 0; i < extra; i++) all[1 + i] = additionalPieces[i];
                pieces = all;
                return pieces;
            }
        }

        public IReadOnlyList<BodyPartPiece> AdditionalPieces => additionalPieces ?? System.Array.Empty<BodyPartPiece>();

        /// <summary>The armor this part is the look of, or null for a part of the bare body.</summary>
        public Item DressesItem => dressesItem;
        public Mesh Mesh => mesh;
        public Material[] Materials => materials;
        public string[] BoneNames => boneNames;
        public string RootBoneName => rootBoneName;
        public Bounds LocalBounds => localBounds;
        public string AttachBoneName => attachBoneName;
        public Vector3 AttachPosition => attachPosition;
        public Quaternion AttachRotation => attachRotation;
        public Vector3 AttachScale => attachScale;

        private void OnValidate() => pieces = null;

        public bool IsSkinned => boneNames != null && boneNames.Length > 0;

        /// <summary>Why this part cannot be built, or null if it can. Used by tests and the definition check.</summary>
        public string Problem()
        {
            foreach (BodyPartPiece piece in Pieces)
            {
                string problem = piece == null ? name + " has an empty piece." : piece.Problem(name);
                if (problem != null) return problem;
            }

            return null;
        }

#if UNITY_EDITOR
        /// <summary>Makes this part the look of an item. Editor tooling only.</summary>
        public void Dress(Item item) => dressesItem = item;

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
            pieces = null;
        }

        /// <summary>Sets the pieces shown with the primary one. Editor tooling only.</summary>
        public void SetAdditionalPieces(BodyPartPiece[] more)
        {
            additionalPieces = more;
            pieces = null;
        }
#endif
    }
}
