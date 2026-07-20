Shader "TravellingMerchant/Outline/Warped Depth Map"
{
    Properties
    {
        _DisplacementPixels ("Displacement Pixels", Range(0, 16)) = 1.5
        _DisplacementNoiseFrequency ("Displacement Noise Frequency", Float) = 0.02
        _DisplacementNoiseOctaves ("Displacement Noise Octaves", Int) = 3
        _DisplacementNoiseSeed ("Displacement Noise Seed", Int) = 1337

        _ReferenceResolutionY ("Reference Resolution Y", Float) = 1080
        _FadeStartDistance ("Fade Start Distance", Float) = 50
        _FadeEndDistance ("Fade End Distance", Float) = 90
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "RenderType" = "Opaque"
        }

        Pass
        {
            Name "Original Selected Layer Mask"

            ZWrite Off
            ZTest LEqual
            Cull Back
            ColorMask A

            HLSLPROGRAM

            #pragma vertex Vert
            #pragma fragment Frag
            #pragma target 3.5

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                return output;
            }

            float4 Frag(Varyings input) : SV_Target
            {
                return float4(0.0, 0.0, 0.0, 1.0);
            }

            ENDHLSL
        }

        Pass
        {
            Name "Displaced Selected Layer Data"

            ZWrite On
            ZTest LEqual
            Cull Back

            HLSLPROGRAM

            #pragma vertex Vert
            #pragma fragment Frag
            #pragma target 3.5

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Assets/_Game/Resources/ComputeShaders/Includes/FastNoiseLite.hlsl"

            static const float EPSILON = 1e-6;

            float _DisplacementPixels;
            float _DisplacementNoiseFrequency;
            int _DisplacementNoiseOctaves;
            int _DisplacementNoiseSeed;

            float _ReferenceResolutionY;
            float _FadeStartDistance;
            float _FadeEndDistance;

            struct Attributes
            {
                float4 positionOS : POSITION;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float eyeDepth : TEXCOORD1;
            };

            float CalculateDistanceFade(float depth)
            {
                if (_FadeEndDistance <= _FadeStartDistance + EPSILON)
                    return depth >= _FadeStartDistance ? 1.0 : 0.0;

                float t = saturate(
                    (depth - _FadeStartDistance) /
                    (_FadeEndDistance - _FadeStartDistance)
                );

                return smoothstep(0.0, 1.0, t);
            }

            float3 SampleDisplacementNoise(float3 positionWS)
            {
                fnl_state state = fnlCreateState(_DisplacementNoiseSeed);
                state.noise_type = FNL_NOISE_OPENSIMPLEX2;
                state.fractal_type = FNL_FRACTAL_FBM;
                state.frequency = max(_DisplacementNoiseFrequency, EPSILON);
                state.octaves = clamp(_DisplacementNoiseOctaves, 1, 8);
                state.lacunarity = 2.0;
                state.gain = 0.5;

                float x = fnlGetNoise3D(state, positionWS.x, positionWS.y, positionWS.z);

                state.seed = _DisplacementNoiseSeed + 1013;
                float y = fnlGetNoise3D(state, positionWS.x, positionWS.y, positionWS.z);

                state.seed = _DisplacementNoiseSeed + 1619;
                float z = fnlGetNoise3D(state, positionWS.x, positionWS.y, positionWS.z);

                return float3(x, y, z);
            }

            float GetWorldUnitsPerReferencePixel(float eyeDepth)
            {
                float projectionY = max(abs(UNITY_MATRIX_P._m11), EPSILON);
                float depthScale = unity_OrthoParams.w > 0.5
                    ? 1.0
                    : max(eyeDepth, EPSILON);

                return 2.0 * depthScale /
                    (projectionY * max(_ReferenceResolutionY, 1.0));
            }

            float2 SignNotZero(float2 value)
            {
                return float2(
                    value.x >= 0.0 ? 1.0 : -1.0,
                    value.y >= 0.0 ? 1.0 : -1.0
                );
            }

            float2 EncodeNormalOct(float3 normalWS)
            {
                normalWS /= max(
                    abs(normalWS.x) + abs(normalWS.y) + abs(normalWS.z),
                    EPSILON
                );

                float2 encoded = normalWS.xy;

                if (normalWS.z < 0.0)
                {
                    encoded =
                        (1.0 - abs(encoded.yx)) *
                        SignNotZero(encoded);
                }

                return encoded * 0.5 + 0.5;
            }

            Varyings Vert(Attributes input)
            {
                Varyings output;

                float3 positionWS =
                    TransformObjectToWorld(input.positionOS.xyz);

                float eyeDepth =
                    max(-TransformWorldToView(positionWS).z, 0.0);

                float distanceFade =
                    CalculateDistanceFade(eyeDepth);

                float worldAmplitude =
                    _DisplacementPixels *
                    GetWorldUnitsPerReferencePixel(eyeDepth) *
                    distanceFade;

                positionWS +=
                    SampleDisplacementNoise(positionWS) *
                    worldAmplitude;

                output.positionWS = positionWS;
                output.positionCS = TransformWorldToHClip(positionWS);
                output.eyeDepth =
                    max(-TransformWorldToView(positionWS).z, 0.0);

                return output;
            }

            float4 Frag(Varyings input) : SV_Target
            {
                float3 tangentX = ddx(input.positionWS);
                float3 tangentY = ddy(input.positionWS);
                float3 normalWS = cross(tangentX, tangentY);

                normalWS *= rsqrt(
                    max(dot(normalWS, normalWS), EPSILON)
                );

                float2 encodedNormal = EncodeNormalOct(normalWS);

                return float4(
                    input.eyeDepth,
                    encodedNormal.x,
                    encodedNormal.y,
                    1.0
                );
            }

            ENDHLSL
        }
    }
}
