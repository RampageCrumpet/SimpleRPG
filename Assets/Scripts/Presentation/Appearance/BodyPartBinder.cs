using System;
using UnityEngine;

namespace SimpleRPG.Appearance
{
    /// <summary>
    /// Builds the renderers for a <see cref="BodyPartDefinition"/> and attaches them to a rig. The only code that knows how a part meets a skeleton.
    /// </summary>
    public static class BodyPartBinder
    {
        /// <param name="part">The part to show.</param>
        /// <param name="rig">The bones of the character it goes on.</param>
        /// <param name="partsRoot">Where skinned pieces live. Rigid pieces go under their bone instead.</param>
        /// <returns>
        /// The part's root object, carrying a <see cref="BodyPartInstance"/>. A one-piece part is that one object, as it always was; a part of
        /// several pieces is an empty root under <paramref name="partsRoot"/> with each skinned piece beneath it.
        /// </returns>
        public static GameObject Create(BodyPartDefinition part, RigBoneIndex rig, Transform partsRoot)
        {
            string problem = part.Problem();
            if (problem != null) throw new InvalidOperationException(problem);

            GameObject root = new GameObject(part.name);
            var instance = root.AddComponent<BodyPartInstance>();
            if (part.Pieces.Count == 1)
            {
                BindPiece(root, part.Pieces[0], rig, partsRoot, instance);
                return root;
            }

            root.transform.SetParent(partsRoot, false);
            foreach (BodyPartPiece piece in part.Pieces)
            {
                BindPiece(new GameObject(part.name + "_" + piece.Mesh.name), piece, rig, root.transform, instance);
            }

            return root;
        }

        // Skinned pieces go under skinnedParent; a rigid piece goes under its bone, which is outside the part's root.
        private static void BindPiece(GameObject go, BodyPartPiece piece, RigBoneIndex rig, Transform skinnedParent, BodyPartInstance instance)
        {
            if (piece.IsSkinned)
            {
                go.transform.SetParent(skinnedParent, false);
                SkinnedMeshRenderer renderer = go.AddComponent<SkinnedMeshRenderer>();
                Transform[] bones = new Transform[piece.BoneNames.Length];
                for (int i = 0; i < bones.Length; i++) bones[i] = rig.Require(piece.BoneNames[i]);

                renderer.sharedMesh = piece.Mesh;
                renderer.sharedMaterials = piece.Materials;
                renderer.bones = bones;
                renderer.rootBone = rig.Require(piece.RootBoneName);
                renderer.localBounds = piece.LocalBounds;
                instance.Add(renderer, false);
                return;
            }

            go.transform.SetParent(rig.Require(piece.AttachBoneName), false);
            go.transform.SetLocalPositionAndRotation(piece.AttachPosition, piece.AttachRotation);
            go.transform.localScale = piece.AttachScale;
            go.AddComponent<MeshFilter>().sharedMesh = piece.Mesh;
            MeshRenderer meshRenderer = go.AddComponent<MeshRenderer>();
            meshRenderer.sharedMaterials = piece.Materials;

            // A one-piece rigid part is its own root, already parented to the bone and destroyed with it; only a piece of a bigger part is outside.
            instance.Add(meshRenderer, go != instance.gameObject);
        }
    }
}
