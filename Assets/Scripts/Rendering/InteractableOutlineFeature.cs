using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

namespace SimpleRPG.Rendering
{
    /// <summary>
    /// Draws a screen-space outline around the renderers in <see cref="InteractableOutline"/>: their silhouette goes into a mask and a
    /// composite pass paints a ring of pixels just outside it. Costs nothing on frames with no outline target.
    /// </summary>
    public class InteractableOutlineFeature : ScriptableRendererFeature
    {
        [SerializeField] private Color color = new Color(1f, 0.85f, 0.2f, 1f);
        [SerializeField, Range(1, 6)] private int widthInPixels = 2;
        [SerializeField] private Shader maskShader;
        [SerializeField] private Shader compositeShader;

        private Material maskMaterial;
        private Material compositeMaterial;
        private OutlinePass pass;

        public override void Create()
        {
            if (maskShader == null || compositeShader == null)
            {
                return;
            }

            maskMaterial = CoreUtils.CreateEngineMaterial(maskShader);
            compositeMaterial = CoreUtils.CreateEngineMaterial(compositeShader);
            pass = new OutlinePass(maskMaterial, compositeMaterial) { renderPassEvent = RenderPassEvent.AfterRenderingTransparents };
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            if (pass == null || !InteractableOutline.HasTargets || renderingData.cameraData.cameraType != CameraType.Game)
            {
                return;
            }

            pass.Configure(color, widthInPixels);
            renderer.EnqueuePass(pass);
        }

        protected override void Dispose(bool disposing)
        {
            CoreUtils.Destroy(maskMaterial);
            CoreUtils.Destroy(compositeMaterial);
        }

        private class OutlinePass : ScriptableRenderPass
        {
            private static readonly int OutlineColorId = Shader.PropertyToID("_OutlineColor");
            private static readonly int OutlineWidthId = Shader.PropertyToID("_OutlineWidth");
            private static readonly int BlitTextureId = Shader.PropertyToID("_BlitTexture");
            private static readonly int BlitScaleBiasId = Shader.PropertyToID("_BlitScaleBias");

            private readonly Material maskMaterial;
            private readonly Material compositeMaterial;
            private readonly MaterialPropertyBlock properties = new MaterialPropertyBlock();
            private readonly List<Renderer> renderers = new List<Renderer>();
            private Color color;
            private int width;

            public OutlinePass(Material maskMaterial, Material compositeMaterial)
            {
                this.maskMaterial = maskMaterial;
                this.compositeMaterial = compositeMaterial;
                profilingSampler = new ProfilingSampler("Interactable Outline");
            }

            public void Configure(Color color, int width)
            {
                this.color = color;
                this.width = width;
            }

            private class MaskPassData
            {
                public List<Renderer> renderers;
                public Material material;
            }

            private class CompositePassData
            {
                public TextureHandle mask;
                public Material material;
                public MaterialPropertyBlock properties;
                public Color color;
                public int width;
            }

            public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
            {
                var resources = frameData.Get<UniversalResourceData>();
                var cameraData = frameData.Get<UniversalCameraData>();

                renderers.Clear();
                foreach (Renderer target in InteractableOutline.Targets)
                {
                    if (target != null && target.enabled && target.gameObject.activeInHierarchy)
                    {
                        renderers.Add(target);
                    }
                }

                if (renderers.Count == 0)
                {
                    return;
                }

                RenderTextureDescriptor maskDescriptor = cameraData.cameraTargetDescriptor;
                maskDescriptor.msaaSamples = 1;
                maskDescriptor.depthBufferBits = 0;
                maskDescriptor.graphicsFormat = GraphicsFormat.R8_UNorm;
                TextureHandle mask = UniversalRenderer.CreateRenderGraphTexture(renderGraph, maskDescriptor, "_InteractableOutlineMask", true);

                using (var builder = renderGraph.AddRasterRenderPass<MaskPassData>("Interactable Outline Mask", out var data, profilingSampler))
                {
                    data.renderers = renderers;
                    data.material = maskMaterial;
                    builder.SetRenderAttachment(mask, 0, AccessFlags.Write);
                    builder.AllowPassCulling(false);
                    builder.SetRenderFunc(static (MaskPassData d, RasterGraphContext context) =>
                    {
                        foreach (Renderer renderer in d.renderers)
                        {
                            if (renderer == null) continue;
                            for (int submesh = 0; submesh < renderer.sharedMaterials.Length; submesh++)
                            {
                                context.cmd.DrawRenderer(renderer, d.material, submesh, 0);
                            }
                        }
                    });
                }

                using (var builder = renderGraph.AddRasterRenderPass<CompositePassData>("Interactable Outline Composite", out var data, profilingSampler))
                {
                    data.mask = mask;
                    data.material = compositeMaterial;
                    data.properties = properties;
                    data.color = color;
                    data.width = width;
                    builder.UseTexture(mask, AccessFlags.Read);
                    builder.SetRenderAttachment(resources.activeColorTexture, 0, AccessFlags.ReadWrite);
                    builder.AllowPassCulling(false);
                    builder.SetRenderFunc(static (CompositePassData d, RasterGraphContext context) =>
                    {
                        d.properties.Clear();
                        d.properties.SetTexture(BlitTextureId, d.mask);
                        d.properties.SetVector(BlitScaleBiasId, new Vector4(1f, 1f, 0f, 0f));
                        d.properties.SetColor(OutlineColorId, d.color);
                        d.properties.SetFloat(OutlineWidthId, d.width);
                        context.cmd.DrawProcedural(Matrix4x4.identity, d.material, 0, MeshTopology.Triangles, 3, 1, d.properties);
                    });
                }
            }
        }
    }
}
