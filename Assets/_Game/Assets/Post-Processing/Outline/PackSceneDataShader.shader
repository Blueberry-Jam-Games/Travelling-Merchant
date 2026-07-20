Shader "TravellingMerchant/Outline/Pack Scene Depth Normal Layer Mask"
{
    Properties
    {
        _DisplacementOcclusionBias ("Displacement Occlusion Bias", Float) = 0.05
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

        HLSLINCLUDE

        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareNormalsTexture.hlsl"

        static const float DEPTH_EPSILON = 1e-6;

        TEXTURE2D_X(_DisplacedOutlineLayerTexture);
        SAMPLER(sampler_DisplacedOutlineLayerTexture);

        float _DisplacementOcclusionBias;

        float SafeSignedValue(float value)
        {
            if (abs(value) >= DEPTH_EPSILON)
                return value;

            return value < 0.0
                ? -DEPTH_EPSILON
                : DEPTH_EPSILON;
        }

        float2 SignNotZero(float2 value)
        {
            return float2(
                value.x >= 0.0 ? 1.0 : -1.0,
                value.y >= 0.0 ? 1.0 : -1.0
            );
        }

        float3 DecodeNormalOct(float2 encoded)
        {
            float2 value = encoded * 2.0 - 1.0;

            float3 normalWS = float3(
                value.x,
                value.y,
                1.0 - abs(value.x) - abs(value.y)
            );

            if (normalWS.z < 0.0)
            {
                normalWS.xy =
                    (1.0 - abs(normalWS.yx)) *
                    SignNotZero(normalWS.xy);
            }

            return normalize(normalWS);
        }

        float3 GetReferencePositionVS(float2 uv)
        {
            float3 positionWS = ComputeWorldSpacePosition(
                uv,
                0.5,
                UNITY_MATRIX_I_VP
            );

            return TransformWorldToView(positionWS);
        }

        float3 GetViewRayPerEyeDepth(float2 uv)
        {
            float3 positionVS = GetReferencePositionVS(uv);

            return positionVS /
                SafeSignedValue(-positionVS.z);
        }

        float2 CalculatePerspectiveGradient(
            float2 uv,
            float eyeDepth,
            float3 normalVS)
        {
            float2 texelSize = rcp(_ScaledScreenParams.xy);

            float3 centerRayVS = GetViewRayPerEyeDepth(uv);
            float3 rightRayVS = GetViewRayPerEyeDepth(
                uv + float2(texelSize.x, 0.0)
            );
            float3 upRayVS = GetViewRayPerEyeDepth(
                uv + float2(0.0, texelSize.y)
            );

            float3 positionVS = centerRayVS * eyeDepth;

            float planeConstant =
                SafeSignedValue(dot(normalVS, positionVS));

            float currentInvDepth =
                rcp(max(eyeDepth, DEPTH_EPSILON));

            float rightInvDepth =
                dot(normalVS, rightRayVS) /
                planeConstant;

            float upInvDepth =
                dot(normalVS, upRayVS) /
                planeConstant;

            return float2(
                rightInvDepth - currentInvDepth,
                upInvDepth - currentInvDepth
            );
        }

        float2 CalculateOrthographicGradient(
            float2 uv,
            float eyeDepth,
            float3 normalVS)
        {
            float2 texelSize = rcp(_ScaledScreenParams.xy);

            float3 centerReferenceVS = GetReferencePositionVS(uv);
            float3 rightReferenceVS = GetReferencePositionVS(
                uv + float2(texelSize.x, 0.0)
            );
            float3 upReferenceVS = GetReferencePositionVS(
                uv + float2(0.0, texelSize.y)
            );

            float3 positionVS = float3(
                centerReferenceVS.xy,
                -eyeDepth
            );

            float planeConstant =
                dot(normalVS, positionVS);

            float normalZ =
                SafeSignedValue(normalVS.z);

            float rightZ =
                (
                    planeConstant -
                    normalVS.x * rightReferenceVS.x -
                    normalVS.y * rightReferenceVS.y
                ) / normalZ;

            float upZ =
                (
                    planeConstant -
                    normalVS.x * upReferenceVS.x -
                    normalVS.y * upReferenceVS.y
                ) / normalZ;

            return float2(
                -rightZ - eyeDepth,
                -upZ - eyeDepth
            );
        }

        float2 CalculateDepthGradient(
            float2 uv,
            float eyeDepth,
            float3 normalWS)
        {
            float3 normalVS = normalize(
                mul((float3x3)UNITY_MATRIX_V, normalWS)
            );

            if (unity_OrthoParams.w == 0.0)
            {
                return CalculatePerspectiveGradient(
                    uv,
                    eyeDepth,
                    normalVS
                );
            }

            return CalculateOrthographicGradient(
                uv,
                eyeDepth,
                normalVS
            );
        }

        bool HasSceneGeometry(float rawDepth)
        {
            #if UNITY_REVERSED_Z
                return rawDepth > 0.000001;
            #else
                return rawDepth < 0.999999;
            #endif
        }

        ENDHLSL

        Pass
        {
            Name "Pack Scene Depth Gradient"

            HLSLPROGRAM

            #pragma vertex Vert
            #pragma fragment FragPackScene
            #pragma target 3.5

            float4 FragPackScene(Varyings input) : SV_Target
            {
                float2 uv = input.texcoord;

                float rawDepth = SampleSceneDepth(uv);

                if (!HasSceneGeometry(rawDepth))
                    return float4(0.0, 0.0, 0.0, 0.0);

                float eyeDepth =
                    unity_OrthoParams.w == 0.0
                    ? LinearEyeDepth(rawDepth, _ZBufferParams)
                    : LinearDepthToEyeDepth(rawDepth);

                float3 normalWS =
                    normalize(SampleSceneNormals(uv));

                float2 depthGradient =
                    CalculateDepthGradient(
                        uv,
                        eyeDepth,
                        normalWS
                    );

                return float4(
                    eyeDepth,
                    depthGradient.x,
                    depthGradient.y,
                    0.0
                );
            }

            ENDHLSL
        }

        Pass
        {
            Name "Merge Displaced Outline Layer"

            HLSLPROGRAM

            #pragma vertex Vert
            #pragma fragment FragMergeDisplaced
            #pragma target 3.5

            float4 FragMergeDisplaced(Varyings input) : SV_Target
            {
                float2 uv = input.texcoord;

                float4 sceneData = SAMPLE_TEXTURE2D_X(
                    _BlitTexture,
                    sampler_PointClamp,
                    uv
                );

                float4 displacedData = SAMPLE_TEXTURE2D_X(
                    _DisplacedOutlineLayerTexture,
                    sampler_DisplacedOutlineLayerTexture,
                    uv
                );

                bool hasDisplaced =
                    displacedData.a > 0.5 &&
                    displacedData.r > DEPTH_EPSILON;

                if (!hasDisplaced)
                    return float4(sceneData.rgb, 0.0);

                bool originalSelected =
                    sceneData.a > 0.5;

                bool sceneHasGeometry =
                    sceneData.r > DEPTH_EPSILON;

                bool blockedByNonSelected =
                    sceneHasGeometry &&
                    !originalSelected &&
                    sceneData.r + _DisplacementOcclusionBias <
                    displacedData.r;

                if (blockedByNonSelected)
                    return float4(sceneData.rgb, 0.0);

                float3 displacedNormalWS =
                    DecodeNormalOct(displacedData.gb);

                float2 displacedGradient =
                    CalculateDepthGradient(
                        uv,
                        displacedData.r,
                        displacedNormalWS
                    );

                return float4(
                    displacedData.r,
                    displacedGradient.x,
                    displacedGradient.y,
                    1.0
                );
            }

            ENDHLSL
        }
    }
}
