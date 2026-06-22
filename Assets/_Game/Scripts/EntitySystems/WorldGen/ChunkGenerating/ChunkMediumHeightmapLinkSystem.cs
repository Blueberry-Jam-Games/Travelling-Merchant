using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

[UpdateInGroup(typeof(WorldGenerationSystemGroup))]
[UpdateAfter(typeof(ChunkMajorPopulationSystem))]
partial struct ChunkMediumHeightmapLinkSystem : ISystem
{
    private ComponentLookup<MajorChunk> majorChunkLookup;

    [BurstCompile]
    public void OnCreate(ref SystemState state)
    {
        majorChunkLookup = state.GetComponentLookup<MajorChunk>(isReadOnly: true);
    }

    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        ChunkWorldSingleton chunkWorld = SystemAPI.GetSingleton<ChunkWorldSingleton>();

        majorChunkLookup.Update(ref state);

        foreach ((
            RefRW<MediumChunk> mediumChunk,
            Entity entity
            ) in SystemAPI.Query<
                RefRW<MediumChunk>
                >().
                WithAll<Chunk, ChunkActive, ChunkRequestLink>().
                WithEntityAccess())
        {
            Entity majorEntity = mediumChunk.ValueRO.majorChunk;

            if (!majorChunkLookup.HasComponent(majorEntity))
                continue;

            MajorChunk major = majorChunkLookup[majorEntity];

            if(!major.heightmapData.IsCreated) continue;

            mediumChunk.ValueRW.majorChunkHeightmap = major.heightmapData;

            SystemAPI.SetComponentEnabled<ChunkRequestLink>(entity, false);
        }
    }

    [BurstCompile]
    public void OnDestroy(ref SystemState state)
    {
        
    }
}
