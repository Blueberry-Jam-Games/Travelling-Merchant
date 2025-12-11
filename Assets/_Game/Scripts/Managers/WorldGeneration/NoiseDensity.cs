using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class NoiseDensity : DensityGenerator {

    [Header ("Noise")]
    public int seed;
    public int numOctaves = 4;
    public float lacunarity = 2;
    public float persistence = .5f;
    public float noiseScale = 1;
    public float noiseWeight = 1;
    public bool closeEdges;
    public float floorOffset = 10;
    public float weightMultiplier = 1;

    public float hardFloorHeight;
    public float hardFloorWeight;

    public Vector4 shaderParams;

    public override ComputeBuffer Generate (ComputeBuffer pointsBuffer, int numPointsPerAxis, float boundsSize, Vector3 centre, Vector4 offset, float spacing) {
        shaderParams = new Vector4(1, 0, 0, 0);
        int densityKernal = densityShader.FindKernel("Density");
        buffersToRelease = new List<ComputeBuffer> ();

        // Noise parameters
        var prng = new System.Random (seed);
        var offsets = new Vector4[numOctaves];
        float offsetRange = 1000;
        for (int i = 0; i < numOctaves; i++) {
            offsets[i] = new Vector4 ((float) prng.NextDouble () * 2 - 1, (float) prng.NextDouble () * 2 - 1, (float) prng.NextDouble () * 2 - 1) * offsetRange;
        }

        var offsetsBuffer = new ComputeBuffer (offsets.Length, sizeof (float) * 4);
        offsetsBuffer.SetData (offsets);
        buffersToRelease.Add (offsetsBuffer);

        densityShader.SetVector ("centre", new Vector4 (centre.x, centre.y, centre.z));
        densityShader.SetInt ("octaves", Mathf.Max (1, numOctaves));
        densityShader.SetFloat ("lacunarity", lacunarity);
        densityShader.SetFloat ("persistence", persistence);
        densityShader.SetFloat ("noiseScale", noiseScale);
        densityShader.SetFloat ("noiseWeight", noiseWeight);
        densityShader.SetInt ("closeEdges", closeEdges ? 1 : 0);
        densityShader.SetBuffer (densityKernal, "offsets", offsetsBuffer);
        densityShader.SetFloat ("floorOffset", floorOffset);
        densityShader.SetFloat ("weightMultiplier", weightMultiplier);
        densityShader.SetFloat ("hardFloor", hardFloorHeight);
        densityShader.SetFloat ("hardFloorWeight", hardFloorWeight);

        densityShader.SetVector("densityParams", shaderParams);

        return base.Generate(pointsBuffer, numPointsPerAxis, boundsSize, centre, offset, spacing);
    }
}