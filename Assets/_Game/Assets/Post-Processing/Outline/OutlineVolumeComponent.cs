using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

[Serializable]
[VolumeComponentMenu("Post-processing Custom/Layer Depth Outline")]
[VolumeRequiresRendererFeatures(typeof(LayerDepthOutlineRendererFeature))]
[SupportedOnRenderPipeline(typeof(UniversalRenderPipelineAsset))]
[DisplayInfo(name = "Layer Depth Outline")]
public sealed class LayerDepthOutlineVolumeComponent :
    VolumeComponent,
    IPostProcessComponent
{
    [Tooltip("Color of the outline.")]
    public ColorParameter outlineColor =
        new ColorParameter(new Color(0.14f, 0.08f, 0.18f, 1f));

    [Tooltip("Minimum depth prediction error needed to create an edge.")]
    public MinFloatParameter depthThreshold =
        new MinFloatParameter(0.05f, 0f);

    [Tooltip("Amount added to the depth threshold per unit of distance.")]
    public MinFloatParameter depthThresholdDistanceScale =
        new MinFloatParameter(0.001f, 0f);

    [Tooltip("Line thickness in pixels at the reference resolution.")]
    public ClampedFloatParameter lineThickness =
        new ClampedFloatParameter(1f, 0f, 16f);

    [Tooltip("Additional soft falloff around the line in pixels.")]
    public ClampedFloatParameter feather =
        new ClampedFloatParameter(0f, 0f, 8f);

    [Tooltip("Reference render height used to scale thickness, feathering, and displacement.")]
    public MinFloatParameter referenceResolutionY =
        new MinFloatParameter(1080f, 1f);

    [Tooltip("Below this distance, no outline is drawn.")]
    public MinFloatParameter fadeStartDistance =
        new MinFloatParameter(50f, 0f);

    [Tooltip("At this distance and beyond, the outline is at full strength.")]
    public MinFloatParameter fadeEndDistance =
        new MinFloatParameter(90f, 0f);

    [Tooltip("Approximate 3D vertex displacement in pixels at the reference resolution.")]
    public ClampedFloatParameter displacementPixels =
        new ClampedFloatParameter(1.5f, 0f, 16f);

    [Tooltip("World-space frequency of the persistent FBM displacement noise.")]
    public MinFloatParameter displacementNoiseFrequency =
        new MinFloatParameter(0.02f, 0.000001f);

    [Tooltip("FBM octave count used for vertex displacement.")]
    public ClampedIntParameter displacementNoiseOctaves =
        new ClampedIntParameter(3, 1, 8);

    [Tooltip("Persistent seed used for vertex displacement.")]
    public IntParameter displacementNoiseSeed =
        new IntParameter(1337);

    [Tooltip("Depth tolerance used when displaced outlines are occluded by non-outline geometry.")]
    public MinFloatParameter displacementOcclusionBias =
        new MinFloatParameter(0.05f, 0f);

    [Tooltip("Final outline opacity.")]
    public ClampedFloatParameter opacity =
        new ClampedFloatParameter(1f, 0f, 1f);

    public bool IsActive()
    {
        return lineThickness.value > 0f &&
               opacity.value > 0f;
    }

    public bool IsTileCompatible()
    {
        return false;
    }
}
