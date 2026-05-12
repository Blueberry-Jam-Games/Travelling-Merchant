using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

class MinorChunkAuthoring : MonoBehaviour
{
    
}

class MinorChunkAuthoringBaker : Baker<MinorChunkAuthoring>
{
    public override void Bake(MinorChunkAuthoring authoring)
    {
        var entity = GetEntity(TransformUsageFlags.None);
        AddComponent<Chunk>(entity, new Chunk
        {

        });
    }
}

public struct Chunk : IComponentData
{
    public int2 majorChunkPosition;
    public Entity majorChunk;
    public int3 chunkPosition;
    public int meshIndex;
    public int materialIndex;
    public BlobAssetReference<MeshBlob> mesh;
}