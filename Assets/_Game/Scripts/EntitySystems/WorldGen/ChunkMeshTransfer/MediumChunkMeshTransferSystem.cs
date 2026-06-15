using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

partial class MediumChunkMeshTransferSystem : SystemBase
{
    [BurstCompile]
    protected override void OnCreate()
    {
        
    }

    [BurstCompile]
    protected override void OnUpdate()
    {
        if (MediumChunkMirrorRegistry.Mirrors.Count == 0)
            return;

        foreach ((
            RefRO<MediumChunk> mediumChunk,
            RefRO<LocalTransform> Transform,
            Entity entity
            ) in SystemAPI.Query<
                RefRO<MediumChunk>,
                RefRO<LocalTransform>
                >().WithAll<Chunk, ChunkActive>()
                    .WithDisabled<ChunkRequestMesh, ChunkGenerationComplete>()
                    .WithEntityAccess())
        {
            MediumChunkMirror mirror;
            if(!MediumChunkMirrorRegistry.Mirrors.TryGetValue(entity, out mirror))
            {
                continue;
            }

            Mesh Lod2 = new Mesh();

            ref BlobArray<float3> vertices = ref mediumChunk.ValueRO.LOD2_Mesh.Value.Vertices;
            ref BlobArray<int> triangles = ref mediumChunk.ValueRO.LOD2_Mesh.Value.Triangles;

            Vector3[] verticesVector = new Vector3[vertices.Length];
            int[] trianglesManaged = new int[triangles.Length];
            
            for(int i = 0; i < vertices.Length; i++)
            {
                verticesVector[i] = vertices[i];
            }

            for(int i = 0; i < triangles.Length; i++)
            {
                trianglesManaged[i] = triangles[i];
            }

            Lod2.SetVertices(verticesVector);
            Lod2.SetTriangles(trianglesManaged, 0);

            Lod2.RecalculateNormals(); // TODO REMOVE AND CUSTOM CALCULATE
            Lod2.RecalculateBounds();

            mirror.TerrainLOD2 = Lod2;
            mirror.newMesh = true;

            mirror.GetComponentInParent<Transform>().position = Transform.ValueRO.Position;

            SystemAPI.SetComponentEnabled<ChunkGenerationComplete>(entity, true);
        }
    }

    [BurstCompile]
    protected override void OnDestroy()
    {
        
    }
}
