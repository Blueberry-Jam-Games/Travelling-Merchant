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
                WithAll<ChunkActive, ChunkContainsParentLink>().
                WithDisabled<ChunkGeneratedMesh>().
                WithEntityAccess())
        {
            int scanRegionLOD2 = chunkWorld.minorPerMediumChunk + 3;
            int faceRegionLOD2 = chunkWorld.minorPerMediumChunk + 2;

            NativeArray<float3> vertexesLOD2 = new NativeArray<float3>(scanRegionLOD2 * scanRegionLOD2, Allocator.Temp);
            NativeArray<int> indexesLOD2 = new NativeArray<int>(faceRegionLOD2 * faceRegionLOD2 * 6, Allocator.Temp);

            for (int y = 0; y < scanRegionLOD2; y++)
            {
                for (int x = 0; x < scanRegionLOD2; x++)
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

                    int vi = x + y * scanRegionLOD2;
                    vertexesLOD2[vi] = pos;
                }
            }

            for (int y = 0; y < faceRegionLOD2; y++)
            {
                for (int x = 0; x < faceRegionLOD2; x++)
                {
                    int v00 = (x    ) + (y    ) * scanRegionLOD2;
                    int v10 = (x + 1) + (y    ) * scanRegionLOD2;
                    int v01 = (x    ) + (y + 1) * scanRegionLOD2;
                    int v11 = (x + 1) + (y + 1) * scanRegionLOD2;

                    int i = (x + y * faceRegionLOD2) * 6;

                    // triangle 1
                    indexesLOD2[i + 0] = v00;
                    indexesLOD2[i + 1] = v01;
                    indexesLOD2[i + 2] = v11;

                    // triangle 2
                    indexesLOD2[i + 3] = v00;
                    indexesLOD2[i + 4] = v11;
                    indexesLOD2[i + 5] = v10;
                }
            }
            NativeArray<float3> normalsLOD2;
            GeometryHelper.CalculateNormalsWithCrease(ref vertexesLOD2, ref indexesLOD2, 180, Allocator.Temp, out normalsLOD2);

            StripRim(ref vertexesLOD2, ref normalsLOD2, scanRegionLOD2, Allocator.Temp, out NativeArray<float3> strippedVerticesLOD2, out NativeArray<float3> strippedNormalsLOD2, out NativeArray<int> strippedIndicesLOD2);

            NativeArray<float3> vertexesLOD3 = new NativeArray<float3>(4, Allocator.Temp);
            NativeArray<int> indexesLOD3 = new NativeArray<int>(6, Allocator.Temp);
            NativeArray<float3> normalsLOD3 = new NativeArray<float3>(4, Allocator.Temp);

            int N = scanRegionLOD2;

            int topLeft     = 1 + 1 * N;
            int topRight    = (N - 2) + 1 * N;
            int bottomLeft  = 1 + (N - 2) * N;
            int bottomRight = (N - 2) + (N - 2) * N;

            vertexesLOD3[0] = vertexesLOD2[topLeft];
            vertexesLOD3[1] = vertexesLOD2[topRight];
            vertexesLOD3[2] = vertexesLOD2[bottomLeft];
            vertexesLOD3[3] = vertexesLOD2[bottomRight];

            normalsLOD3[0] = normalsLOD2[topLeft];
            normalsLOD3[1] = normalsLOD2[topRight];
            normalsLOD3[2] = normalsLOD2[bottomLeft];
            normalsLOD3[3] = normalsLOD2[bottomRight];

            indexesLOD3[0] = 0;
            indexesLOD3[1] = 2;
            indexesLOD3[2] = 3;

            indexesLOD3[3] = 0;
            indexesLOD3[4] = 3;
            indexesLOD3[5] = 1;

            if (mediumChunk.ValueRO.LOD2_Mesh.IsCreated) mediumChunk.ValueRW.LOD2_Mesh.Dispose();

            if (mediumChunk.ValueRO.LOD3_Mesh.IsCreated) mediumChunk.ValueRW.LOD3_Mesh.Dispose();

            mediumChunk.ValueRW.LOD2_Mesh = MeshBlobUtility.CreateMeshBlob(strippedVerticesLOD2, strippedIndicesLOD2, strippedNormalsLOD2);

            mediumChunk.ValueRW.LOD3_Mesh = MeshBlobUtility.CreateMeshBlob(vertexesLOD3, indexesLOD3, normalsLOD3);

            SystemAPI.SetComponentEnabled<ChunkGeneratedMesh>(entity, true);

            vertexesLOD2.Dispose();
            indexesLOD2.Dispose();
            normalsLOD2.Dispose();

            strippedVerticesLOD2.Dispose();
            strippedNormalsLOD2.Dispose();
            strippedIndicesLOD2.Dispose();       
        }
    }

    [BurstCompile]
    private static void StripRim(ref NativeArray<float3> sourceVertices, ref NativeArray<float3> sourceNormals, int sourceResolution, Allocator allocator, out NativeArray<float3> vertices, out NativeArray<float3> normals, out NativeArray<int> indices)
    {
        // Remove the 1-vertex border
        int resolution = sourceResolution - 2;
        int faceResolution = resolution - 1;

        vertices = new NativeArray<float3>(
            resolution * resolution,
            allocator);

        normals = new NativeArray<float3>(
            resolution * resolution,
            allocator);

        indices = new NativeArray<int>(
            faceResolution * faceResolution * 6,
            allocator);

        //
        // Copy vertices and normals
        //
        for (int y = 0; y < resolution; y++)
        {
            for (int x = 0; x < resolution; x++)
            {
                int sourceIndex =
                    (x + 1) +
                    (y + 1) * sourceResolution;

                int destIndex =
                    x +
                    y * resolution;

                vertices[destIndex] = sourceVertices[sourceIndex];
                normals[destIndex] = sourceNormals[sourceIndex];
            }
        }

        //
        // Generate triangles
        //
        for (int y = 0; y < faceResolution; y++)
        {
            for (int x = 0; x < faceResolution; x++)
            {
                int v00 = x + y * resolution;
                int v10 = (x + 1) + y * resolution;
                int v01 = x + (y + 1) * resolution;
                int v11 = (x + 1) + (y + 1) * resolution;

                int i = (x + y * faceResolution) * 6;

                // Triangle 1
                indices[i + 0] = v00;
                indices[i + 1] = v01;
                indices[i + 2] = v11;

                // Triangle 2
                indices[i + 3] = v00;
                indices[i + 4] = v11;
                indices[i + 5] = v10;
            }
        }
    }
}

