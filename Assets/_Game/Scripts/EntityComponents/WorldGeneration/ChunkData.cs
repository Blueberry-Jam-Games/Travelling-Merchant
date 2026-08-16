using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

public struct Chunk : IComponentData, IEnableableComponent
{
    public int3 position; // Local coordinate space position
    public int distancesqr; // Chunk distance squared to player in local coordinate space
}

public struct MinorChunk : IComponentData
{
    public Entity mediumChunk;
    public Entity majorChunk;

    public bool meshHighDetailValid;
    public bool meshLowDetailValid;

    public BlobAssetReference<MeshBlob> meshBlobHighDetail;
    public BlobAssetReference<MeshBlob> meshBlobLowDetail;
}

public struct MediumChunk : IComponentData
{
    public enum MeshTransferState{
        NOT_READY,
        READY,
        TRANSFERED
    }
    public Entity majorChunk;
    public BlobAssetReference<HeightmapData> majorChunkHeightmap;
    public BlobAssetReference<MeshBlob> LOD0_Mesh;
    public BlobAssetReference<MeshBlob> LOD2_Mesh;
    public BlobAssetReference<MeshBlob> LOD3_Mesh;

    public MeshTransferState LoD_0;
    public MeshTransferState LoD_1;
    public MeshTransferState LoD_2;
    public MeshTransferState LoD_3;
}

public struct MajorChunk : IComponentData
{
    public BlobAssetReference<HeightmapData> heightmapData;
    public UnityObjectRef<Texture2D> renderInformation;
}

public struct HeightmapData
{
    public int resolution;
    public BlobArray<float> heights;
    public BlobArray<float2> gradients;

    public float GetHeight(int x, int y)
    {
        return heights[x + 1 + (y + 1) * resolution];
    }

    public float2 GetGradient(int x, int y)
    {
        return gradients[x + 1 + (y + 1) * resolution];
    }
}

public static class HeightmapDataBlobBuilder
{
    public static BlobAssetReference<HeightmapData> Create(float[] heights, float2[] gradients, int resolution)
    {
        var builder = new BlobBuilder(Allocator.Temp);
        ref HeightmapData root = ref builder.ConstructRoot<HeightmapData>();

        root.resolution = resolution;

        var heightArray = builder.Allocate(ref root.heights, heights.Length);

        for (int i = 0; i < heights.Length; i++) heightArray[i] = heights[i];

        var gradientArray = builder.Allocate(ref root.gradients, gradients.Length);

        for (int i = 0; i < heights.Length; i++) gradientArray[i] = gradients[i];

        var blob = builder.CreateBlobAssetReference<HeightmapData>(Allocator.Persistent);

        builder.Dispose();
        return blob;
    }
}

