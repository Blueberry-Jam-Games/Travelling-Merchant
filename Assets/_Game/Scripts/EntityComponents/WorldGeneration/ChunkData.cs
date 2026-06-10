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