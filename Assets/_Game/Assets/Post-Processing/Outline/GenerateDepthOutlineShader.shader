Shader "TravellingMerchant/Outline/Generate Layer Depth Outline"
{
    Properties
    {
        _OutlineColor ("Outline Color", Color) = (0.14, 0.08, 0.18, 1)

        _DepthThreshold ("Base Depth Threshold", Float) = 0.05
        _DepthThresholdDistanceScale ("Depth Threshold Distance Scale", Float) = 0.001

        _LineThickness ("Line Thickness", Range(0, 16)) = 1
        _Feather ("Feather", Range(0, 8)) = 0
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

        ZWrite Off
        ZTest Always
        Cull Off

        HLSLINCLUDE

        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

        static const float DEPTH_EPSILON = 1e-6;
        static const float AA_WIDTH = 0.35;

        TEXTURE2D_X(_OriginalSelectedLayerDataTexture);
        SAMPLER(sampler_OriginalSelectedLayerDataTexture);

        float4 _OutlineColor;

        float _DepthThreshold;
        float _DepthThresholdDistanceScale;

        float _LineThickness;
        float _Feather;
        float _ReferenceResolutionY;

        float _FadeStartDistance;
        float _FadeEndDistance;

        struct EdgeResult
        {
            float strength;
            float depth;
            float position;
        };

        float4 SampleSource(float2 uv)
        {
            return SAMPLE_TEXTURE2D_X(
                _BlitTexture,
                sampler_PointClamp,
                uv
            );
        }

        bool IsOriginalSelected(float2 uv)
        {
            return SAMPLE_TEXTURE2D_X(
                _OriginalSelectedLayerDataTexture,
                sampler_OriginalSelectedLayerDataTexture,
                uv
            ).a > 0.5;
        }

        bool HasGeometry(float4 data)
        {
            return data.r > DEPTH_EPSILON;
        }

        bool IsSelected(float4 data)
        {
            return data.a > 0.5;
        }

        float PredictDepth(float4 current, float2 pixelOffset)
        {
            float depth = current.r;

            if (unity_OrthoParams.w > 0.5)
                return depth + dot(current.gb, pixelOffset);

            float inverseDepth =
                rcp(max(depth, DEPTH_EPSILON)) +
                dot(current.gb, pixelOffset);

            return inverseDepth > DEPTH_EPSILON
                ? rcp(inverseDepth)
                : depth;
        }

        float GetDepthThreshold(float depth)
        {
            return max(_DepthThreshold, 0.0) +
                depth * max(_DepthThresholdDistanceScale, 0.0);
        }

        float GetPairDepth(float4 current, float4 neighbor)
        {
            bool currentGeometry = HasGeometry(current);
            bool neighborGeometry = HasGeometry(neighbor);

            if (currentGeometry && neighborGeometry)
                return min(current.r, neighbor.r);

            if (currentGeometry)
                return current.r;

            if (neighborGeometry)
                return neighbor.r;

            return 0.0;
        }

        float CalculateDistanceFade(float depth)
        {
            if (depth <= DEPTH_EPSILON)
                return 0.0;

            if (_FadeEndDistance <= _FadeStartDistance + DEPTH_EPSILON)
                return depth >= _FadeStartDistance ? 1.0 : 0.0;

            float t = saturate(
                (depth - _FadeStartDistance) /
                (_FadeEndDistance - _FadeStartDistance)
            );

            return smoothstep(0.0, 1.0, t);
        }

        float EstimateDepthEdgePosition(float depthError, float threshold)
        {
            return depthError > DEPTH_EPSILON
                ? saturate(threshold / depthError)
                : 0.5;
        }

        EdgeResult EvaluatePair(
            float4 current,
            float4 neighbor,
            float2 neighborOffset,
            bool currentOriginalSelected,
            bool neighborOriginalSelected)
        {
            EdgeResult result;
            result.strength = 0.0;
            result.depth = 0.0;
            result.position = 0.5;

            bool currentSelected = IsSelected(current);
            bool neighborSelected = IsSelected(neighbor);

            bool currentGeometry = HasGeometry(current);
            bool neighborGeometry = HasGeometry(neighbor);

            float pairDepth = GetPairDepth(current, neighbor);

            if (currentSelected != neighborSelected)
            {
                result.strength = 1.0;
                result.depth = pairDepth;
                return result;
            }

            bool originalSelectedOnly =
                (currentOriginalSelected || neighborOriginalSelected) &&
                !(currentSelected || neighborSelected);

            if (originalSelectedOnly)
                return result;

            if (currentGeometry != neighborGeometry)
            {
                result.strength = 1.0;
                result.depth = pairDepth;
                return result;
            }

            if (!currentGeometry)
                return result;

            float expectedDepth =
                PredictDepth(current, neighborOffset);

            float depthError =
                abs(neighbor.r - expectedDepth);

            float threshold =
                GetDepthThreshold(pairDepth);

            if (depthError >= threshold)
            {
                result.strength = 1.0;
                result.depth = pairDepth;
                result.position =
                    EstimateDepthEdgePosition(depthError, threshold);
            }

            return result;
        }

        float GetResolutionScale()
        {
            return max(_BlitTexture_TexelSize.w, 1.0) /
                max(_ReferenceResolutionY, 1.0);
        }

        float DistanceToSegment(
            float2 samplePoint,
            float2 segmentStart,
            float2 segmentEnd)
        {
            float2 segment = segmentEnd - segmentStart;
            float lengthSquared = dot(segment, segment);

            if (lengthSquared <= DEPTH_EPSILON)
                return length(samplePoint - segmentStart);

            float t = saturate(
                dot(samplePoint - segmentStart, segment) /
                lengthSquared
            );

            return length(
                samplePoint - (segmentStart + segment * t)
            );
        }

        float4 GetCoverage4(
            float4 distances,
            float thicknessPixels,
            float featherPixels)
        {
            float radius = thicknessPixels * 0.5;
            float innerRadius = max(radius - AA_WIDTH, 0.0);
            float outerRadius =
                radius + AA_WIDTH + max(featherPixels, 0.0);

            float4 coverage =
                1.0 - smoothstep(
                    innerRadius,
                    outerRadius,
                    distances
                );

            return coverage * saturate(thicknessPixels);
        }

        float4 SegmentCoverage4(
            float2 segmentStart,
            float2 segmentEnd,
            float strength,
            float thicknessPixels,
            float featherPixels)
        {
            // Rotated-grid 4x subpixel pattern reduces directional bias.
            float4 distances = float4(
                DistanceToSegment(
                    float2(-0.125, -0.375),
                    segmentStart,
                    segmentEnd
                ),
                DistanceToSegment(
                    float2(0.375, -0.125),
                    segmentStart,
                    segmentEnd
                ),
                DistanceToSegment(
                    float2(0.125, 0.375),
                    segmentStart,
                    segmentEnd
                ),
                DistanceToSegment(
                    float2(-0.375, 0.125),
                    segmentStart,
                    segmentEnd
                )
            );

            return GetCoverage4(
                distances,
                thicknessPixels,
                featherPixels
            ) * strength;
        }

        float4 PointCoverage4(
            float2 samplePoint,
            float strength,
            float thicknessPixels,
            float featherPixels)
        {
            return SegmentCoverage4(
                samplePoint,
                samplePoint,
                strength,
                thicknessPixels,
                featherPixels
            );
        }

        ENDHLSL


        Pass
        {
            Name "Detect Fractional Layer Depth Edges"

            HLSLPROGRAM

            #pragma vertex Vert
            #pragma fragment FragEdgeSeeds
            #pragma target 3.5

            float4 FragEdgeSeeds(Varyings input) : SV_Target
            {
                float2 uv = input.texcoord;
                float2 texelSize = _BlitTexture_TexelSize.xy;

                float2 rightUv =
                    uv + float2(texelSize.x, 0.0);

                float2 upUv =
                    uv + float2(0.0, texelSize.y);

                float4 current = SampleSource(uv);

                bool currentOriginalSelected =
                    IsOriginalSelected(uv);

                EdgeResult horizontal = EvaluatePair(
                    current,
                    SampleSource(rightUv),
                    float2(1.0, 0.0),
                    currentOriginalSelected,
                    IsOriginalSelected(rightUv)
                );

                EdgeResult vertical = EvaluatePair(
                    current,
                    SampleSource(upUv),
                    float2(0.0, 1.0),
                    currentOriginalSelected,
                    IsOriginalSelected(upUv)
                );

                horizontal.strength *=
                    CalculateDistanceFade(horizontal.depth);

                vertical.strength *=
                    CalculateDistanceFade(vertical.depth);

                return float4(
                    horizontal.position,
                    vertical.position,
                    horizontal.strength,
                    vertical.strength
                );
            }

            ENDHLSL
        }


        Pass
        {
            Name "Reconstruct And Antialias Layer Depth Edges"

            HLSLPROGRAM

            #pragma vertex Vert
            #pragma fragment FragThicken
            #pragma target 3.5

            float4 EvaluateCellCoverage(
                float2 uv,
                float2 cellOffset,
                float2 texelSize,
                float thicknessPixels,
                float featherPixels)
            {
                float2 cellUv =
                    uv + cellOffset * texelSize;

                float4 baseEdge = SampleSource(cellUv);
                float4 upEdge = SampleSource(
                    cellUv + float2(0.0, texelSize.y)
                );
                float4 rightEdge = SampleSource(
                    cellUv + float2(texelSize.x, 0.0)
                );

                float2 points[4];
                float strengths[4];
                int count = 0;

                // Bottom crossing.
                if (baseEdge.b > 0.0)
                {
                    points[count] =
                        cellOffset + float2(baseEdge.r, 0.0);

                    strengths[count] = baseEdge.b;
                    count++;
                }

                // Top crossing.
                if (upEdge.b > 0.0)
                {
                    points[count] =
                        cellOffset + float2(upEdge.r, 1.0);

                    strengths[count] = upEdge.b;
                    count++;
                }

                // Left crossing.
                if (baseEdge.a > 0.0)
                {
                    points[count] =
                        cellOffset + float2(0.0, baseEdge.g);

                    strengths[count] = baseEdge.a;
                    count++;
                }

                // Right crossing.
                if (rightEdge.a > 0.0)
                {
                    points[count] =
                        cellOffset + float2(1.0, rightEdge.g);

                    strengths[count] = rightEdge.a;
                    count++;
                }

                if (count == 0)
                    return 0.0;

                if (count == 1)
                {
                    return PointCoverage4(
                        points[0],
                        strengths[0],
                        thicknessPixels,
                        featherPixels
                    );
                }

                if (count == 2)
                {
                    return SegmentCoverage4(
                        points[0],
                        points[1],
                        min(strengths[0], strengths[1]),
                        thicknessPixels,
                        featherPixels
                    );
                }

                float4 coverage = 0.0;

                if (count == 3)
                {
                    float d01 = distance(points[0], points[1]);
                    float d02 = distance(points[0], points[2]);
                    float d12 = distance(points[1], points[2]);

                    // Drop the longest side and keep the two shorter
                    // segments, producing a continuous local contour.
                    if (d01 >= d02 && d01 >= d12)
                    {
                        coverage = max(
                            SegmentCoverage4(
                                points[0], points[2],
                                min(strengths[0], strengths[2]),
                                thicknessPixels, featherPixels
                            ),
                            SegmentCoverage4(
                                points[1], points[2],
                                min(strengths[1], strengths[2]),
                                thicknessPixels, featherPixels
                            )
                        );
                    }
                    else if (d02 >= d01 && d02 >= d12)
                    {
                        coverage = max(
                            SegmentCoverage4(
                                points[0], points[1],
                                min(strengths[0], strengths[1]),
                                thicknessPixels, featherPixels
                            ),
                            SegmentCoverage4(
                                points[1], points[2],
                                min(strengths[1], strengths[2]),
                                thicknessPixels, featherPixels
                            )
                        );
                    }
                    else
                    {
                        coverage = max(
                            SegmentCoverage4(
                                points[0], points[1],
                                min(strengths[0], strengths[1]),
                                thicknessPixels, featherPixels
                            ),
                            SegmentCoverage4(
                                points[0], points[2],
                                min(strengths[0], strengths[2]),
                                thicknessPixels, featherPixels
                            )
                        );
                    }

                    return coverage;
                }

                float pairing01_23 =
                    distance(points[0], points[1]) +
                    distance(points[2], points[3]);

                float pairing02_13 =
                    distance(points[0], points[2]) +
                    distance(points[1], points[3]);

                float pairing03_12 =
                    distance(points[0], points[3]) +
                    distance(points[1], points[2]);

                if (pairing01_23 <= pairing02_13 &&
                    pairing01_23 <= pairing03_12)
                {
                    coverage = max(
                        SegmentCoverage4(
                            points[0], points[1],
                            min(strengths[0], strengths[1]),
                            thicknessPixels, featherPixels
                        ),
                        SegmentCoverage4(
                            points[2], points[3],
                            min(strengths[2], strengths[3]),
                            thicknessPixels, featherPixels
                        )
                    );
                }
                else if (pairing02_13 <= pairing01_23 &&
                         pairing02_13 <= pairing03_12)
                {
                    coverage = max(
                        SegmentCoverage4(
                            points[0], points[2],
                            min(strengths[0], strengths[2]),
                            thicknessPixels, featherPixels
                        ),
                        SegmentCoverage4(
                            points[1], points[3],
                            min(strengths[1], strengths[3]),
                            thicknessPixels, featherPixels
                        )
                    );
                }
                else
                {
                    coverage = max(
                        SegmentCoverage4(
                            points[0], points[3],
                            min(strengths[0], strengths[3]),
                            thicknessPixels, featherPixels
                        ),
                        SegmentCoverage4(
                            points[1], points[2],
                            min(strengths[1], strengths[2]),
                            thicknessPixels, featherPixels
                        )
                    );
                }

                return coverage;
            }

            float4 FragThicken(Varyings input) : SV_Target
            {
                float2 uv = input.texcoord;
                float2 texelSize = _BlitTexture_TexelSize.xy;

                float resolutionScale = GetResolutionScale();

                float thicknessPixels =
                    max(_LineThickness, 0.0) * resolutionScale;

                float featherPixels =
                    max(_Feather, 0.0) * resolutionScale;

                if (thicknessPixels <= 0.0)
                    return float4(_OutlineColor.rgb, 0.0);

                float maxDistance =
                    thicknessPixels * 0.5 +
                    featherPixels +
                    AA_WIDTH;

                int searchRadius =
                    (int)ceil(maxDistance) + 1;

                float4 subpixelCoverage = 0.0;

                [loop]
                for (int y = -searchRadius; y <= searchRadius; y++)
                {
                    [loop]
                    for (int x = -searchRadius; x <= searchRadius; x++)
                    {
                        subpixelCoverage = max(
                            subpixelCoverage,
                            EvaluateCellCoverage(
                                uv,
                                float2(x, y),
                                texelSize,
                                thicknessPixels,
                                featherPixels
                            )
                        );
                    }
                }

                float coverage =
                    dot(subpixelCoverage, 0.25);

                float alpha =
                    saturate(coverage * _OutlineColor.a);

                return float4(_OutlineColor.rgb, alpha);
            }

            ENDHLSL
        }
    }
}
