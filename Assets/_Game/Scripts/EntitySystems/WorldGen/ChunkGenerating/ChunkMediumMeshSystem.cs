using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

[UpdateInGroup(typeof(WorldGenerationSystemGroup))]
[UpdateAfter(typeof(ChunkMediumPopulationSystem))]
partial struct ChunkMediumMeshSystem : ISystem
{
    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        ChunkWorldSingleton chunkWorld = SystemAPI.GetSingleton<ChunkWorldSingleton>();

        foreach ((
            RefRW<MediumChunk> mediumChunk,
            RefRO<Chunk> chunk,
            Entity entity
            ) in SystemAPI.Query<
                RefRW<MediumChunk>,
                RefRO<Chunk>
                >().
                WithAll<ChunkRequestMesh, ChunkActive>().
                WithDisabled<ChunkGenerationComplete, ChunkRequestLink>().
                WithEntityAccess())
        {
            int scanRegion = chunkWorld.minorPerMediumChunk + 3;
            int faceRegion = chunkWorld.minorPerMediumChunk + 2;

            NativeArray<float3> vertexes = new NativeArray<float3>(scanRegion * scanRegion, Allocator.Temp);
            NativeArray<int> indexes = new NativeArray<int>(faceRegion * faceRegion * 6, Allocator.Temp);

            for (int y = 0; y < scanRegion; y++)
            {
                for (int x = 0; x < scanRegion; x++)
                {
                    int hx = x - 1;
                    int hy = y - 1;

                    int2 mediumChunkPosition = chunk.ValueRO.position.xz;

                    int2 majorOriginMediumSpace = new int2(
                        (int) math.floor((float)mediumChunkPosition.x / chunkWorld.mediumPerMajorChunk) * chunkWorld.mediumPerMajorChunk,
                        (int) math.floor((float)mediumChunkPosition.y / chunkWorld.mediumPerMajorChunk) * chunkWorld.mediumPerMajorChunk
                    );

                    int2 localInMajor = mediumChunkPosition - majorOriginMediumSpace;
                    localInMajor = localInMajor * chunkWorld.minorPerMediumChunk + new int2(x, y);

                    float3 pos = new float3(hx, 0, hy) * chunkWorld.minorChunkSize;

                    pos.y = mediumChunk.ValueRO.majorChunkHeightmap.Value.GetHeight(localInMajor.x - 1, localInMajor.y - 1);

                    int vi = x + y * scanRegion;
                    vertexes[vi] = pos;
                }
            }

            for (int y = 0; y < faceRegion; y++)
            {
                for (int x = 0; x < faceRegion; x++)
                {
                    int v00 = (x    ) + (y    ) * scanRegion;
                    int v10 = (x + 1) + (y    ) * scanRegion;
                    int v01 = (x    ) + (y + 1) * scanRegion;
                    int v11 = (x + 1) + (y + 1) * scanRegion;

                    int i = (x + y * faceRegion) * 6;

                    // triangle 1
                    indexes[i + 0] = v00;
                    indexes[i + 1] = v01;
                    indexes[i + 2] = v11;

                    // triangle 2
                    indexes[i + 3] = v00;
                    indexes[i + 4] = v11;
                    indexes[i + 5] = v10;
                }
            }

            mediumChunk.ValueRW.LOD2_Mesh = MeshBlobUtility.CreateMeshBlob(vertexes, indexes);

            SystemAPI.SetComponentEnabled<ChunkRequestMesh>(entity, false);            
        }
    }
}
