using Unity.Entities;
using Unity.Burst;
using Unity.Collections;

[BurstCompile]
[UpdateInGroup(typeof(WorldGenerationSystemGroup))]
[UpdateAfter(typeof(ChunkMinorMeshSystem))]
partial struct MinorChunkAgregateSystem : ISystem
{
    public void OnUpdate(ref SystemState state)
    {
        // 1. For each MediumChunk
        foreach ((
            RefRW<MediumChunk> mediumChunk,
            Entity entity
            ) in SystemAPI.Query<
                RefRW<MediumChunk>
                >().WithAll<Chunk, ChunkPopulationComplete>()
                .WithEntityAccess())
        {
            bool hasAny = false;
            bool notReady = false;
            // 2. Find all minor chunks belonging to this medium chunk
            foreach ((
                RefRO<MinorChunk> minorChunk,
                Entity minorEntity
                ) in SystemAPI.Query<
                    RefRO<MinorChunk>
                    >().WithEntityAccess())
            {
                if (minorChunk.ValueRO.mediumChunk != entity)
                    continue;

                if (!SystemAPI.IsComponentEnabled<ChunkGenerationComplete>(minorEntity))
                {
                    notReady = true;
                    break;
                }

                hasAny = true;
            }

            if (!hasAny || notReady)
                continue;

            // 3. Build merged mesh
            //medium.ValueRW.LOD2_Mesh // TODO

            // 4. Mark ready for transfer system
        }
    }
}