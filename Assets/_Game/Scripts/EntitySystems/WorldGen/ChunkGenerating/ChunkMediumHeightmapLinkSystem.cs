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
        majorChunkLookup.Update(ref state);

        foreach ((
            RefRW<MediumChunk> mediumChunk,
            Entity entity
            ) in SystemAPI.Query<
                RefRW<MediumChunk>
                >()
                .WithAll<Chunk, ChunkActive>()
                .WithDisabled<ChunkContainsParentLink>()
                .WithEntityAccess())
        {
            Entity majorEntity = mediumChunk.ValueRO.majorChunk;

            if (!majorChunkLookup.HasComponent(majorEntity))
                continue;

            if(!SystemAPI.IsComponentEnabled<ChunkPopulated>(majorEntity)) continue;

            MajorChunk majorChunk = majorChunkLookup[majorEntity];

            if(!majorChunk.heightmapData.IsCreated) continue;

            mediumChunk.ValueRW.majorChunkHeightmap = majorChunk.heightmapData;

            SystemAPI.SetComponentEnabled<ChunkContainsParentLink>(entity, true);
        }
    }

    [BurstCompile]
    public void OnDestroy(ref SystemState state)
    {
        
    }
}
