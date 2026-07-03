using Unity.Entities;
using Unity.Burst;
using Unity.Collections;
using UnityEngine.UIElements;
using UnityEngine;
using Unity.Mathematics;

[BurstCompile]
[UpdateInGroup(typeof(WorldGenerationSystemGroup))]
[UpdateAfter(typeof(ChunkMinorMeshSystem))]
partial struct MinorChunkAgregateSystem : ISystem
{
    public void OnUpdate(ref SystemState state)
    {
        ChunkWorldSingleton chunkWorld = SystemAPI.GetSingleton<ChunkWorldSingleton>();
        // 1. For each MediumChunk
        foreach ((
            RefRW<MediumChunk> mediumChunk,
            RefRO<Chunk> chunkPose,
            Entity mediumEntity
            ) in SystemAPI.Query<
                RefRW<MediumChunk>,
                RefRO<Chunk>
                >().WithAll<Chunk, ChunkPopulationComplete>()
                .WithDisabled<ChunkMediumLod0Ready>()
                .WithEntityAccess())
        {
            ECSMesh LOD00Mesh = new ECSMesh(Allocator.Temp);

            bool hasAny = false;
            bool notReady = false;
            // 2. Find all minor chunks belonging to this medium chunk
            foreach ((
                RefRO<MinorChunk> minorChunk,
                RefRO<Chunk> chunk,
                Entity minorEntity
                ) in SystemAPI.Query<
                    RefRO<MinorChunk>,
                    RefRO<Chunk>
                    >().
                    WithPresent<ChunkGenerationComplete>().
                    WithEntityAccess())
            {
                if (minorChunk.ValueRO.mediumChunk != mediumEntity)
                {
                    continue;
                }

                if (!SystemAPI.IsComponentEnabled<ChunkGenerationComplete>(minorEntity))
                {
                    notReady = true;
                    break;
                }

                if(!minorChunk.ValueRO.meshHighDetailValid)
                {
                    continue;
                }

                float3 offset = chunk.ValueRO.position - chunkPose.ValueRO.position * chunkWorld.minorPerMediumChunk;

                LOD00Mesh.Add(minorChunk.ValueRO.meshBlobHighDetail, offset * chunkWorld.minorChunkSize);

                hasAny = true;
            }

            if (!hasAny || notReady)
                continue;

            mediumChunk.ValueRW.LOD0_Mesh = LOD00Mesh.Build(Allocator.Temp);
            LOD00Mesh.Dispose();

            SystemAPI.SetComponentEnabled<ChunkMediumLod0Ready>(mediumEntity, true);
        }
    }
}