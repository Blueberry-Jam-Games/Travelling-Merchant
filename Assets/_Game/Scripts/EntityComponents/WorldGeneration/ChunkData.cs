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
    public BlobAssetReference<HeightmapData> majorChunkHeightmap;
    public UnityObjectRef<MediumChunkMirror> mediumChunkMirror;
}

public struct MajorChunk : IComponentData
{
    public BlobAssetReference<HeightmapData> heightmapData;
}

public struct HeightmapData
{
    public int resolution;
    public BlobArray<float> heights;
}

public static class HeightmapDataBlobBuilder
{
    public static BlobAssetReference<HeightmapData> Create(float[] heights, int resolution)
    {
        var builder = new BlobBuilder(Allocator.Temp);
        ref HeightmapData root = ref builder.ConstructRoot<HeightmapData>();

        root.resolution = resolution;

        var heightArray = builder.Allocate(ref root.heights, heights.Length);

        for (int i = 0; i < heights.Length; i++)
            heightArray[i] = heights[i];

        var blob = builder.CreateBlobAssetReference<HeightmapData>(
            Allocator.Persistent);

        builder.Dispose();
        return blob;
    }
}
