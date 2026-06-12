using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

public struct Chunk : IComponentData, IEnableableComponent
{
    public int3 position; // Local coordinate space position
}

public struct MinorChunk : IComponentData
{
    public Entity mediumChunk;
    public Entity majorChunk;

    public BlobAssetReference<MeshBlob> meshBlobHighDetail;
    public BlobAssetReference<MeshBlob> meshBlobLowDetail;
}

public struct MediumChunk : IComponentData
{
    public Entity majorChunk;
    public UnityObjectRef<MediumChunkMirror> mediumChunkMirror;
}

public struct MajorChunk : IComponentData
{
    public BlobAssetReference<HeightmapData> heightmapData;
}

public struct RequestedMinorChunkData : IBufferElementData
{
    public int3 position;
    public bool placed;
}

public struct HeightmapData
{
    public int resolution;
    public BlobArray<float> heights;
    public BlobArray<float2> gradients;
}

public static class HeightmapDataBlobBuilder
{
    public static BlobAssetReference<HeightmapData> Create(float[] heights, float2[] gradients, int resolution)
    {
        var builder = new BlobBuilder(Allocator.Temp);
        ref HeightmapData root = ref builder.ConstructRoot<HeightmapData>();

        root.resolution = resolution;

        var heightArray = builder.Allocate(ref root.heights, heights.Length);

        var gradientArray = builder.Allocate(ref root.gradients, gradients.Length);

        for (int i = 0; i < heights.Length; i++)
            heightArray[i] = heights[i];

        for (int i = 0; i < gradients.Length; i++)
            gradientArray[i] = gradients[i];

        var blob = builder.CreateBlobAssetReference<HeightmapData>(
            Allocator.Persistent);

        builder.Dispose();
        return blob;
    }
}
