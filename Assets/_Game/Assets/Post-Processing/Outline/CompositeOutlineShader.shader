Shader "TravellingMerchant/Outline/Composite Layer Depth Outline"
{
    Properties
    {
        _CompositeOpacity ("Composite Opacity", Range(0, 1)) = 1
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "RenderType" = "Opaque"
        }

        ZWrite Off
        ZTest Always
        Cull Off

        Pass
        {
            Name "Composite Layer Depth Outline"

            HLSLPROGRAM

            #pragma vertex Vert
            #pragma fragment Frag
            #pragma target 3.5

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

            TEXTURE2D_X(_LayerDepthOutlineTexture);
            SAMPLER(sampler_LayerDepthOutlineTexture);

            float _CompositeOpacity;

            float4 Frag(Varyings input) : SV_Target
            {
                float2 uv = input.texcoord;

                float3 sceneColor =
                    SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv).rgb;

                float4 outline =
                    SAMPLE_TEXTURE2D_X(
                        _LayerDepthOutlineTexture,
                        sampler_LayerDepthOutlineTexture,
                        uv
                    );

                float alpha = saturate(outline.a * _CompositeOpacity);

                float3 finalColor =
                    lerp(sceneColor, outline.rgb, alpha);

                return float4(finalColor, 1.0);
            }

            ENDHLSL
        }
    }
}