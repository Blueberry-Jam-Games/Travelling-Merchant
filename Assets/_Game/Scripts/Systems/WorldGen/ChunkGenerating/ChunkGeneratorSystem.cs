using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using Unity.Collections;
using Unity.Physics;
using System.Runtime.CompilerServices;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;


[UpdateAfter(typeof(ChunkSpawnerSystem))]
partial struct ChunkGeneratorSystem : ISystem
{
    private NativeParallelHashMap<int3, Entity> minorChunkMap;
    private NativeParallelHashMap<int2, Entity> majorChunkMap;

    public void OnCreate(ref SystemState state)
    {
        minorChunkMap = new NativeParallelHashMap<int3, Entity>(capacity: 1024, Allocator.Persistent);
        majorChunkMap = new NativeParallelHashMap<int2, Entity>(capacity: 128, Allocator.Persistent);
    }

    public void OnUpdate(ref SystemState state)
    {
        if (!SystemAPI.HasSingleton<ChunkWorldSingleton>()) return;

        EntityManager entityManager = state.EntityManager;

        ref ChunkWorldSingleton chunkWorld = ref SystemAPI.GetSingletonRW<ChunkWorldSingleton>().ValueRW;
        Entity player = SystemAPI.GetSingletonEntity<Player>();
        LocalTransform playerTransform = SystemAPI.GetComponent<LocalTransform>(player);


        int2 playerMajorChunk = new int2(
            (int)math.floor(playerTransform.Position.x / chunkWorld.MajorChunkSize),
            (int)math.floor(playerTransform.Position.z / chunkWorld.MajorChunkSize)
        );
        
        if(math.any(playerMajorChunk != chunkWorld.activeMajorChunk))
        {
            chunkWorld.activeMajorChunk = playerMajorChunk;

            //FixedList128Bytes<Entity> sleepingChunks = new FixedList128Bytes<Entity>();
            //NativeHashSet<int2> missingMajorChunks = new NativeHashSet<int2>(256, Allocator.Temp);

            int2 majorChunkLowerBound = playerMajorChunk - chunkWorld.majorChunkRenderDistance;
            int2 majorChunkUpperBound = playerMajorChunk + chunkWorld.majorChunkRenderDistance;
            NativeList<SleepingChunk> sleepingReleasableMajorChunk = new NativeList<SleepingChunk>(Allocator.Temp);

            foreach((
                RefRW<MajorChunk> majorChunk,
                Entity entity
            ) in SystemAPI.Query<
                RefRW<MajorChunk>>().WithEntityAccess())
            {
                int chunkDistance = flatDistance(majorChunk.ValueRO.chunkPosition, chunkWorld.activeMajorChunk);
                if(chunkDistance > chunkWorld.majorChunkRenderDistance)
                {
                    SystemAPI.SetComponentEnabled<ChunkSleeping>(entity, true);
                    sleepingReleasableMajorChunk.Add(new SleepingChunk { entity = entity, distSq = chunkDistance });
                }
                else if(chunkDistance >= chunkWorld.majorChunkRenderDistance)
                {
                    SystemAPI.SetComponentEnabled<ChunkSleeping>(entity, true);
                }
                else if(majorChunk.ValueRO.generationStep == -1)
                {
                    SystemAPI.SetComponentEnabled<ChunkSleeping>(entity, true);
                    sleepingReleasableMajorChunk.Add(new SleepingChunk { entity = entity, distSq = 10000 });
                }
                else
                {
                    continue;
                }
            }

            sleepingReleasableMajorChunk.Sort();
            int sleepingMajorChunkFromArray = 0;
            for (int x = majorChunkLowerBound.x + 1; x < majorChunkUpperBound.x; x ++)
            {
                for (int z = majorChunkLowerBound.y + 1; z < majorChunkUpperBound.y; z ++)
                {
                    if (!majorChunkMap.ContainsKey(new int2(x, z)))
                    {
                        if(sleepingReleasableMajorChunk.Length == 0 || sleepingReleasableMajorChunk.Length <= sleepingMajorChunkFromArray)
                        {
                            continue;
                        }

                        Entity outChunk = sleepingReleasableMajorChunk[sleepingMajorChunkFromArray].entity;
                        int2 position = new int2(x, z);
                        MajorChunk chunk = SystemAPI.GetComponent<MajorChunk>(outChunk);
                        int2 chunkOldPosition = chunk.chunkPosition;
                        LocalTransform transform = SystemAPI.GetComponent<LocalTransform>(outChunk);
                        chunk.chunkPosition = position;
                        int3 worldPos = new int3(position.x, 0, position.y);
                        worldPos *= chunkWorld.MajorChunkSize;
                        transform.Position = worldPos;

                        majorChunkMap.Remove(chunkOldPosition);
                        majorChunkMap.Add(position, outChunk);

                        SystemAPI.SetComponentEnabled<ChunkSleeping>(outChunk, false);

                        SystemAPI.SetComponent(outChunk, chunk);
                        SystemAPI.SetComponent(outChunk, transform);

                        sleepingMajorChunkFromArray++;
                    }
                    else
                    {
                        majorChunkMap.TryGetValue(new int2(x, z), out Entity chunk);
                        if (SystemAPI.IsComponentEnabled<ChunkSleeping>(chunk))
                        {
                            SystemAPI.SetComponentEnabled<ChunkSleeping>(chunk, false);
                        }
                    }
                }
            }
        }

        // Minor Chunk Placement
        int3 playerMinorChunk = new int3(
            (int)math.floor(playerTransform.Position.x / chunkWorld.chunkSize),
            (int)math.floor(playerTransform.Position.y / chunkWorld.chunkSize),
            (int)math.floor(playerTransform.Position.z / chunkWorld.chunkSize)
        );

        if (math.any(playerMinorChunk != chunkWorld.activeChunk))
        {
            chunkWorld.activeChunk = playerMinorChunk;

            int3 lowerBound = new int3(playerMinorChunk.x - chunkWorld.renderDistance, playerMinorChunk.y - 3, playerMinorChunk.z - chunkWorld.renderDistance);
            int3 upperBound = new int3(playerMinorChunk.x + chunkWorld.renderDistance, playerMinorChunk.y + 3, playerMinorChunk.z + chunkWorld.renderDistance);


            // Chunk marked for sleep outside of bounds
            NativeList<SleepingChunk> sleepingChunks = new NativeList<SleepingChunk>(Allocator.Temp);
            foreach ((
                RefRW<Chunk> chunk,
                Entity entity
                ) in SystemAPI.Query<
                RefRW<Chunk>
                >().WithEntityAccess())
            {
                if (SystemAPI.IsComponentEnabled<ChunkSleeping>(entity))
                {
                    float2 chunkXZ = chunk.ValueRO.chunkPosition.xz;
                    float2 playerXZ = playerTransform.Position.xz;

                    float distSq = math.distancesq(chunkXZ, playerXZ);

                    sleepingChunks.Add(new SleepingChunk { entity = entity, distSq = distSq });
                }
                else
                {
                    int3 tempChunkPosition = chunk.ValueRO.chunkPosition;

                    // Check if the chunk is out of bounds and if so, disable
                    if (math.any(tempChunkPosition < lowerBound) || math.any(tempChunkPosition > upperBound))
                    {
                        SystemAPI.SetComponentEnabled<ChunkSleeping>(entity, true);

                        float distSq = math.distancesq(chunk.ValueRO.chunkPosition, playerMinorChunk);
                        sleepingChunks.Add(new SleepingChunk { entity = entity, distSq = distSq });
                    }
                }
            }

            sleepingChunks.Sort();

            int sleepingChunkFromArray = 0;
            // Add chunks as necessary
            for (int x = lowerBound.x; x <= upperBound.x; x++)
            {
                for (int z = lowerBound.z; z <= upperBound.z; z++)
                {
                    for (int y = lowerBound.y; y <= upperBound.y; y++)
                    {
                        if (!minorChunkMap.ContainsKey(new int3(x, y, z)))
                        {
                            Entity outChunk = sleepingChunks[sleepingChunkFromArray].entity;
                            int3 position = new int3(x, y, z);
                            Chunk chunk = SystemAPI.GetComponent<Chunk>(outChunk);
                            int3 chunkOldPosition = chunk.chunkPosition;
                            LocalTransform transform = SystemAPI.GetComponent<LocalTransform>(outChunk);
                            chunk.chunkPosition = position;
                            transform.Position = position * chunkWorld.chunkSize;

                            minorChunkMap.Remove(chunkOldPosition);
                            minorChunkMap.Add(position, outChunk);

                            SystemAPI.SetComponentEnabled<ChunkSleeping>(outChunk, false);
                            SystemAPI.SetComponentEnabled<ChunkRequestUpdate>(outChunk, true);

                            SystemAPI.SetComponent(outChunk, chunk);
                            SystemAPI.SetComponent(outChunk, transform);

                            sleepingChunkFromArray++;
                        }
                        else
                        {
                            minorChunkMap.TryGetValue(new int3(x, y, z), out Entity chunk);
                            if (SystemAPI.IsComponentEnabled<ChunkSleeping>(chunk))
                            {
                                SystemAPI.SetComponentEnabled<ChunkSleeping>(chunk, false);
                            }

                        }
                    }
                }
            }
        }
    }

    public void OnDestroy(ref SystemState state)
    {
        if (minorChunkMap.IsCreated) minorChunkMap.Dispose();
    }

    public int flatDistance(int2 start, int2 end)
    {
        int2 result = start - end;
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