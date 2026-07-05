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
    private NativeParallelHashMap<int2, Entity> mediumChunkMap;
    private NativeParallelHashMap<int2, Entity> majorChunkMap;

    private EntityQuery sleepingMajorChunkQuery;

    private EntityQuery sleepingMediumChunkQuery;

    public void OnCreate(ref SystemState state)
    {
        majorChunkMap = new NativeParallelHashMap<int2, Entity>(capacity: 128, Allocator.Persistent);
        mediumChunkMap = new NativeParallelHashMap<int2, Entity>(capacity: 1024, Allocator.Persistent);

        sleepingMajorChunkQuery = SystemAPI.QueryBuilder().
            WithAll<MajorChunk, Chunk>().
            WithDisabled<ChunkActive, ChunkDelayedSleep>().
            Build();

        sleepingMediumChunkQuery = SystemAPI.QueryBuilder().
            WithAll<MediumChunk, Chunk>().
            WithDisabled<ChunkActive, ChunkDelayedSleep>().
            Build();
    }

    public void OnUpdate(ref SystemState state)
    {
        if (!SystemAPI.HasSingleton<ChunkWorldSingleton>()) return;

        ChunkWorldSingleton chunkWorld = SystemAPI.GetSingletonRW<ChunkWorldSingleton>().ValueRO;

        if(chunkWorld.newMajorChunkOccupied)
        {
            foreach((
                RefRO<Chunk> chunk,
                Entity entity
                ) in SystemAPI.Query<
                    RefRO<Chunk>
                    >().WithAll<MajorChunk, ChunkActive>()
                    .WithEntityAccess())
            {
                int2 posDif = chunkWorld.activeMajorChunk - chunk.ValueRO.position.xz;

                int distanceSQR = posDif.x * posDif.x + posDif.y * posDif.y;

                if(distanceSQR > chunkWorld.renderDistanceMajor * chunkWorld.renderDistanceMajor)
                {
                    SystemAPI.SetComponentEnabled<ChunkActive>(entity, false);
                    SystemAPI.SetComponentEnabled<ChunkPopulated>(entity, false);
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
            SleepingMajorChunks.Dispose();
        }

        if(chunkWorld.newMediumChunkOccupied)
        {
            foreach((
                RefRO<Chunk> chunk,
                RefRW<MediumChunk> mediumChunk,
                Entity entity
                ) in SystemAPI.Query<
                    RefRO<Chunk>,
                    RefRW<MediumChunk>
                    >().WithAll<ChunkActive>()
                    .WithEntityAccess())
            {
                int2 posDif = chunkWorld.activeMediumChunk - chunk.ValueRO.position.xz;

                int distanceSQR = posDif.x * posDif.x + posDif.y * posDif.y;

                if(distanceSQR > chunkWorld.renderDistanceMedium * chunkWorld.renderDistanceMedium + 1)
                {
                    SystemAPI.SetComponentEnabled<ChunkActive>(entity, false);
                    SystemAPI.SetComponentEnabled<ChunkPopulated>(entity, false);
                    SystemAPI.SetComponentEnabled<ChunkGeneratedMesh>(entity, false);
                    SystemAPI.SetComponentEnabled<ChunkContainsParentLink>(entity, false);
                    SystemAPI.SetComponentEnabled<ChunkDelayedSleep>(entity, true);
                    mediumChunk.ValueRW.LoD_0 = MediumChunk.MeshTransferState.NOT_READY;
                    mediumChunk.ValueRW.LoD_1 = MediumChunk.MeshTransferState.NOT_READY;
                    mediumChunk.ValueRW.LoD_2 = MediumChunk.MeshTransferState.NOT_READY;
                    mediumChunk.ValueRW.LoD_3 = MediumChunk.MeshTransferState.NOT_READY;
                    mediumChunkMap.Remove(chunk.ValueRO.position.xz);
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
                            SystemAPI.SetComponentEnabled<ChunkActive>(newMediumChunk, true);

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

            SleepingMediumChunks.Dispose();
        }
    }

    public void OnDestroy(ref SystemState state)
    {
        if (mediumChunkMap.IsCreated) mediumChunkMap.Dispose();
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