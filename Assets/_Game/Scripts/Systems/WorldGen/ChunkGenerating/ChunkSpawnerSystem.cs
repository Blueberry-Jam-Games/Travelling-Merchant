using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using Unity.Collections;
using Unity.Physics;

[UpdateBefore(typeof(CharacterControllerSystem))]
partial struct ChunkSpawnerSystem : ISystem
{
    EntityArchetype minorChunkArchtype;
    EntityArchetype majorChunkArchtype;

    public void OnCreate(ref SystemState state)
    {
        minorChunkArchtype = state.EntityManager.CreateArchetype(
            typeof(LocalTransform),
            typeof(Chunk),
            typeof(ChunkSleeping),
            typeof(ChunkRequestUpdate),
            typeof(ChunkRequestPhysics),
            typeof(PhysicsCollider),
            typeof(PhysicsWorldIndex),
            typeof(EntityMeshRenderer)
        );

        majorChunkArchtype = state.EntityManager.CreateArchetype(
            typeof(LocalTransform),
            typeof(MajorChunk),
            typeof(ChunkSleeping),
            typeof(ChunkRequestUpdate),
            typeof(MajorChunkReady),
            typeof(EntityMeshRenderer)
        );
    }

    public void OnUpdate(ref SystemState state)
    {
        if (!SystemAPI.HasSingleton<ChunkWorldSingleton>()) return;

        EntityManager entityManager = state.EntityManager;

        ChunkWorldSingleton chunkWorld = SystemAPI.GetSingleton<ChunkWorldSingleton>();

        // Major Chunk Creation
        if (chunkWorld.pooledMajorChunkCount > chunkWorld.MajorChunkCurrentCount)
        {
            NativeArray<Entity> entities = new NativeArray<Entity>(chunkWorld.pooledMajorChunkCount - chunkWorld.MajorChunkCurrentCount, Allocator.Temp);

            entityManager.CreateEntity(majorChunkArchtype, entities);

            for (int i = 0; i < entities.Length; i++)
            {
                Entity currentEntity = entities[i];

                entityManager.SetComponentData(currentEntity, LocalTransform.Identity);
                entityManager.SetComponentData(currentEntity, new MajorChunk
                {
                    generationStep = -1,
                    chunkPosition = new int2(5, 5)
                });
                entityManager.SetName(currentEntity, "MajorChunk " + chunkWorld.MajorChunkCurrentCount);
                entityManager.SetComponentEnabled<ChunkSleeping>(currentEntity, true);
                entityManager.SetComponentEnabled<ChunkRequestUpdate>(currentEntity, false);
                entityManager.SetComponentEnabled<MajorChunkReady>(currentEntity, false);
                entityManager.SetComponentEnabled<EntityMeshRenderer>(currentEntity, false);

                chunkWorld.MajorChunkCurrentCount++;
            }

            SystemAPI.SetSingleton(chunkWorld);

            entities.Dispose();
        }


        // Minor Chunk Creation
        if (chunkWorld.pooledChunkCount > chunkWorld.currentChunkCount)
        {
            NativeArray<Entity> entities = new NativeArray<Entity>(chunkWorld.pooledChunkCount - chunkWorld.currentChunkCount, Allocator.Temp);

            entityManager.CreateEntity(minorChunkArchtype, entities);

            for (int i = 0; i < entities.Length; i++)
            {
                Entity currentEntity = entities[i];

                entityManager.SetComponentData(currentEntity, LocalTransform.Identity);
                entityManager.SetComponentEnabled<ChunkSleeping>(currentEntity, true);
                entityManager.SetComponentEnabled<ChunkRequestUpdate>(currentEntity, false);
                entityManager.SetComponentEnabled<EntityMeshRenderer>(currentEntity, false);
                entityManager.SetComponentEnabled<ChunkRequestPhysics>(currentEntity, false);
                entityManager.SetComponentData(currentEntity, new Chunk
                {
                    chunkPosition = new int3(100, 100, 100),

                    meshIndex = ChunkMeshManagerSystem.Instance.CreateEmptyMesh()
                });

                entityManager.SetName(currentEntity, "MinorChunk " + chunkWorld.currentChunkCount);

                chunkWorld.currentChunkCount++;
            }

            SystemAPI.SetSingleton(chunkWorld);

            entities.Dispose();
        }
    }
}