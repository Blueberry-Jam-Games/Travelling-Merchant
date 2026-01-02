using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

class MajorChunkAuthoring : MonoBehaviour
{
    
}

class MajorChunkAuthoringBaker : Baker<MajorChunkAuthoring>
{
    public override void Bake(MajorChunkAuthoring authoring)
    {
        var entity = GetEntity(TransformUsageFlags.None);
        AddComponent<MajorChunk>(entity, new MajorChunk
        {

        });
    }
}

public struct MajorChunk : IComponentData
{
    public int2 chunkPosition;
    public int generationStep; // Current step of generation. -1 == released, 2 <= in generation or in use. 
    FixedList512Bytes<float> heightmap; // Stores rough vertex heights for minor chunks and structure generation.

    // TODO change 17 to be MinorPerMajorChunk + 1.
    public float this[int x, int z]
    {
        get => heightmap[x * 17 + z];
        set => heightmap[x * 17 + z] = value;
    }
}
