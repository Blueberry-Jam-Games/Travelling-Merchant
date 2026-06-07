using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

public struct ChunkT : IComponentData
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

public struct MajorChunkT : IComponentData
{
    
}