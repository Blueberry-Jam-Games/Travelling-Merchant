using Unity.Mathematics;
using UnityEngine;

public class HeightmapGenerator
{
    public HeightmapGenerator(ComputeShader baseShader)
    {
        heightmapShader = baseShader;
    }
    const int threadGroupSize = 8;
    public ComputeShader heightmapShader;

    public void Generate(ref ComputeBuffer heightmap, float4 roughCorners, float4 xgradients, float4 ygradients, int numPointsPerAxis, float dimensions, int3 cornerPosition, int seed, float scale)
    {
        int kernel = heightmapShader.FindKernel("Heightmap");
        heightmapShader.SetBuffer(kernel, "HeightmapBuffer", heightmap);
        heightmapShader.SetInts("chunkPosition", cornerPosition.x, cornerPosition.y, cornerPosition.z, 0);
        heightmapShader.SetInt("numPointsPerAxis", numPointsPerAxis);
        heightmapShader.SetFloat("chunkSize", dimensions);
        heightmapShader.SetFloat("scale", scale);
        heightmapShader.SetFloat("worldHeight", 10);
        heightmapShader.SetInt("seed", seed);
        heightmapShader.SetVector("corners", roughCorners);
        heightmapShader.SetVector("xgradients", xgradients);
        heightmapShader.SetVector("ygradients", ygradients);

        int numThreadsPerAxis = Mathf.CeilToInt (numPointsPerAxis / (float) threadGroupSize);

        heightmapShader.Dispatch(kernel, numThreadsPerAxis, numThreadsPerAxis, 1);
    }
}

