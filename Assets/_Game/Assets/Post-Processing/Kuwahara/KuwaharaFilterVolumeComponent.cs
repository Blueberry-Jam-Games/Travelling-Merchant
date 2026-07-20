using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

[Serializable]
[VolumeComponentMenu("Post-processing Custom/Kuwahara Filter")]
[VolumeRequiresRendererFeatures(typeof(KuwaharaFilterRendererFeature))]
[SupportedOnRenderPipeline(typeof(UniversalRenderPipelineAsset))]
[DisplayInfo(name = "Kuwahara Filter")]
public sealed class KuwaharaFilterVolumeComponent : VolumeComponent, IPostProcessComponent
{
    [Tooltip("Blend amount for the Kuwahara effect.")]
    public ClampedFloatParameter intensity = new ClampedFloatParameter(1f, 0f, 1f);

    [Tooltip("Filter radius in pixels. Higher values are much more expensive.")]
    public ClampedIntParameter radius = new ClampedIntParameter(4, 1, 12);

    [Tooltip("How strongly depth differences reject samples. Higher values preserve silhouettes and hard depth edges more aggressively.")]
    public ClampedFloatParameter depthSensitivity = new ClampedFloatParameter(50f, 0f, 200f);

    [Tooltip("Distance where the Kuwahara effect starts fading in.")]
    public MinFloatParameter fadeStartDistance = new MinFloatParameter(20f, 0f);

    [Tooltip("Distance where the Kuwahara effect reaches full strength.")]
    public MinFloatParameter fadeEndDistance = new MinFloatParameter(80f, 0f);

    public bool IsActive()
    {
        return intensity.value > 0f && radius.value > 0;
    }

    public bool IsTileCompatible()
    {
        return false;
    }
}