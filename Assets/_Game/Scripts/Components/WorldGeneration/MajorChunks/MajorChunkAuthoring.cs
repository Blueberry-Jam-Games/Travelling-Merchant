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
    FixedList4096Bytes<float> heightmap; // Stores rough vertex heights for minor chunks and structure generation.

    // TODO change 17 to be MinorPerMajorChunk + 1.
    public float this[int x, int z]
    {
        get => heightmap[Index(x, z)];
        set => heightmap[Index(x, z)] = value;
    }

    public float4 SampleQuad(int2 baseCoord)
    {
        int idx00 = Index(baseCoord.x,     baseCoord.y);
        int idx10 = Index(baseCoord.x + 1, baseCoord.y);
        int idx01 = Index(baseCoord.x,     baseCoord.y + 1);
        int idx11 = Index(baseCoord.x + 1, baseCoord.y + 1);

        return new float4(
            heightmap[idx00],
            heightmap[idx10],
            heightmap[idx01],
            heightmap[idx11]
        );
    }

    private int Index(int x, int z)
    {
    #if ENABLE_UNITY_COLLECTIONS_CHECKS
        if(x < 0 || z < 0)
            throw new System.IndexOutOfRangeException(
                    $"Heightmap access out of range (negative): ({x}, {z})");

        if ((uint)x >= 17 || (uint)z >= 17)
            throw new System.IndexOutOfRangeException(
                $"Heightmap access out of range: ({x}, {z})");
    #endif
        return x * 17 + z;
    }
}
