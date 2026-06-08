// using Unity.Burst;
// using Unity.Entities;
// using Unity.Mathematics;
// using Unity.Transforms;
// using Unity.Collections;
// using UnityEngine;


// [UpdateInGroup(typeof(WorldGenerationSystemGroup))]
// partial struct FlatChunkPlacementSystem : ISystem
// {
//     private NativeParallelHashMap<int3, Entity> minorChunkMap;
//     private NativeParallelHashMap<int2, Entity> mediumChunkMap;
//     private NativeParallelHashMap<int2, Entity> majorChunkMap;

//     public void OnCreate(ref SystemState state)
//     {
//         minorChunkMap = new NativeParallelHashMap<int3, Entity>(capacity: 1024, Allocator.Persistent);
//         majorChunkMap = new NativeParallelHashMap<int2, Entity>(capacity: 128, Allocator.Persistent);
//         mediumChunkMap = new NativeParallelHashMap<int2, Entity>(capacity: 1024, Allocator.Persistent);
//     }

//     public void OnUpdate(ref SystemState state)
//     {
//         if (!SystemAPI.HasSingleton<ChunkWorldSingleton>()) return;

//         EntityManager entityManager = state.EntityManager;

//         ChunkWorldSingleton chunkWorld = SystemAPI.GetSingletonRW<ChunkWorldSingleton>().ValueRO;

//         if(chunkWorld.newMajorChunk)
//         {
//             int2 majorChunkLowerBound = chunkWorld.activeMajorChunk - chunkWorld.majorChunkRenderDistance;
//             int2 majorChunkUpperBound = chunkWorld.activeMajorChunk + chunkWorld.majorChunkRenderDistance;

//             NativeList<SleepingChunk> sleepingReleasableMajorChunk = new NativeList<SleepingChunk>(Allocator.Temp);

//             foreach((
//                 RefRW<MajorChunk> majorChunk,
//                 Entity entity
//             ) in SystemAPI.Query<
//                 RefRW<MajorChunk>>().WithEntityAccess())
//             {
//                 int chunkDistance = flatDistance(majorChunk.ValueRO.chunkPosition, chunkWorld.activeMajorChunk);
//                 if(chunkDistance > chunkWorld.majorChunkRenderDistance)
//                 {
//                     SystemAPI.SetComponentEnabled<ChunkSleeping>(entity, true);
//                     SystemAPI.SetComponentEnabled<MajorChunkReady>(entity, false);
//                     sleepingReleasableMajorChunk.Add(new SleepingChunk { entity = entity, distSq = chunkDistance });
//                 }
//                 else if(chunkDistance == chunkWorld.majorChunkRenderDistance)
//                 {
//                     SystemAPI.SetComponentEnabled<ChunkSleeping>(entity, true);
//                 }
//                 else if(majorChunk.ValueRO.generationStep == -1)
//                 {
//                     SystemAPI.SetComponentEnabled<ChunkSleeping>(entity, true);
//                     SystemAPI.SetComponentEnabled<MajorChunkReady>(entity, false);
//                     sleepingReleasableMajorChunk.Add(new SleepingChunk { entity = entity, distSq = 10000 });
//                 }
//                 else
//                 {
//                     continue;
//                 }
//             }

//             sleepingReleasableMajorChunk.Sort();
//             int sleepingMajorChunkFromArray = 0;
//             for (int x = majorChunkLowerBound.x + 1; x < majorChunkUpperBound.x; x ++)
//             {
//                 for (int z = majorChunkLowerBound.y + 1; z < majorChunkUpperBound.y; z ++)
//                 {
//                     if (!majorChunkMap.ContainsKey(new int2(x, z)))
//                     {
//                         if(sleepingReleasableMajorChunk.Length == 0 || sleepingReleasableMajorChunk.Length <= sleepingMajorChunkFromArray)
//                         {
//                             if(sleepingReleasableMajorChunk.Length == 0)
//                             {
//                                 Debug.Log("No chunks");
//                             }
//                             else
//                             {
//                                 Debug.Log("Loaded to many chunks");
//                             }
//                             continue;
//                         }

//                         Entity outChunk = sleepingReleasableMajorChunk[sleepingMajorChunkFromArray].entity;

//                         MajorChunk chunk = SystemAPI.GetComponent<MajorChunk>(outChunk);
//                         LocalTransform transform = SystemAPI.GetComponent<LocalTransform>(outChunk);

//                         // Remove previous chunk
//                         majorChunkMap.Remove(chunk.chunkPosition);

//                         // Create new position data for the chunk
//                         int2 position = new int2(x, z);
//                         chunk.chunkPosition = position;
//                         chunk.generationStep = 0;

//                         int3 worldPos = new int3(x, 0, z);
//                         worldPos *= chunkWorld.MajorChunkSize;
//                         transform.Position = worldPos;

//                         // Save data to chunk
//                         majorChunkMap.Add(position, outChunk);

//                         SystemAPI.SetComponentEnabled<ChunkSleeping>(outChunk, false);
//                         SystemAPI.SetComponentEnabled<ChunkRequestUpdate>(outChunk, true);
//                         SystemAPI.SetComponentEnabled<MajorChunkReady>(outChunk, false);

//                         SystemAPI.SetComponent(outChunk, chunk);
//                         SystemAPI.SetComponent(outChunk, transform);

//                         sleepingMajorChunkFromArray++;
//                     }
//                     else
//                     {
//                         majorChunkMap.TryGetValue(new int2(x, z), out Entity chunk);
//                         if (SystemAPI.IsComponentEnabled<ChunkSleeping>(chunk))
//                         {
//                             SystemAPI.SetComponentEnabled<ChunkSleeping>(chunk, false);
//                         }
//                     }
//                 }
//             }
//         }

//         // Minor Chunks
//         if (chunkWorld.newMinorChunk)
//         {
//             int3 lowerBound = new int3(chunkWorld.activeMinorChunk.x - chunkWorld.renderDistance, chunkWorld.activeMinorChunk.y - 3, chunkWorld.activeMinorChunk.z - chunkWorld.renderDistance);
//             int3 upperBound = new int3(chunkWorld.activeMinorChunk.x + chunkWorld.renderDistance, chunkWorld.activeMinorChunk.y + 3, chunkWorld.activeMinorChunk.z + chunkWorld.renderDistance);


//             // Chunk marked for sleep outside of bounds
//             NativeList<SleepingChunk> sleepingChunks = new NativeList<SleepingChunk>(Allocator.Temp);
//             foreach ((
//                 RefRW<Chunk> chunk,
//                 Entity entity
//                 ) in SystemAPI.Query<
//                 RefRW<Chunk>
//                 >().WithEntityAccess())
//             {
//                 if (SystemAPI.IsComponentEnabled<ChunkSleeping>(entity))
//                 {
//                     float2 chunkXZ = chunk.ValueRO.chunkPosition.xz;

//                     float distSq = math.distancesq(chunkXZ, chunkWorld.activeMinorChunk.xz);

//                     sleepingChunks.Add(new SleepingChunk { entity = entity, distSq = distSq });
//                 }
//                 else
//                 {
//                     int3 tempChunkPosition = chunk.ValueRO.chunkPosition;

//                     // Check if the chunk is out of bounds and if so, disable
//                     if (math.any(tempChunkPosition < lowerBound) || math.any(tempChunkPosition > upperBound))
//                     {
//                         SystemAPI.SetComponentEnabled<ChunkSleeping>(entity, true);

//                         float distSq = math.distancesq(chunk.ValueRO.chunkPosition, chunkWorld.activeMinorChunk);
//                         sleepingChunks.Add(new SleepingChunk { entity = entity, distSq = distSq });
//                     }
//                 }
//             }

//             sleepingChunks.Sort();

//             int sleepingChunkFromArray = 0;
//             // Add chunks as necessary
//             for (int x = lowerBound.x; x <= upperBound.x; x++)
//             {
//                 for (int z = lowerBound.z; z <= upperBound.z; z++)
//                 {
//                     for (int y = lowerBound.y; y <= upperBound.y; y++)
//                     {
//                         int2 majorPosition = new int2((int)math.floor(x / chunkWorld.MinorPerMajorChunk),
//                                                       (int)math.floor(z / chunkWorld.MinorPerMajorChunk));

//                         if (!minorChunkMap.ContainsKey(new int3(x, y, z)))
//                         {
//                             if(majorChunkMap.ContainsKey(majorPosition))
//                             {
//                                 Entity outChunk = sleepingChunks[sleepingChunkFromArray].entity;
//                                 int3 position = new int3(x, y, z);
//                                 Chunk chunk = SystemAPI.GetComponent<Chunk>(outChunk);
//                                 int3 chunkOldPosition = chunk.chunkPosition;
//                                 LocalTransform transform = SystemAPI.GetComponent<LocalTransform>(outChunk);
//                                 chunk.chunkPosition = position;
                                
//                                 majorChunkMap.TryGetValue(majorPosition, out chunk.majorChunk);
//                                 chunk.majorChunkPosition = majorPosition;
//                                 transform.Position = position * chunkWorld.chunkSize;

//                                 minorChunkMap.Remove(chunkOldPosition);
//                                 minorChunkMap.Add(position, outChunk);

//                                 SystemAPI.SetComponentEnabled<ChunkSleeping>(outChunk, false);
//                                 SystemAPI.SetComponentEnabled<ChunkRequestUpdate>(outChunk, true);

//                                 SystemAPI.SetComponent(outChunk, chunk);
//                                 SystemAPI.SetComponent(outChunk, transform);

//                                 sleepingChunkFromArray++;
//                             }
//                         }
//                         else
//                         {
//                             minorChunkMap.TryGetValue(new int3(x, y, z), out Entity chunk);
//                             if (SystemAPI.IsComponentEnabled<ChunkSleeping>(chunk))
//                             {
//                                 SystemAPI.SetComponentEnabled<ChunkSleeping>(chunk, false);
//                             }
//                         }
//                     }
//                 }
//             }
//         }
//     }

//     public void OnDestroy(ref SystemState state)
//     {
//         if (minorChunkMap.IsCreated) minorChunkMap.Dispose();
//         if (majorChunkMap.IsCreated) majorChunkMap.Dispose();
//     }

//     public int flatDistance(int2 start, int2 end)
//     {
//         int2 result = math.abs(start) - math.abs(end);
//         result = math.abs(result);

//         if(result.x > result.y)
//         {
//             return result.x;
//         }
//         else
//         {
//             return result.y;
//         }
//     }

//     // Sample function if needed for later. Will be used for external systems to gather heightmap data. Read only
//     public Entity getMajorChunkEntity(int2 position)
//     {
//         majorChunkMap.TryGetValue(position, out Entity entity);
//         return entity;
//     }

//     public bool majorChunkExists(int2 position)
//     {
//         return majorChunkMap.ContainsKey(position);
//     }

// }

// public struct SleepingChunk : System.IComparable<SleepingChunk>
// {
//     public Entity entity;
//     public float distSq;

//     // Furthest first
//     public int CompareTo(SleepingChunk other)
//     {
//         return other.distSq.CompareTo(distSq);
//     }
// }