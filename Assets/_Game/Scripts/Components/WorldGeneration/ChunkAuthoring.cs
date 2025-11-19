using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

class ChunkAuthoring : MonoBehaviour
{
    
}

class ChunkAuthoringBaker : Baker<ChunkAuthoring>
{
    public override void Bake(ChunkAuthoring authoring)
    {
        var entity = GetEntity(TransformUsageFlags.None);
        AddComponent<Chunk>(entity, new Chunk
        {

        });
    }
}

public struct Chunk : IComponentData
{
    public int3 chunkPosition;
    public int meshIndex;
    public int materialIndex;
}

public struct ChunkCollider : IComponentData
{
    public BlobAssetReference<Unity.Physics.Collider> collider;
}