using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using Unity.Collections;
using UnityEngine;


[UpdateInGroup(typeof(WorldGenerationSystemGroup))]
[UpdateAfter(typeof(CharacterChunkTrackingSystem))]
partial struct ChunkPlacementSystem : ISystem
{
    private NativeParallelHashMap<int3, Entity> minorChunkMap;
    private NativeParallelHashMap<int2, Entity> mediumChunkMap;
    private NativeParallelHashMap<int2, Entity> majorChunkMap;

    private EntityQuery sleepingMajorChunkQuery;

    private EntityQuery sleepingMediumChunkQuery;

    public void OnCreate(ref SystemState state)
    {
        minorChunkMap = new NativeParallelHashMap<int3, Entity>(capacity: 1024, Allocator.Persistent);
        majorChunkMap = new NativeParallelHashMap<int2, Entity>(capacity: 128, Allocator.Persistent);
        mediumChunkMap = new NativeParallelHashMap<int2, Entity>(capacity: 1024, Allocator.Persistent);

        sleepingMajorChunkQuery = SystemAPI.QueryBuilder().
            WithAll<MajorChunk, Chunk>().
            WithDisabled<ChunkActive>().
            Build();

        sleepingMediumChunkQuery = SystemAPI.QueryBuilder().
            WithAll<MediumChunk, Chunk>().
            WithDisabled<ChunkActive>().
            Build();
    }

    public void OnUpdate(ref SystemState state)
    {
        if (!SystemAPI.HasSingleton<ChunkWorldSingleton>()) return;

        EntityManager entityManager = state.EntityManager;

        ChunkWorldSingleton chunkWorld = SystemAPI.GetSingletonRW<ChunkWorldSingleton>().ValueRO;

        if(chunkWorld.newMajorChunkOccupied)
        {
            foreach((
                RefRW<MajorChunk> majorChunk,
                RefRO<Chunk> chunk,
                RefRO<ChunkActive> ChunkActive,
                Entity entity
                ) in SystemAPI.Query<
                    RefRW<MajorChunk>,
                    RefRO<Chunk>,
                    RefRO<ChunkActive>
                    >().WithEntityAccess())
            {
                int2 posDif = chunkWorld.activeMajorChunk - chunk.ValueRO.position.xz;

                int distanceSQR = posDif.x * posDif.x + posDif.y * posDif.y;

                if(distanceSQR > chunkWorld.renderDistanceMajor * chunkWorld.renderDistanceMajor)
                {
                    SystemAPI.SetComponentEnabled<ChunkActive>(entity, false);
                    SystemAPI.SetComponentEnabled<ChunkGenerationComplete>(entity, false);
                    SystemAPI.SetComponentEnabled<ChunkRequestPopulation>(entity, false);
                    majorChunkMap.Remove(chunk.ValueRO.position.xz);
                }
            }

            NativeArray<Entity> SleepingMajorChunks = sleepingMajorChunkQuery.ToEntityArray(Allocator.Temp);
            int sleepingChunkIndex = 0;

            for(int rX = 1; rX <= chunkWorld.renderDistanceMajor * 2 + 1; rX++)
            {
                int x = rX / 2;

                if(rX % 2 == 1)
                {
                    x *= -1;
                }

                int yBound = (int)math.floor(math.sqrt((chunkWorld.renderDistanceMajor * chunkWorld.renderDistanceMajor) - (x * x)));

                for(int rY = 1; rY <= yBound * 2 + 1; rY ++)
                {
                    int y = rY / 2;

                    if(rY % 2 == 1)
                    {
                        y *= -1;
                    }

                    int2 checkPos = new int2(x, y) + chunkWorld.activeMajorChunk;

                    if(!majorChunkMap.ContainsKey(checkPos))
                    {
                        // activate sleeping chunk
                        if(sleepingChunkIndex < SleepingMajorChunks.Length)
                        {
                            Entity newMajorChunk = SleepingMajorChunks[sleepingChunkIndex];
                            majorChunkMap.Add(checkPos, newMajorChunk);

                            SystemAPI.SetComponent(newMajorChunk, new Chunk
                            {
                                position = new int3(checkPos.x, 0, checkPos.y)
                            });
                            SystemAPI.SetComponentEnabled<ChunkRequestPopulation>(newMajorChunk, true);
                            SystemAPI.SetComponentEnabled<ChunkActive>(newMajorChunk, true);

                            SystemAPI.SetComponent(newMajorChunk, new LocalTransform
                            {
                                Position = new float3(checkPos.x, 0, checkPos.y) * chunkWorld.majorChunkSize
                            });

                            sleepingChunkIndex ++;
                        }
                        else
                        {
                            Debug.LogWarning("Out of Free Major Chunks");
                        }
                    }

                }
            }
        }

        if(chunkWorld.newMediumChunkOccupied)
        {
            foreach((
                RefRW<MediumChunk> mediumChunk,
                RefRO<Chunk> chunk,
                RefRO<ChunkActive> ChunkActive,
                Entity entity
                ) in SystemAPI.Query<
                    RefRW<MediumChunk>,
                    RefRO<Chunk>,
                    RefRO<ChunkActive>
                    >().WithEntityAccess())
            {
                // TODO move to new system
                int2 posDif = chunkWorld.activeMediumChunk - chunk.ValueRO.position.xz;

                int distanceSQR = posDif.x * posDif.x + posDif.y * posDif.y;

                if(distanceSQR > chunkWorld.renderDistanceMedium * chunkWorld.renderDistanceMedium + 1)
                {
                    SystemAPI.SetComponentEnabled<ChunkActive>(entity, false);
                    SystemAPI.SetComponentEnabled<ChunkGenerationComplete>(entity, false);
                    SystemAPI.SetComponentEnabled<ChunkRequestPopulation>(entity, false);
                    SystemAPI.SetComponentEnabled<ChunkRequestMesh>(entity, false);
                    mediumChunkMap.Remove(chunk.ValueRO.position.xz);
                    // TOOD Add other reset stuff. Lots for medium chunks.
                }
            }

            NativeArray<Entity> SleepingMediumChunks = sleepingMediumChunkQuery.ToEntityArray(Allocator.Temp);
            int sleepingChunkIndex = 0;

            for(int rX = 1; rX <= chunkWorld.renderDistanceMedium * 2 + 1; rX++)
            {
                int x = rX / 2;

                if(rX % 2 == 1)
                {
                    x *= -1;
                }

                int yBound = (int)math.floor(math.sqrt((chunkWorld.renderDistanceMedium * chunkWorld.renderDistanceMedium) - (x * x)));

                for(int rY = 1; rY <= yBound * 2 + 1; rY ++)
                {
                    int y = rY / 2;

                    if(rY % 2 == 1)
                    {
                        y *= -1;
                    }

                    int2 checkPos = new int2(x, y) + chunkWorld.activeMediumChunk;
                    int2 checkMajorChunkPos = new int2(
                        (int)math.floor((float)checkPos.x / chunkWorld.mediumPerMajorChunk),
                        (int)math.floor((float)checkPos.y / chunkWorld.mediumPerMajorChunk)
                    );

                    if(!mediumChunkMap.ContainsKey(checkPos) && majorChunkMap.ContainsKey(checkMajorChunkPos))
                    {
                        // activate sleeping chunk
                        if(sleepingChunkIndex < SleepingMediumChunks.Length)
                        {
                            Entity majorChunkInRegion;
                            majorChunkMap.TryGetValue(checkMajorChunkPos, out majorChunkInRegion);
                            Entity newMediumChunk = SleepingMediumChunks[sleepingChunkIndex];
                            mediumChunkMap.Add(checkPos, newMediumChunk);

                            SystemAPI.SetComponent(newMediumChunk, new Chunk
                            {
                                position = new int3(checkPos.x, 0, checkPos.y)
                            });
                            SystemAPI.SetComponent(newMediumChunk, new MediumChunk
                            {
                                majorChunk = majorChunkInRegion,
                                //majorChunkHeightmap = SystemAPI.GetComponent<MajorChunk>(majorChunkInRegion).heightmapData
                            });
                            SystemAPI.SetComponentEnabled<ChunkRequestPopulation>(newMediumChunk, true);
                            SystemAPI.SetComponentEnabled<ChunkRequestMesh>(newMediumChunk, true);
                            SystemAPI.SetComponentEnabled<ChunkActive>(newMediumChunk, true);
                            SystemAPI.SetComponentEnabled<ChunkRequestLink>(newMediumChunk, true);

                            SystemAPI.SetComponent(newMediumChunk, new LocalTransform
                            {
                                Position = new float3(checkPos.x, 0, checkPos.y) * chunkWorld.mediumChunkSize
                            });

                            sleepingChunkIndex ++;
                        }
                        else
                        {
                            Debug.LogWarning("Out of Free Medium Chunks");
                        }
                    }

                }
            }
        }
    }

    public void OnDestroy(ref SystemState state)
    {
        if (minorChunkMap.IsCreated) minorChunkMap.Dispose();
        if (majorChunkMap.IsCreated) majorChunkMap.Dispose();
    }

    public int flatDistance(int2 start, int2 end)
    {
        int2 result = math.abs(start) - math.abs(end);
        result = math.abs(result);

        if(result.x > result.y)
        {
            return result.x;
        }
        else
        {
            return result.y;
        }
    }
}