using Unity.Mathematics;
using UnityEngine;

public class HeightmapGenerator
{
    public HeightmapGenerator(ComputeShader baseShader)
    {
        densityShader = baseShader;
    }
    const int threadGroupSize = 8;
    public ComputeShader densityShader;

    public void Generate(ref ComputeBuffer heightmap, float4 roughCorners, int numPointsPerAxis, float dimensions, int3 cornerPosition, int seed, float scale)
    {
        int kernel = densityShader.FindKernel("Heightmap");
        densityShader.SetBuffer(kernel, "HeightmapBuffer", heightmap);
        densityShader.SetInts("chunkPosition", cornerPosition.x, cornerPosition.y, cornerPosition.z, 0);
        densityShader.SetInt("numPointsPerAxis", numPointsPerAxis);
        densityShader.SetFloat("chunkSize", dimensions);
        densityShader.SetFloat("scale", scale);
        densityShader.SetFloat("worldHeight", 10);
        densityShader.SetInt("seed", seed);
        densityShader.SetVector("corners", roughCorners);

        int numThreadsPerAxis = Mathf.CeilToInt (numPointsPerAxis / (float) threadGroupSize);

        densityShader.Dispatch(kernel, numThreadsPerAxis, numThreadsPerAxis, 1);
    }
}
