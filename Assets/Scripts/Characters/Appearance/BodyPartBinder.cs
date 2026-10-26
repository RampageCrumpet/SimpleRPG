using System;
using UnityEngine;

namespace SimpleRPG.Appearance
{
    /// <summary>
    /// Builds the renderer for a <see cref="BodyPartDefinition"/> and attaches it to a rig. The only code that knows how a part meets a skeleton.
    /// </summary>
    public static class BodyPartBinder
    {
        /// <param name="part">The part to show.</param>
        /// <param name="rig">The bones of the character it goes on.</param>
        /// <param name="partsRoot">Where skinned parts live. Rigid parts go under their bone instead.</param>
        public static GameObject Create(BodyPartDefinition part, RigBoneIndex rig, Transform partsRoot)
        {
            string problem = part.Problem();
            if (problem != null) throw new InvalidOperationException(problem);

            GameObject go = new GameObject(part.name);
            if (part.IsSkinned) BindSkinned(go, part, rig, partsRoot);
            else BindRigid(go, part, rig);
            return go;
        }

        private static void BindSkinned(GameObject go, BodyPartDefinition part, RigBoneIndex rig, Transform partsRoot)
        {
            go.transform.SetParent(partsRoot, false);
            SkinnedMeshRenderer renderer = go.AddComponent<SkinnedMeshRenderer>();
            Transform[] bones = new Transform[part.BoneNames.Length];
            for (int i = 0; i < bones.Length; i++) bones[i] = rig.Require(part.BoneNames[i]);

            renderer.sharedMesh = part.Mesh;
            renderer.sharedMaterials = part.Materials;
            renderer.bones = bones;
            renderer.rootBone = rig.Require(part.RootBoneName);
            renderer.localBounds = part.LocalBounds;
        }

        private static void BindRigid(GameObject go, BodyPartDefinition part, RigBoneIndex rig)
        {
            Transform attach = rig.Require(part.AttachBoneName);
            go.transform.SetParent(attach, false);
            go.transform.SetLocalPositionAndRotation(part.AttachPosition, part.AttachRotation);
            go.transform.localScale = part.AttachScale;
            go.AddComponent<MeshFilter>().sharedMesh = part.Mesh;
            go.AddComponent<MeshRenderer>().sharedMaterials = part.Materials;
        }
    }
}
