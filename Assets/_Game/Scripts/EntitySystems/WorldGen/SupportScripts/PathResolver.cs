using Unity.Burst;
using Unity.Mathematics;

[BurstCompile]
public static class PathResolver
{
    private struct PathContext {}
    private struct SeedKey {}
    private static readonly SharedStatic<int> Seed = SharedStatic<int>.GetOrCreate<PathContext, SeedKey>();

#region tuning values
    private const float PathFrequency = 0.00025f;
    private const float PathAmplitude = 2000f;

    private const int PathOctaves = 4;
    private const float PathLacunarity = 1.9f;
    private const float PathGain = 0.35f;
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

        return value / totalAmplitude * PathAmplitude;
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
}