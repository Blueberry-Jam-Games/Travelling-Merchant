using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

partial class MediumChunkLod23TransferSystem : SystemBase
{
    protected override void OnUpdate()
    {
        if (MediumChunkMirrorRegistry.Mirrors.Count == 0)
            return;

        foreach ((
            RefRO<MediumChunk> mediumChunk,
            RefRO<LocalTransform> transform,
            Entity entity
            ) in SystemAPI.Query<
                RefRO<MediumChunk>,
                RefRO<LocalTransform>
                >()
                .WithAll<Chunk, ChunkActive>()
                .WithDisabled<ChunkRequestMesh, ChunkGenerationComplete>()
                .WithEntityAccess())
        {
            if (!MediumChunkMirrorRegistry.Mirrors.TryGetValue(entity, out MediumChunkMirror mirror))
                continue;

            if (!mediumChunk.ValueRO.LOD2_Mesh.IsCreated ||
                !mediumChunk.ValueRO.LOD3_Mesh.IsCreated)
                continue;

            GenerateMesh(
                mediumChunk.ValueRO.LOD2_Mesh,
                mirror.TerrainLOD2);

            GenerateMesh(
                mediumChunk.ValueRO.LOD3_Mesh,
                mirror.TerrainLOD3);

            mirror.newMesh = true;

            mirror.transform.position = transform.ValueRO.Position;

            SystemAPI.SetComponentEnabled<ChunkGenerationComplete>(entity, true);
        }
    }

    private static void GenerateMesh(BlobAssetReference<MeshBlob> meshBlob, Mesh targetMesh)
    {
        ref MeshBlob blob = ref meshBlob.Value;

        ref var vertices = ref blob.Vertices;
        ref var triangles = ref blob.Triangles;
        ref var normals = ref blob.Normals;

        Vector3[] managedVertices = new Vector3[vertices.Length];
        int[] managedTriangles = new int[triangles.Length];
        Vector3[] managedNormals = new Vector3[normals.Length];

        for (int i = 0; i < vertices.Length; i++)
        {
            float3 v = vertices[i];
            managedVertices[i] = new Vector3(v.x, v.y, v.z);
        }

        for (int i = 0; i < triangles.Length; i++)
        {
            managedTriangles[i] = triangles[i];
        }

        for (int i = 0; i < normals.Length; i++)
        {
            float3 v = normals[i];
            managedNormals[i] = new Vector3(v.x, v.y, v.z);
        }

        targetMesh.Clear();

        targetMesh.vertices = managedVertices;
        targetMesh.triangles = managedTriangles;
        targetMesh.normals = managedNormals;

        // TODO: Replace with custom normal generation
        //targetMesh.RecalculateNormals();
        targetMesh.RecalculateTangents();
        targetMesh.RecalculateBounds();
    }
}