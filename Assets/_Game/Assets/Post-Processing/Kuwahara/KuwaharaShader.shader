Shader "TravellingMerchant/Post/Kuwahara Filter"
{
    Properties
    {
        _Intensity ("Intensity", Range(0, 1)) = 1
        _Radius ("Radius", Int) = 4

        _DepthSensitivity ("Depth Sensitivity", Range(0, 200)) = 50

        _FadeStartDistance ("Fade Start Distance", Float) = 20
        _FadeEndDistance ("Fade End Distance", Float) = 80
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
            Name "Kuwahara Filter"

            HLSLPROGRAM

            #pragma vertex Vert
            #pragma fragment Frag
            #pragma target 3.5

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

            float _Intensity;
            int _Radius;

            float _DepthSensitivity;
            float _FadeStartDistance;
            float _FadeEndDistance;

            struct RegionData
            {
                float3 mean;
                float variance;
            };

            float SampleLinearEyeDepth(float2 uv)
            {
                float rawDepth = SampleSceneDepth(uv);
                return LinearEyeDepth(rawDepth, _ZBufferParams);
            }

            float CalculateDepthWeight(float centerDepth, float sampleDepth)
            {
                if (_DepthSensitivity <= 0.0001)
                    return 1.0;

                float relativeDepthDifference =
                    abs(sampleDepth - centerDepth) / max(centerDepth, 0.001);

                return exp2(-relativeDepthDifference * _DepthSensitivity);
            }

            float CalculateDistanceFade(float centerDepth)
            {
                float fadeRange = max(_FadeEndDistance - _FadeStartDistance, 0.001);

                float fade =
                    saturate((centerDepth - _FadeStartDistance) / fadeRange);

                return smoothstep(0.0, 1.0, fade);
            }

            RegionData CalculateRegion(
                int2 lower,
                int2 upper,
                float2 uv,
                float centerDepth
            )
            {
                RegionData region;

                float3 weightedSum = 0.0;
                float3 weightedSquareSum = 0.0;
                float weightSum = 0.0;

                [loop]
                for (int y = lower.y; y <= upper.y; y++)
                {
                    [loop]
                    for (int x = lower.x; x <= upper.x; x++)
                    {
                        float2 offset = float2(x, y) * _BlitTexture_TexelSize.xy;
                        float2 sampleUV = uv + offset;

                        float3 color =
                            SAMPLE_TEXTURE2D(_BlitTexture, sampler_LinearClamp, sampleUV).rgb;

                        float sampleDepth = SampleLinearEyeDepth(sampleUV);

                        float depthWeight =
                            CalculateDepthWeight(centerDepth, sampleDepth);

                        weightedSum += color * depthWeight;
                        weightedSquareSum += color * color * depthWeight;
                        weightSum += depthWeight;
                    }
                }

                float inverseWeightSum = rcp(max(weightSum, 0.00001));

                region.mean = weightedSum * inverseWeightSum;

                float3 variance =
                    abs((weightedSquareSum * inverseWeightSum) - (region.mean * region.mean));

                region.variance = dot(variance, float3(0.299, 0.587, 0.114));

                return region;
            }

            float4 Frag(Varyings input) : SV_Target
            {
                float2 uv = input.texcoord;

                float3 originalColor =
                    SAMPLE_TEXTURE2D(_BlitTexture, sampler_LinearClamp, uv).rgb;

                float centerDepth = SampleLinearEyeDepth(uv);

                int radius = max(_Radius, 1);

                RegionData regionA = CalculateRegion(
                    int2(-radius, -radius),
                    int2(0, 0),
                    uv,
                    centerDepth
                );

                RegionData regionB = CalculateRegion(
                    int2(0, -radius),
                    int2(radius, 0),
                    uv,
                    centerDepth
                );

                RegionData regionC = CalculateRegion(
                    int2(-radius, 0),
                    int2(0, radius),
                    uv,
                    centerDepth
                );

                RegionData regionD = CalculateRegion(
                    int2(0, 0),
                    int2(radius, radius),
                    uv,
                    centerDepth
                );

                float3 kuwaharaColor = regionA.mean;
                float minVariance = regionA.variance;

                if (regionB.variance < minVariance)
                {
                    minVariance = regionB.variance;
                    kuwaharaColor = regionB.mean;
                }

                if (regionC.variance < minVariance)
                {
                    minVariance = regionC.variance;
                    kuwaharaColor = regionC.mean;
                }

                if (regionD.variance < minVariance)
                {
                    kuwaharaColor = regionD.mean;
                }

                float distanceFade = CalculateDistanceFade(centerDepth);

                float blend = saturate(_Intensity) * distanceFade;

                float3 finalColor =
                    lerp(originalColor, kuwaharaColor, blend);

                return float4(finalColor, 1.0);
            }

            ENDHLSL
        }
    }
}