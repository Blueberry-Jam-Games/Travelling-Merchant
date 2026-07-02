using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

[UpdateInGroup(typeof(WorldGenerationSystemGroup))]
[UpdateAfter(typeof(ChunkMediumHeightmapLinkSystem))]
partial struct ChunkMediumPopulationSystem : ISystem
{
    EntityQuery sleepingMinorChunkQuery;
    private ComponentLookup<MajorChunk> majorChunkLookup;

    [BurstCompile]
    public void OnCreate(ref SystemState state)
    {
        sleepingMinorChunkQuery = SystemAPI.QueryBuilder().
            WithAll<MinorChunk, Chunk>().
            WithDisabled<ChunkActive>().
            Build();

        majorChunkLookup = state.GetComponentLookup<MajorChunk>(isReadOnly: true);
    }

    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        ChunkWorldSingleton chunkWorld = SystemAPI.GetSingleton<ChunkWorldSingleton>();

        if(!chunkWorld.newMediumChunkOccupied) return;

        NativeArray<Entity> sleepingMinorChunks = sleepingMinorChunkQuery.ToEntityArray(Allocator.Temp);
        int sleepingChunkIndex = 0;

        majorChunkLookup.Update(ref state);

        foreach ((
            RefRW<MediumChunk> mediumChunk,
            RefRO<Chunk> chunk,
            RefRO<ChunkRequestPopulation> update,
            Entity entity
            ) in SystemAPI.Query<
                RefRW<MediumChunk>,
                RefRO<Chunk>,
                RefRO<ChunkRequestPopulation>
                >().
                WithAll<ChunkActive>().
                WithDisabled<ChunkRequestLink>().
                WithEntityAccess())
        {
            int renderDistanceSqr = chunkWorld.renderDistanceMedium * chunkWorld.renderDistanceMedium / 25;

            if(math.distancesq(chunkWorld.activeMediumChunk, chunk.ValueRO.position.xz) > renderDistanceSqr)
            {
                continue;
            }

            if(!SystemAPI.IsComponentEnabled<ChunkGenerationComplete>(mediumChunk.ValueRO.majorChunk))
            {
                continue;
            }

            for(int x = 0; x < chunkWorld.minorPerMediumChunk; x++)
            {
                for(int y = 0; y < chunkWorld.minorPerMediumChunk; y++)
                {
                    int2 mediumChunkPosition = chunk.ValueRO.position.xz;

                    int2 majorOriginMediumSpace = new int2(
                        (int) math.floor((float)mediumChunkPosition.x / chunkWorld.mediumPerMajorChunk) * chunkWorld.mediumPerMajorChunk,
                        (int) math.floor((float)mediumChunkPosition.y / chunkWorld.mediumPerMajorChunk) * chunkWorld.mediumPerMajorChunk
                    );

                    int2 localInMajor = mediumChunkPosition - majorOriginMediumSpace;
                    localInMajor = localInMajor * chunkWorld.minorPerMediumChunk + new int2(x, y);

                    int resolution = mediumChunk.ValueRO.majorChunkHeightmap.Value.resolution;

                    NativeArray<float> heights = new NativeArray<float>(4, Allocator.Temp);
                    heights[0] = mediumChunk.ValueRO.majorChunkHeightmap.Value.GetHeight(localInMajor.x, localInMajor.y);
                    heights[1] = mediumChunk.ValueRO.majorChunkHeightmap.Value.GetHeight(localInMajor.x + 1, localInMajor.y);
                    heights[2] = mediumChunk.ValueRO.majorChunkHeightmap.Value.GetHeight(localInMajor.x, localInMajor.y + 1);
                    heights[3] = mediumChunk.ValueRO.majorChunkHeightmap.Value.GetHeight(localInMajor.x + 1, localInMajor.y + 1);

                    float minHeight = heights[0], maxHeight = heights[0];

                    for(int i = 1; i < 4; i++)
                    {
                        if(heights[i] < minHeight) minHeight = heights[i];

                        if(heights[i] > maxHeight) maxHeight = heights[i];
                    }

                    heights.Dispose();
                    
                    minHeight -= 16;
                    //maxHeight += 16;
                    int minHeightChunkSpace = (int)math.floor(minHeight / chunkWorld.minorChunkSize);
                    int maxHeightChunkSpace = (int)math.floor(maxHeight / chunkWorld.minorChunkSize);

                    int2 baseMinor = chunk.ValueRO.position.xz * chunkWorld.minorPerMediumChunk;

                    int2 chunkPosMinorSpace = baseMinor + new int2(x, y);

                    for (int h = minHeightChunkSpace; h <= maxHeightChunkSpace; h++)
                    {
                        if (sleepingChunkIndex >= sleepingMinorChunks.Length)
                            break;

                        Entity minorChunk = sleepingMinorChunks[sleepingChunkIndex];
                        sleepingChunkIndex ++;

                        int3 gridPos = new int3(chunkPosMinorSpace.x, h, chunkPosMinorSpace.y);

                        SystemAPI.SetComponent(minorChunk, new Chunk
                        {
                            position = gridPos
                        });

                        LocalTransform newTransform = LocalTransform.Identity;
                        newTransform.Position = (float3)gridPos * chunkWorld.minorChunkSize;

                        SystemAPI.SetComponent(minorChunk, newTransform);

                        SystemAPI.SetComponent(minorChunk, new MinorChunk
                        {
                           mediumChunk = entity,
                           majorChunk = mediumChunk.ValueRO.majorChunk 
                        });

                        SystemAPI.SetComponentEnabled<ChunkActive>(minorChunk, true);
                        SystemAPI.SetComponentEnabled<ChunkRequestMesh>(minorChunk, true);
                    }                
                }
            }

            SystemAPI.SetComponentEnabled<ChunkRequestPopulation>(entity, false);
            SystemAPI.SetComponentEnabled<ChunkPopulationComplete>(entity, true);
        }
    }

    [BurstCompile]
    public void OnDestroy(ref SystemState state)
    {
        
    }
}
