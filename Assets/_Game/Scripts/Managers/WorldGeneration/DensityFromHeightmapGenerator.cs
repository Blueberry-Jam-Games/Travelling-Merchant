using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class DensityFromHeightmapGenerator {

    public DensityFromHeightmapGenerator(ComputeShader baseShader)
    {
        densityShader = baseShader;
    }
    const int threadGroupSize = 8;
    public ComputeShader densityShader;

    public ComputeBuffer Generate (ref ComputeBuffer heightmapBuffer, ComputeBuffer pointsBuffer, int numPointsPerAxis, float boundsSize, Vector3 centre, float spacing, int seed) {
        int densityKernal = densityShader.FindKernel("Density");
        int numPoints = numPointsPerAxis * numPointsPerAxis * numPointsPerAxis;
        int numThreadsPerAxis = Mathf.CeilToInt (numPointsPerAxis / (float) threadGroupSize);
        // Points buffer is populated inside shader with pos (xyz) + density (w).
        // Set paramaters
        densityShader.SetBuffer (densityKernal, "points", pointsBuffer);
        densityShader.SetBuffer(densityKernal, "HeightmapBuffer", heightmapBuffer);
        densityShader.SetInt ("numPointsPerAxis", numPointsPerAxis);
        densityShader.SetFloat ("boundsSize", boundsSize);
        densityShader.SetVector ("centre", new Vector4 (centre.x, centre.y, centre.z, 0));
        densityShader.SetFloat("spacing", spacing);
        densityShader.SetInt("seed", seed);

        // Dispatch shader
        densityShader.Dispatch (densityKernal, numThreadsPerAxis, numThreadsPerAxis, numThreadsPerAxis);        

        // Return voxel data buffer so it can be used to generate mesh
        return pointsBuffer;
    }
}