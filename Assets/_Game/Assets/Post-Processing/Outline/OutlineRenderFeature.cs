using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

public sealed class LayerDepthOutlineRendererFeature : ScriptableRendererFeature
{
    [Header("Filtering")]
    [SerializeField] private LayerMask m_OutlineLayerMask;

    [Header("Materials")]
    [SerializeField] private Material m_SelectedLayerDepthMaskMaterial;
    [SerializeField] private Material m_PackSceneDataMaterial;
    [SerializeField] private Material m_GenerateOutlineMaterial;
    [SerializeField] private Material m_CompositeOutlineMaterial;

    [Header("Pass Timing")]
    [SerializeField]
    private RenderPassEvent m_RenderPassEvent =
        RenderPassEvent.BeforeRenderingPostProcessing;

    private LayerDepthOutlinePass m_Pass;

    private bool IsConfigured =>
        m_OutlineLayerMask.value != 0 &&
        m_SelectedLayerDepthMaskMaterial != null &&
        m_PackSceneDataMaterial != null &&
        m_GenerateOutlineMaterial != null &&
        m_CompositeOutlineMaterial != null;

    public override void Create()
    {
        m_Pass = new LayerDepthOutlinePass(this);
    }

    public override void AddRenderPasses(
        ScriptableRenderer renderer,
        ref RenderingData renderingData)
    {
        if (m_Pass == null || !IsConfigured)
            return;

        CameraType cameraType = renderingData.cameraData.cameraType;

        if (cameraType == CameraType.Preview ||
            cameraType == CameraType.Reflection ||
            !TryGetActiveVolume(out _))
        {
            return;
        }

        m_Pass.renderPassEvent = m_RenderPassEvent;
        renderer.EnqueuePass(m_Pass);
    }

    private static bool TryGetActiveVolume(
        out LayerDepthOutlineVolumeComponent volume)
    {
        volume = VolumeManager.instance.stack?
            .GetComponent<LayerDepthOutlineVolumeComponent>();

        return volume != null && volume.IsActive();
    }

    private sealed class LayerDepthOutlinePass : ScriptableRenderPass
    {
        private const string BaseSceneDataName = "_BaseOutlineSceneData";
        private const string DisplacedLayerDataName = "_DisplacedOutlineLayerTexture";
        private const string DisplacedLayerDepthName = "_DisplacedOutlineLayerDepth";
        private const string FinalSceneDataName = "_FinalOutlineSceneData";
        private const string EdgeSeedName = "_LayerDepthEdgeSeeds";
        private const string OutlineName = "_LayerDepthOutlineTexture";

        private static readonly Vector4 BlitScaleBias =
            new Vector4(1f, 1f, 0f, 0f);

        private static readonly int OriginalSelectedLayerDataTextureId =
            Shader.PropertyToID("_OriginalSelectedLayerDataTexture");

        private static readonly int DisplacedOutlineLayerTextureId =
            Shader.PropertyToID("_DisplacedOutlineLayerTexture");

        private static readonly int OutlineTextureId =
            Shader.PropertyToID("_LayerDepthOutlineTexture");

        private static readonly int OutlineColorId =
            Shader.PropertyToID("_OutlineColor");

        private static readonly int DepthThresholdId =
            Shader.PropertyToID("_DepthThreshold");

        private static readonly int DepthThresholdDistanceScaleId =
            Shader.PropertyToID("_DepthThresholdDistanceScale");

        private static readonly int LineThicknessId =
            Shader.PropertyToID("_LineThickness");

        private static readonly int FeatherId =
            Shader.PropertyToID("_Feather");

        private static readonly int ReferenceResolutionYId =
            Shader.PropertyToID("_ReferenceResolutionY");

        private static readonly int FadeStartDistanceId =
            Shader.PropertyToID("_FadeStartDistance");

        private static readonly int FadeEndDistanceId =
            Shader.PropertyToID("_FadeEndDistance");

        private static readonly int DisplacementPixelsId =
            Shader.PropertyToID("_DisplacementPixels");

        private static readonly int DisplacementNoiseFrequencyId =
            Shader.PropertyToID("_DisplacementNoiseFrequency");

        private static readonly int DisplacementNoiseOctavesId =
            Shader.PropertyToID("_DisplacementNoiseOctaves");

        private static readonly int DisplacementNoiseSeedId =
            Shader.PropertyToID("_DisplacementNoiseSeed");

        private static readonly int DisplacementOcclusionBiasId =
            Shader.PropertyToID("_DisplacementOcclusionBias");

        private static readonly int CompositeOpacityId =
            Shader.PropertyToID("_CompositeOpacity");

        private static readonly List<ShaderTagId> ShaderTagIds = new()
        {
            new ShaderTagId("UniversalForward"),
            new ShaderTagId("UniversalForwardOnly"),
            new ShaderTagId("SRPDefaultUnlit")
        };

        private readonly LayerDepthOutlineRendererFeature m_Feature;

        public LayerDepthOutlinePass(LayerDepthOutlineRendererFeature feature)
        {
            m_Feature = feature;

            ConfigureInput(
                ScriptableRenderPassInput.Depth |
                ScriptableRenderPassInput.Normal
            );
        }

        public override void RecordRenderGraph(
            RenderGraph renderGraph,
            ContextContainer frameData)
        {
            if (!TryGetActiveVolume(out LayerDepthOutlineVolumeComponent volume))
                return;

            UniversalResourceData resources =
                frameData.Get<UniversalResourceData>();

            if (resources.isActiveTargetBackBuffer)
                return;

            TextureHandle cameraColor = resources.activeColorTexture;
            TextureHandle cameraDepthTarget = resources.activeDepthTexture;
            TextureHandle cameraDepthTexture = resources.cameraDepthTexture;
            TextureHandle cameraNormalsTexture = resources.cameraNormalsTexture;

            if (!cameraColor.IsValid() ||
                !cameraDepthTarget.IsValid() ||
                !cameraDepthTexture.IsValid() ||
                !cameraNormalsTexture.IsValid())
            {
                return;
            }

            SetMaterialProperties(volume);

            TextureHandle baseSceneData = CreateColorTexture(
                renderGraph,
                cameraColor,
                BaseSceneDataName,
                GraphicsFormat.R32G32B32A32_SFloat
            );

            TextureHandle displacedLayerData = CreateColorTexture(
                renderGraph,
                cameraColor,
                DisplacedLayerDataName,
                GraphicsFormat.R32G32B32A32_SFloat,
                clear: true
            );

            TextureHandle displacedLayerDepth = CreateDepthTexture(
                renderGraph,
                cameraDepthTarget,
                DisplacedLayerDepthName
            );

            TextureHandle finalSceneData = CreateColorTexture(
                renderGraph,
                cameraColor,
                FinalSceneDataName,
                GraphicsFormat.R32G32B32A32_SFloat
            );

            TextureHandle edgeSeeds = CreateColorTexture(
                renderGraph,
                cameraColor,
                EdgeSeedName,
                GraphicsFormat.R8G8B8A8_UNorm
            );

            TextureHandle outlineTexture = CreateColorTexture(
                renderGraph,
                cameraColor,
                OutlineName,
                GraphicsFormat.R8G8B8A8_UNorm
            );

            TextureHandle compositeDestination = CreateColorTexture(
                renderGraph,
                cameraColor,
                "Camera Color With Layer Depth Outline"
            );

            AddFullscreenPass(
                renderGraph,
                "Pack Scene Data",
                cameraDepthTexture,
                baseSceneData,
                m_Feature.m_PackSceneDataMaterial,
                additionalInput: cameraNormalsTexture
            );

            AddSelectedLayerPass(
                renderGraph,
                frameData,
                "Write Original Selected Layer Mask",
                baseSceneData,
                cameraDepthTarget,
                materialPassIndex: 0,
                colorAccess: AccessFlags.ReadWrite,
                depthAccess: AccessFlags.Read,
                globalTextureId: OriginalSelectedLayerDataTextureId
            );

            AddSelectedLayerPass(
                renderGraph,
                frameData,
                "Render Displaced Outline Layer",
                displacedLayerData,
                displacedLayerDepth,
                materialPassIndex: 1,
                colorAccess: AccessFlags.Write,
                depthAccess: AccessFlags.Write,
                globalTextureId: DisplacedOutlineLayerTextureId
            );

            AddFullscreenPass(
                renderGraph,
                "Merge Displaced Outline Layer",
                baseSceneData,
                finalSceneData,
                m_Feature.m_PackSceneDataMaterial,
                passIndex: 1,
                additionalInput: displacedLayerData
            );

            AddFullscreenPass(
                renderGraph,
                "Detect Layer Depth Edges",
                finalSceneData,
                edgeSeeds,
                m_Feature.m_GenerateOutlineMaterial,
                additionalInput: baseSceneData
            );

            AddFullscreenPass(
                renderGraph,
                "Antialias Layer Depth Edges",
                edgeSeeds,
                outlineTexture,
                m_Feature.m_GenerateOutlineMaterial,
                passIndex: 1,
                globalTextureId: OutlineTextureId
            );

            AddFullscreenPass(
                renderGraph,
                "Composite Layer Depth Outline",
                cameraColor,
                compositeDestination,
                m_Feature.m_CompositeOutlineMaterial,
                additionalInput: outlineTexture
            );

            resources.cameraColor = compositeDestination;
        }

        private RendererListHandle CreateSelectedRendererList(
            RenderGraph renderGraph,
            ContextContainer frameData,
            int materialPassIndex)
        {
            UniversalRenderingData renderingData =
                frameData.Get<UniversalRenderingData>();

            UniversalCameraData cameraData =
                frameData.Get<UniversalCameraData>();

            UniversalLightData lightData =
                frameData.Get<UniversalLightData>();

            DrawingSettings drawingSettings =
                RenderingUtils.CreateDrawingSettings(
                    ShaderTagIds,
                    renderingData,
                    cameraData,
                    lightData,
                    cameraData.defaultOpaqueSortFlags
                );

            drawingSettings.overrideMaterial =
                m_Feature.m_SelectedLayerDepthMaskMaterial;

            drawingSettings.overrideMaterialPassIndex = materialPassIndex;

            FilteringSettings filteringSettings =
                new FilteringSettings(
                    RenderQueueRange.opaque,
                    m_Feature.m_OutlineLayerMask.value
                );

            return renderGraph.CreateRendererList(
                new RendererListParams(
                    renderingData.cullResults,
                    drawingSettings,
                    filteringSettings
                )
            );
        }

        private void AddSelectedLayerPass(
            RenderGraph renderGraph,
            ContextContainer frameData,
            string passName,
            TextureHandle colorTarget,
            TextureHandle depthTarget,
            int materialPassIndex,
            AccessFlags colorAccess,
            AccessFlags depthAccess,
            int globalTextureId = -1)
        {
            using IRasterRenderGraphBuilder builder =
                renderGraph.AddRasterRenderPass<RendererListPassData>(
                    passName,
                    out RendererListPassData passData
                );

            passData.rendererList = CreateSelectedRendererList(
                renderGraph,
                frameData,
                materialPassIndex
            );

            builder.UseRendererList(passData.rendererList);
            builder.SetRenderAttachment(colorTarget, 0, colorAccess);
            builder.SetRenderAttachmentDepth(depthTarget, depthAccess);

            if (globalTextureId >= 0)
            {
                builder.SetGlobalTextureAfterPass(colorTarget, globalTextureId);
                builder.AllowGlobalStateModification(true);
            }

            builder.SetRenderFunc(
                static (RendererListPassData data, RasterGraphContext context) =>
                {
                    context.cmd.DrawRendererList(data.rendererList);
                }
            );
        }

        private static void AddFullscreenPass(
            RenderGraph renderGraph,
            string passName,
            TextureHandle source,
            TextureHandle destination,
            Material material,
            int passIndex = 0,
            TextureHandle additionalInput = default,
            int globalTextureId = -1)
        {
            using IRasterRenderGraphBuilder builder =
                renderGraph.AddRasterRenderPass<FullscreenPassData>(
                    passName,
                    out FullscreenPassData passData
                );

            passData.source = source;
            passData.material = material;
            passData.passIndex = passIndex;

            builder.UseTexture(source, AccessFlags.Read);

            if (additionalInput.IsValid())
                builder.UseTexture(additionalInput, AccessFlags.Read);

            builder.SetRenderAttachment(
                destination,
                0,
                AccessFlags.WriteAll
            );

            if (globalTextureId >= 0)
            {
                builder.SetGlobalTextureAfterPass(destination, globalTextureId);
                builder.AllowGlobalStateModification(true);
            }

            builder.SetRenderFunc(
                static (FullscreenPassData data, RasterGraphContext context) =>
                {
                    Blitter.BlitTexture(
                        context.cmd,
                        data.source,
                        BlitScaleBias,
                        data.material,
                        data.passIndex
                    );
                }
            );
        }

        private static TextureHandle CreateColorTexture(
            RenderGraph renderGraph,
            TextureHandle template,
            string name,
            GraphicsFormat format = GraphicsFormat.None,
            bool clear = false)
        {
            TextureDesc desc = template.GetDescriptor(renderGraph);

            desc.name = name;
            desc.clearBuffer = clear;
            desc.clearColor = Color.clear;
            desc.depthBufferBits = 0;

            if (format != GraphicsFormat.None)
                desc.colorFormat = format;

            return renderGraph.CreateTexture(desc);
        }

        private static TextureHandle CreateDepthTexture(
            RenderGraph renderGraph,
            TextureHandle template,
            string name)
        {
            TextureDesc desc = template.GetDescriptor(renderGraph);
            desc.name = name;
            desc.clearBuffer = true;

            return renderGraph.CreateTexture(desc);
        }

        private void SetMaterialProperties(
            LayerDepthOutlineVolumeComponent volume)
        {
            Material outline = m_Feature.m_GenerateOutlineMaterial;

            outline.SetColor(OutlineColorId, volume.outlineColor.value);
            outline.SetFloat(DepthThresholdId, volume.depthThreshold.value);
            outline.SetFloat(
                DepthThresholdDistanceScaleId,
                volume.depthThresholdDistanceScale.value
            );
            outline.SetFloat(LineThicknessId, volume.lineThickness.value);
            outline.SetFloat(FeatherId, volume.feather.value);
            outline.SetFloat(
                ReferenceResolutionYId,
                volume.referenceResolutionY.value
            );
            outline.SetFloat(
                FadeStartDistanceId,
                volume.fadeStartDistance.value
            );
            outline.SetFloat(
                FadeEndDistanceId,
                volume.fadeEndDistance.value
            );

            Material selected = m_Feature.m_SelectedLayerDepthMaskMaterial;

            selected.SetFloat(
                DisplacementPixelsId,
                volume.displacementPixels.value
            );
            selected.SetFloat(
                DisplacementNoiseFrequencyId,
                volume.displacementNoiseFrequency.value
            );
            selected.SetInt(
                DisplacementNoiseOctavesId,
                volume.displacementNoiseOctaves.value
            );
            selected.SetInt(
                DisplacementNoiseSeedId,
                volume.displacementNoiseSeed.value
            );
            selected.SetFloat(
                ReferenceResolutionYId,
                volume.referenceResolutionY.value
            );
            selected.SetFloat(
                FadeStartDistanceId,
                volume.fadeStartDistance.value
            );
            selected.SetFloat(
                FadeEndDistanceId,
                volume.fadeEndDistance.value
            );

            m_Feature.m_PackSceneDataMaterial.SetFloat(
                DisplacementOcclusionBiasId,
                volume.displacementOcclusionBias.value
            );

            m_Feature.m_CompositeOutlineMaterial.SetFloat(
                CompositeOpacityId,
                volume.opacity.value
            );
        }

        private sealed class RendererListPassData
        {
            public RendererListHandle rendererList;
        }

        private sealed class FullscreenPassData
        {
            public TextureHandle source;
            public Material material;
            public int passIndex;
        }
    }
}
