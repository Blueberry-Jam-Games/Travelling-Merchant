using Unity.Burst;
using Unity.Mathematics;

[BurstCompile]

public struct PathBoundaryPoint
    {
        public float2 position;
        public float2 perpendicular;

        public PathBoundaryPoint(float2 position, float2 perpendicular)
        {
            this.position = position;
            this.perpendicular = perpendicular;
        }
    }

public static class PathResolver
{
    private struct PathContext {}
    private struct SeedKey {}
    private static readonly SharedStatic<int> Seed = SharedStatic<int>.GetOrCreate<PathContext, SeedKey>();

#region tuning values
    private const float PathFrequency = 0.0001f;
    private const float PathAmplitude = 5000f;

    private const int PathOctaves = 5;
    private const float PathLacunarity = 1.9f;
    private const float PathGain = 0.35f;

    private const float PathOffset = 200;
#endregion

    public static void SetPathData(int value)
    {
        Seed.Data = value;
    }

    /// <summary>
    /// Calculates perpendicular path position U for a given forward position V.
    /// </summary>
    /// <param name="v"></param>Forward position in unity units.
    /// <returns></returns>Perpendicular position in unity units as a float
    [BurstCompile]
    public static float GetUFromV(float v)
    {
        int seed = Seed.Data;

        float frequency = PathFrequency;
        float amplitude = 1f;

        float value = 0f;
        float totalAmplitude = 0f;

        for (int i = 0; i < PathOctaves; i++)
        {
            value += FastNoiseLite.SingleSimplex(seed + i * 1013, v * frequency, 0f) * amplitude;

            totalAmplitude += amplitude;

            frequency *= PathLacunarity;
            amplitude *= PathGain;
        }

        return value / totalAmplitude * PathAmplitude + PathOffset;
    }

    [BurstCompile]
    public static float2 GetDirection(float v)
    {
        const float sampleDistance = 4f;

        float u0 = GetUFromV(v - sampleDistance);
        float u1 = GetUFromV(v + sampleDistance);

        return math.normalizesafe(new float2(sampleDistance * 2f, u1 - u0), new float2(1f, 0f));
    }

    /// <summary>
    /// Finds the first forward coordinate V within [minV, maxV]
    /// where the path reaches the supplied perpendicular coordinate U.
    /// </summary>
    [BurstCompile]
    public static bool TryGetVFromU(
        float u,
        float minV,
        float maxV,
        out float v)
    {
        const int scanSteps = 16;
        const int refinementSteps = 12;
        const float tolerance = 0.01f;

        float previousV = minV;
        float previousDifference = GetUFromV(previousV) - u;

        if (math.abs(previousDifference) <= tolerance)
        {
            v = previousV;
            return true;
        }

        // First find an interval containing a crossing.
        for (int i = 1; i <= scanSteps; i++)
        {
            float t = (float)i / scanSteps;
            float currentV = math.lerp(minV, maxV, t);
            float currentDifference = GetUFromV(currentV) - u;

            if (math.abs(currentDifference) <= tolerance)
            {
                v = currentV;
                return true;
            }

            bool crossed = (previousDifference < 0f && currentDifference > 0f) || (previousDifference > 0f && currentDifference < 0f);

            if (crossed)
            {
                float lowV = previousV;
                float highV = currentV;

                float lowDifference = previousDifference;

                // Refine the intersection with binary search.
                for (int j = 0; j < refinementSteps; j++)
                {
                    float midV = (lowV + highV) * 0.5f;
                    float midDifference = GetUFromV(midV) - u;

                    if (math.abs(midDifference) <= tolerance)
                    {
                        v = midV;
                        return true;
                    }

                    bool sameSide = (lowDifference < 0f && midDifference < 0f) || (lowDifference > 0f && midDifference > 0f);

                    if (sameSide)
                    {
                        lowV = midV;
                        lowDifference = midDifference;
                    }
                    else
                    {
                        highV = midV;
                    }
                }

                v = (lowV + highV) * 0.5f;
                return true;
            }

            previousV = currentV;
            previousDifference = currentDifference;
        }

        v = 0f;
        return false;
    }

    [BurstCompile]
    public static bool TryGetChunkIntersections(float2 chunkMin, float chunkSize, out PathBoundaryPoint entrance, out PathBoundaryPoint exit)
    {
        const int scanSteps = 32;
        const int refinementSteps = 12;

        float minV = chunkMin.x;
        float maxV = chunkMin.x + chunkSize;
        float minU = chunkMin.y;
        float maxU = chunkMin.y + chunkSize;

        entrance = default;
        exit = default;

        bool foundEntrance = false;

        float previousV = minV;
        float previousU = GetUFromV(previousV);
        bool previousInside = previousU >= minU && previousU <= maxU;

        if(previousInside)
        {
            entrance = new PathBoundaryPoint(new float2(minV, previousU), GetDirection(minV));
            foundEntrance = true;
        }

        for(int i = 1; i <= scanSteps; i++)
        {
            float currentV = math.lerp(minV, maxV, (float)i / scanSteps);
            float currentU = GetUFromV(currentV);
            bool currentInside = currentU >= minU && currentU <= maxU;

            if(!previousInside && currentInside && !foundEntrance)
            {
                float boundaryU = previousU < minU ? minU : maxU;
                float lowV = previousV;
                float highV = currentV;
                float lowDifference = GetUFromV(lowV) - boundaryU;

                for(int j = 0; j < refinementSteps; j++)
                {
                    float midV = (lowV + highV) * 0.5f;
                    float midDifference = GetUFromV(midV) - boundaryU;

                    if((lowDifference < 0f && midDifference < 0f) || (lowDifference > 0f && midDifference > 0f))
                    {
                        lowV = midV;
                        lowDifference = midDifference;
                    }
                    else highV = midV;
                }

                float crossingV = (lowV + highV) * 0.5f;
                entrance = new PathBoundaryPoint(new float2(crossingV, boundaryU), GetDirection(crossingV));
                foundEntrance = true;
            }

            if(previousInside && !currentInside && foundEntrance)
            {
                float boundaryU = currentU < minU ? minU : maxU;
                float lowV = previousV;
                float highV = currentV;
                float lowDifference = GetUFromV(lowV) - boundaryU;

                for(int j = 0; j < refinementSteps; j++)
                {
                    float midV = (lowV + highV) * 0.5f;
                    float midDifference = GetUFromV(midV) - boundaryU;

                    if((lowDifference < 0f && midDifference < 0f) || (lowDifference > 0f && midDifference > 0f))
                    {
                        lowV = midV;
                        lowDifference = midDifference;
                    }
                    else highV = midV;
                }

                float crossingV = (lowV + highV) * 0.5f;
                exit = new PathBoundaryPoint(new float2(crossingV, boundaryU), GetDirection(crossingV));
                return true;
            }

            previousV = currentV;
            previousU = currentU;
            previousInside = currentInside;
        }

        if(foundEntrance && previousInside)
        {
            exit = new PathBoundaryPoint(new float2(maxV, previousU), GetDirection(maxV));
            return true;
        }

        return false;
    }
}