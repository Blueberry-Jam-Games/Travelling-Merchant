using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public abstract class DensityGenerator {

    const int threadGroupSize = 8;
    public ComputeShader densityShader;

    protected List<ComputeBuffer> buffersToRelease;

    public virtual ComputeBuffer Generate (ComputeBuffer pointsBuffer, int numPointsPerAxis, float boundsSize, Vector3 centre, Vector4 offset, float spacing) {
        int densityKernal = densityShader.FindKernel("Density");
        int numPoints = numPointsPerAxis * numPointsPerAxis * numPointsPerAxis;
        int numThreadsPerAxis = Mathf.CeilToInt (numPointsPerAxis / (float) threadGroupSize);
        // Points buffer is populated inside shader with pos (xyz) + density (w).
        // Set paramaters
        densityShader.SetBuffer (densityKernal, "points", pointsBuffer);
        densityShader.SetInt ("numPointsPerAxis", numPointsPerAxis);
        densityShader.SetFloat ("boundsSize", boundsSize);
        densityShader.SetVector ("centre", new Vector4 (centre.x, centre.y, centre.z));
        densityShader.SetVector ("offset", new Vector4 (offset.x, offset.y, offset.z));
        densityShader.SetFloat("spacing", spacing);

        // Dispatch shader
        densityShader.Dispatch (densityKernal, numThreadsPerAxis, numThreadsPerAxis, numThreadsPerAxis);

        if (buffersToRelease != null)
        {
            foreach (var b in buffersToRelease)
            {
                b.Release();
            }
        }
        

        // Return voxel data buffer so it can be used to generate mesh
        return pointsBuffer;
    }
}