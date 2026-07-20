using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.RenderGraphModule.Util;
using UnityEngine.Rendering.Universal;

public sealed class KuwaharaFilterRendererFeature : ScriptableRendererFeature
{
    [SerializeField]
    private Material m_Material;

    [SerializeField]
    private RenderPassEvent m_RenderPassEvent = RenderPassEvent.BeforeRenderingPostProcessing;

    private KuwaharaFilterPass m_Pass;

    public override void Create()
    {
        m_Pass = new KuwaharaFilterPass
        {
            renderPassEvent = m_RenderPassEvent
        };
    }

    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        if (m_Material == null || m_Pass == null)
            return;

        CameraType cameraType = renderingData.cameraData.cameraType;

        if (cameraType == CameraType.Preview || cameraType == CameraType.Reflection)
            return;

        KuwaharaFilterVolumeComponent volume =
            VolumeManager.instance.stack?.GetComponent<KuwaharaFilterVolumeComponent>();

        if (volume == null || !volume.IsActive())
            return;

        m_Pass.Setup(m_Material);
        m_Pass.renderPassEvent = m_RenderPassEvent;

        renderer.EnqueuePass(m_Pass);
    }

    private sealed class KuwaharaFilterPass : ScriptableRenderPass
    {
        private const string PassName = "Kuwahara Filter";
        private const string TempTextureName = "_KuwaharaFilterTexture";

        private static readonly int IntensityId = Shader.PropertyToID("_Intensity");
        private static readonly int RadiusId = Shader.PropertyToID("_Radius");
        private static readonly int DepthSensitivityId = Shader.PropertyToID("_DepthSensitivity");
        private static readonly int FadeStartDistanceId = Shader.PropertyToID("_FadeStartDistance");
        private static readonly int FadeEndDistanceId = Shader.PropertyToID("_FadeEndDistance");

        private Material m_Material;

        public KuwaharaFilterPass()
        {
            ConfigureInput(ScriptableRenderPassInput.Depth);
        }

        public void Setup(Material material)
        {
            m_Material = material;
        }

        public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
        {
            if (m_Material == null)
                return;

            KuwaharaFilterVolumeComponent volume =
                VolumeManager.instance.stack?.GetComponent<KuwaharaFilterVolumeComponent>();

            if (volume == null || !volume.IsActive())
                return;

            UniversalResourceData resourceData = frameData.Get<UniversalResourceData>();

            // Avoid trying to sample directly from the back buffer.
            if (resourceData.isActiveTargetBackBuffer)
                return;

            TextureHandle source = resourceData.activeColorTexture;

            if (!source.IsValid())
                return;

            m_Material.SetFloat(IntensityId, volume.intensity.value);
            m_Material.SetInt(RadiusId, volume.radius.value);
            m_Material.SetFloat(DepthSensitivityId, volume.depthSensitivity.value);
            m_Material.SetFloat(FadeStartDistanceId, volume.fadeStartDistance.value);
            m_Material.SetFloat(FadeEndDistanceId, volume.fadeEndDistance.value);
            

            TextureDesc destinationDesc = source.GetDescriptor(renderGraph);
            destinationDesc.name = TempTextureName;
            destinationDesc.clearBuffer = false;
            destinationDesc.depthBufferBits = 0;

            TextureHandle destination = renderGraph.CreateTexture(destinationDesc);

            if (!destination.IsValid())
                return;

            RenderGraphUtils.BlitMaterialParameters blitParameters =
                new RenderGraphUtils.BlitMaterialParameters(source, destination, m_Material, 0);

            renderGraph.AddBlitPass(blitParameters, PassName);

            // Make the result become the camera color for following passes.
            resourceData.cameraColor = destination;
        }
    }
}