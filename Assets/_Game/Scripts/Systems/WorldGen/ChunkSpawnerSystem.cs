using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using Unity.Collections;
using Unity.Physics;

[UpdateBefore(typeof(CharacterControllerSystem))]
partial struct ChunkSpawnerSystem : ISystem
{
    private NativeParallelHashMap<int3, Entity> chunkMap;

    EntityArchetype terrainChunkArchtype;

    public void OnCreate(ref SystemState state)
    {
        chunkMap = new NativeParallelHashMap<int3, Entity>(capacity: 1024, Allocator.Persistent);

        terrainChunkArchtype = state.EntityManager.CreateArchetype(
                typeof(LocalTransform),
                typeof(Chunk),
                typeof(ChunkSleeping),
                typeof(ChunkRequestUpdate),
                typeof(PhysicsCollider),
                typeof(PhysicsWorldIndex)
        );
    }

    public void OnUpdate(ref SystemState state)
    {
        if (!SystemAPI.HasSingleton<ChunkWorldSingleton>()) return;

        EntityManager entityManager = state.EntityManager;

        ChunkWorldSingleton chunkWorld = SystemAPI.GetSingleton<ChunkWorldSingleton>();
        Entity player = SystemAPI.GetSingletonEntity<Player>();
        LocalTransform playerTransform = SystemAPI.GetComponent<LocalTransform>(player);

        if (chunkWorld.pooledChunkCount > chunkWorld.currentChunkCount)
        {
            NativeArray<Entity> entities = new NativeArray<Entity>(chunkWorld.pooledChunkCount - chunkWorld.currentChunkCount, Allocator.Temp);

            entityManager.CreateEntity(terrainChunkArchtype, entities);

            for (int i = 0; i < entities.Length; i++)
            {
                Entity currentEntity = entities[i];

                entityManager.SetComponentData(currentEntity, LocalTransform.Identity);
                entityManager.SetComponentEnabled<ChunkSleeping>(currentEntity, true);
                entityManager.SetComponentEnabled<ChunkRequestUpdate>(currentEntity, false);
                entityManager.SetComponentData(currentEntity, new Chunk
                {
                    chunkPosition = new int3(100, 100, 100),

                    meshIndex = ChunkMeshManagerSystem.Instance.CreateEmptyMesh()
                });

                chunkWorld.currentChunkCount++;
            }

            SystemAPI.SetSingleton(chunkWorld);

            entities.Dispose();
        }

        // Chunk Mover
        int3 playerChunk = new int3(
            (int)math.floor(playerTransform.Position.x / chunkWorld.chunkSize),
            (int)math.floor(playerTransform.Position.y / chunkWorld.chunkSize),
            (int)math.floor(playerTransform.Position.z / chunkWorld.chunkSize)
        );

        if (math.any(playerChunk != chunkWorld.activeChunk))
        {
            chunkWorld.activeChunk = playerChunk;

            int3 lowerBound = new int3(playerChunk.x - chunkWorld.renderDistance, playerChunk.y - 3, playerChunk.z - chunkWorld.renderDistance);
            int3 upperBound = new int3(playerChunk.x + chunkWorld.renderDistance, playerChunk.y + 3, playerChunk.z + chunkWorld.renderDistance);


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
                        int3 chunkToRemove = chunk.ValueRO.chunkPosition;

                        float distSq = math.distancesq(chunk.ValueRO.chunkPosition, playerChunk);
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
                        if (!chunkMap.ContainsKey(new int3(x, y, z)))
                        {
                            Entity outChunk = sleepingChunks[sleepingChunkFromArray].entity;
                            int3 position = new int3(x, y, z);
                            Chunk chunk = SystemAPI.GetComponent<Chunk>(outChunk);
                            int3 chunkOldPosition = chunk.chunkPosition;
                            LocalTransform transform = SystemAPI.GetComponent<LocalTransform>(outChunk);
                            chunk.chunkPosition = position;
                            transform.Position = position * chunkWorld.chunkSize;

                            chunkMap.Remove(chunkOldPosition);
                            chunkMap.Add(position, outChunk);

                            SystemAPI.SetComponentEnabled<ChunkSleeping>(outChunk, false);
                            SystemAPI.SetComponentEnabled<ChunkRequestUpdate>(outChunk, true);

                            SystemAPI.SetComponent(outChunk, chunk);
                            SystemAPI.SetComponent(outChunk, transform);

                            sleepingChunkFromArray++;
                        }
                        else
                        {
                            chunkMap.TryGetValue(new int3(x, y, z), out Entity chunk);
                            if (SystemAPI.IsComponentEnabled<ChunkSleeping>(chunk))
                            {
                                SystemAPI.SetComponentEnabled<ChunkSleeping>(chunk, false);
                            }

                        }
                    }
                }
            }

            SystemAPI.SetSingleton(chunkWorld);
        }


    }
    
    public void OnDestroy(ref SystemState state)
    {
        if (chunkMap.IsCreated) chunkMap.Dispose();
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