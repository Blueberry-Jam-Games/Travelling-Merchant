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
    const int HeightmapResolution = 25;
    public int2 chunkPosition;
    public int generationStep; // Current step of generation. -1 == released, 2 <= in generation or in use. 
    public FixedList4096Bytes<float> heightmap; // Stores rough vertex heights for minor chunks and structure generation.

    // TODO change 17 to be MinorPerMajorChunk + 1.
    public float this[int x, int z]
    {
        get => heightmap[Index(x, z)];
        set => heightmap[Index(x, z)] = value;
    }

    public float4 Quad(int2 baseCoord)
    {
        int x0 = Wrap(baseCoord.x,     HeightmapResolution);
        int z0 = Wrap(baseCoord.y,     HeightmapResolution);
        int x1 = Wrap(baseCoord.x + 1, HeightmapResolution);
        int z1 = Wrap(baseCoord.y + 1, HeightmapResolution);

        int idx00 = Index(x0, z0);
        int idx10 = Index(x1, z0);
        int idx01 = Index(x0, z1);
        int idx11 = Index(x1, z1);

        return new float4(
            heightmap[idx00], // (0,0)
            heightmap[idx10], // (1,0)
            heightmap[idx01], // (0,1)
            heightmap[idx11]  // (1,1)
        );
    }

    private static int Wrap(int v, int size)
    {
        v %= size;
        if (v < 0) v += size;
        return v;
    }

    private int Index(int x, int z)
    {
    #if ENABLE_UNITY_COLLECTIONS_CHECKS
        if(x < 0 || z < 0)
            throw new System.IndexOutOfRangeException(
                    $"Heightmap access out of range (negative): ({x}, {z})");

        if ((uint)x >= 25 || (uint)z >= 25)
            throw new System.IndexOutOfRangeException(
                $"Heightmap access out of range: ({x}, {z})");
    #endif
        return z * 25 + x;
    }
}
