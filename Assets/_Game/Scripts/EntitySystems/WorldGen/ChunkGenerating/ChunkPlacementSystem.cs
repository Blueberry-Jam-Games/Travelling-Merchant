using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using Unity.Collections;
using UnityEngine;


[UpdateInGroup(typeof(WorldGenerationSystemGroup))]
partial struct ChunkPlacementSystem : ISystem
{
    private NativeParallelHashMap<int3, Entity> minorChunkMap;
    private NativeParallelHashMap<int2, Entity> mediumChunkMap;
    private NativeParallelHashMap<int2, Entity> majorChunkMap;

    private EntityQuery activeMajorChunkQuery;
    private EntityQuery sleepingMajorChunkQuery;

    private EntityQuery activeMediumChunkQuery;
    private EntityQuery sleepingMediumChunkQuery;

    public void OnCreate(ref SystemState state)
    {
        minorChunkMap = new NativeParallelHashMap<int3, Entity>(capacity: 1024, Allocator.Persistent);
        majorChunkMap = new NativeParallelHashMap<int2, Entity>(capacity: 128, Allocator.Persistent);
        mediumChunkMap = new NativeParallelHashMap<int2, Entity>(capacity: 1024, Allocator.Persistent);

        activeMajorChunkQuery = SystemAPI.QueryBuilder().
            WithAll<MajorChunk, ChunkActive>().
            Build();

        sleepingMajorChunkQuery = SystemAPI.QueryBuilder().
            WithAll<MajorChunk>().
            WithDisabled<ChunkActive>().
            Build();

        activeMediumChunkQuery = SystemAPI.QueryBuilder().
            WithAll<MediumChunk, ChunkActive>().
            Build();

        sleepingMediumChunkQuery = SystemAPI.QueryBuilder().
            WithAll<MediumChunk>().
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
                    SystemAPI.SetComponentEnabled<ChunkRequestUpdate>(entity, false);
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
                            SystemAPI.SetComponentEnabled<ChunkRequestUpdate>(newMajorChunk, true);
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
                    SystemAPI.SetComponentEnabled<ChunkRequestUpdate>(entity, false);
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
                                majorChunk = majorChunkInRegion
                            });
                            SystemAPI.SetComponentEnabled<ChunkRequestUpdate>(newMediumChunk, true);
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
        }

        // if(chunkWorld.newMajorChunk)
        // {
        //     int2 majorChunkLowerBound = chunkWorld.activeMajorChunk - chunkWorld.majorChunkRenderDistance;
        //     int2 majorChunkUpperBound = chunkWorld.activeMajorChunk + chunkWorld.majorChunkRenderDistance;

        //     NativeList<SleepingChunk> sleepingReleasableMajorChunk = new NativeList<SleepingChunk>(Allocator.Temp);

        //     foreach((
        //         RefRW<MajorChunk> majorChunk,
        //         Entity entity
        //     ) in SystemAPI.Query<
        //         RefRW<MajorChunk>>().WithEntityAccess())
        //     {
        //         int chunkDistance = flatDistance(majorChunk.ValueRO.chunkPosition, chunkWorld.activeMajorChunk);
        //         if(chunkDistance > chunkWorld.majorChunkRenderDistance)
        //         {
        //             SystemAPI.SetComponentEnabled<ChunkSleeping>(entity, true);
        //             SystemAPI.SetComponentEnabled<MajorChunkReady>(entity, false);
        //             sleepingReleasableMajorChunk.Add(new SleepingChunk { entity = entity, distSq = chunkDistance });
        //         }
        //         else if(chunkDistance == chunkWorld.majorChunkRenderDistance)
        //         {
        //             SystemAPI.SetComponentEnabled<ChunkSleeping>(entity, true);
        //         }
        //         else if(majorChunk.ValueRO.generationStep == -1)
        //         {
        //             SystemAPI.SetComponentEnabled<ChunkSleeping>(entity, true);
        //             SystemAPI.SetComponentEnabled<MajorChunkReady>(entity, false);
        //             sleepingReleasableMajorChunk.Add(new SleepingChunk { entity = entity, distSq = 10000 });
        //         }
        //         else
        //         {
        //             continue;
        //         }
        //     }

        //     sleepingReleasableMajorChunk.Sort();
        //     int sleepingMajorChunkFromArray = 0;
        //     for (int x = majorChunkLowerBound.x + 1; x < majorChunkUpperBound.x; x ++)
        //     {
        //         for (int z = majorChunkLowerBound.y + 1; z < majorChunkUpperBound.y; z ++)
        //         {
        //             if (!majorChunkMap.ContainsKey(new int2(x, z)))
        //             {
        //                 if(sleepingReleasableMajorChunk.Length == 0 || sleepingReleasableMajorChunk.Length <= sleepingMajorChunkFromArray)
        //                 {
        //                     if(sleepingReleasableMajorChunk.Length == 0)
        //                     {
        //                         Debug.Log("No chunks");
        //                     }
        //                     else
        //                     {
        //                         Debug.Log("Loaded to many chunks");
        //                     }
        //                     continue;
        //                 }

        //                 Entity outChunk = sleepingReleasableMajorChunk[sleepingMajorChunkFromArray].entity;

        //                 MajorChunk chunk = SystemAPI.GetComponent<MajorChunk>(outChunk);
        //                 LocalTransform transform = SystemAPI.GetComponent<LocalTransform>(outChunk);

        //                 // Remove previous chunk
        //                 majorChunkMap.Remove(chunk.chunkPosition);

        //                 // Create new position data for the chunk
        //                 int2 position = new int2(x, z);
        //                 chunk.chunkPosition = position;
        //                 chunk.generationStep = 0;

        //                 int3 worldPos = new int3(x, 0, z);
        //                 worldPos *= chunkWorld.majorChunkSize;
        //                 transform.Position = worldPos;

        //                 // Save data to chunk
        //                 majorChunkMap.Add(position, outChunk);

        //                 SystemAPI.SetComponentEnabled<ChunkSleeping>(outChunk, false);
        //                 SystemAPI.SetComponentEnabled<ChunkRequestUpdate>(outChunk, true);
        //                 SystemAPI.SetComponentEnabled<MajorChunkReady>(outChunk, false);

        //                 SystemAPI.SetComponent(outChunk, chunk);
        //                 SystemAPI.SetComponent(outChunk, transform);

        //                 sleepingMajorChunkFromArray++;
        //             }
        //             else
        //             {
        //                 majorChunkMap.TryGetValue(new int2(x, z), out Entity chunk);
        //                 if (SystemAPI.IsComponentEnabled<ChunkSleeping>(chunk))
        //                 {
        //                     SystemAPI.SetComponentEnabled<ChunkSleeping>(chunk, false);
        //                 }
        //             }
        //         }
        //     }
        // }

        // // Minor Chunks
        // if (chunkWorld.newMinorChunk)
        // {
        //     int3 lowerBound = new int3(chunkWorld.activeMinorChunk.x - chunkWorld.renderDistance, chunkWorld.activeMinorChunk.y - 3, chunkWorld.activeMinorChunk.z - chunkWorld.renderDistance);
        //     int3 upperBound = new int3(chunkWorld.activeMinorChunk.x + chunkWorld.renderDistance, chunkWorld.activeMinorChunk.y + 3, chunkWorld.activeMinorChunk.z + chunkWorld.renderDistance);


        //     // Chunk marked for sleep outside of bounds
        //     NativeList<SleepingChunk> sleepingChunks = new NativeList<SleepingChunk>(Allocator.Temp);
        //     foreach ((
        //         RefRW<Chunk> chunk,
        //         Entity entity
        //         ) in SystemAPI.Query<
        //         RefRW<Chunk>
        //         >().WithEntityAccess())
        //     {
        //         if (SystemAPI.IsComponentEnabled<ChunkSleeping>(entity))
        //         {
        //             float2 chunkXZ = chunk.ValueRO.chunkPosition.xz;

        //             float distSq = math.distancesq(chunkXZ, chunkWorld.activeMinorChunk.xz);

        //             sleepingChunks.Add(new SleepingChunk { entity = entity, distSq = distSq });
        //         }
        //         else
        //         {
        //             int3 tempChunkPosition = chunk.ValueRO.chunkPosition;

        //             // Check if the chunk is out of bounds and if so, disable
        //             if (math.any(tempChunkPosition < lowerBound) || math.any(tempChunkPosition > upperBound))
        //             {
        //                 SystemAPI.SetComponentEnabled<ChunkSleeping>(entity, true);

        //                 float distSq = math.distancesq(chunk.ValueRO.chunkPosition, chunkWorld.activeMinorChunk);
        //                 sleepingChunks.Add(new SleepingChunk { entity = entity, distSq = distSq });
        //             }
        //         }
        //     }

        //     sleepingChunks.Sort();

        //     int sleepingChunkFromArray = 0;
        //     // Add chunks as necessary
        //     for (int x = lowerBound.x; x <= upperBound.x; x++)
        //     {
        //         for (int z = lowerBound.z; z <= upperBound.z; z++)
        //         {
        //             for (int y = lowerBound.y; y <= upperBound.y; y++)
        //             {
        //                 int2 majorPosition = new int2((int)math.floor(x / chunkWorld.minorPerMajorChunk),
        //                                               (int)math.floor(z / chunkWorld.minorPerMajorChunk));

        //                 if (!minorChunkMap.ContainsKey(new int3(x, y, z)))
        //                 {
        //                     if(majorChunkMap.ContainsKey(majorPosition))
        //                     {
        //                         Entity outChunk = sleepingChunks[sleepingChunkFromArray].entity;
        //                         int3 position = new int3(x, y, z);
        //                         Chunk chunk = SystemAPI.GetComponent<Chunk>(outChunk);
        //                         int3 chunkOldPosition = chunk.chunkPosition;
        //                         LocalTransform transform = SystemAPI.GetComponent<LocalTransform>(outChunk);
        //                         chunk.chunkPosition = position;
                                
        //                         majorChunkMap.TryGetValue(majorPosition, out chunk.majorChunk);
        //                         chunk.majorChunkPosition = majorPosition;
        //                         transform.Position = position * chunkWorld.chunkSize;

        //                         minorChunkMap.Remove(chunkOldPosition);
        //                         minorChunkMap.Add(position, outChunk);

        //                         SystemAPI.SetComponentEnabled<ChunkSleeping>(outChunk, false);
        //                         SystemAPI.SetComponentEnabled<ChunkRequestUpdate>(outChunk, true);

        //                         SystemAPI.SetComponent(outChunk, chunk);
        //                         SystemAPI.SetComponent(outChunk, transform);

        //                         sleepingChunkFromArray++;
        //                     }
        //                 }
        //                 else
        //                 {
        //                     minorChunkMap.TryGetValue(new int3(x, y, z), out Entity chunk);
        //                     if (SystemAPI.IsComponentEnabled<ChunkSleeping>(chunk))
        //                     {
        //                         SystemAPI.SetComponentEnabled<ChunkSleeping>(chunk, false);
        //                     }
        //                 }
        //             }
        //         }
        //     }
        // }
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

    // Sample function if needed for later. Will be used for external systems to gather heightmap data. Read only
    public Entity getMajorChunkEntity(int2 position)
    {
        majorChunkMap.TryGetValue(position, out Entity entity);
        return entity;
    }

    public bool majorChunkExists(int2 position)
    {
        return majorChunkMap.ContainsKey(position);
    }

}

public struct SleepingChunk : System.IComparable<SleepingChunk>
{
    public Entity entity;
    public float distSq;

    // Furthest first
    public int CompareTo(SleepingChunk other)
    {
        return other.distSq.CompareTo(distSq);
    }
}