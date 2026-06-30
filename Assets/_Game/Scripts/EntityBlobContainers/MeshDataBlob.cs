using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

public struct MeshBlob
{
    public BlobArray<float3> Vertices;
    public BlobArray<int> Triangles;
    public BlobArray<float3> Normals;
}

public static class MeshBlobUtility
{
    public static BlobAssetReference<MeshBlob> CreateMeshBlob(
        NativeArray<float3> vertices,
        NativeArray<int> triangles,
        NativeArray<float3> normals)
    {
        BlobBuilder builder = new BlobBuilder(Allocator.Temp);

        ref MeshBlob root = ref builder.ConstructRoot<MeshBlob>();

        // Allocate vertex array
        BlobBuilderArray<float3> verts =
            builder.Allocate(ref root.Vertices, vertices.Length);

        for (int i = 0; i < vertices.Length; i++)
        {
            verts[i] = vertices[i];
        }

        // Allocate triangle array
        BlobBuilderArray<int> tris =
            builder.Allocate(ref root.Triangles, triangles.Length);

        for (int i = 0; i < triangles.Length; i++)
        {
            tris[i] = triangles[i];
        }

        BlobBuilderArray<float3> norms =
        builder.Allocate(ref root.Normals, normals.Length);

        for (int i = 0; i < normals.Length; i++)
        {
            norms[i] = normals[i];
        }

        BlobAssetReference<MeshBlob> blob =
            builder.CreateBlobAssetReference<MeshBlob>(Allocator.Persistent);

        builder.Dispose();

        return blob;
    }

    public static BlobAssetReference<MeshBlob> CreateMeshBlob(
        NativeArray<Triangle> triangles,
        int numTriangles)
    {
        BlobBuilder builder = new BlobBuilder(Allocator.Temp);

        ref MeshBlob root = ref builder.ConstructRoot<MeshBlob>();

        // Allocate vertex array
        BlobBuilderArray<float3> verts = builder.Allocate(ref root.Vertices, triangles.Length * 3);
        BlobBuilderArray<int> tris = builder.Allocate(ref root.Triangles, triangles.Length * 3);

        for (int i = 0; i < numTriangles; i++)
        {
            for (int j = 0; j < 3; j++)
            {
                tris[i * 3 + j] = i * 3 + j;
                verts[i * 3 + j] = triangles[i][j];
            }
        }

        BlobAssetReference<MeshBlob> blob = builder.CreateBlobAssetReference<MeshBlob>(Allocator.Persistent);

        builder.Dispose();

        return blob;
    }
}

public struct ECSMesh
{
    private NativeList<float3> vertices;
    private NativeList<int> triangles;
    private NativeList<float3> normals;

    private int written;

    public ECSMesh(Allocator allocator)
    {
        vertices = new NativeList<float3>(allocator);
        triangles = new NativeList<int>(allocator);
        normals = new NativeList<float3>(allocator);
        written = 0;
    }

    public void Add(BlobAssetReference<MeshBlob> blob, float3 offset)
    {
        ref MeshBlob b = ref blob.Value;

        int baseVertex = vertices.Length;
        // Add vertices
        for (int i = 0; i < b.Vertices.Length; i++)
            vertices.Add(b.Vertices[i] + offset);

        // Add triangles (with offset remap)
        for (int i = 0; i < b.Triangles.Length; i++)
            triangles.Add(b.Triangles[i] + baseVertex);

        // Add normals (1:1 with vertices)
        for (int i = 0; i < b.Normals.Length; i++)
            normals.Add(b.Normals[i]);
    }

    public BlobAssetReference<MeshBlob> Build(Allocator allocator)
    {
        BlobBuilder builder = new BlobBuilder(Allocator.Temp);
        ref MeshBlob root = ref builder.ConstructRoot<MeshBlob>();

        BlobBuilderArray<float3> v = builder.Allocate(ref root.Vertices, vertices.Length);
        BlobBuilderArray<int> t = builder.Allocate(ref root.Triangles, triangles.Length);
        BlobBuilderArray<float3> n = builder.Allocate(ref root.Normals, normals.Length);

        for (int i = 0; i < vertices.Length; i++)
            v[i] = vertices[i];

        for (int i = 0; i < triangles.Length; i++)
            t[i] = triangles[i];

        for (int i = 0; i < normals.Length; i++)
            n[i] = normals[i];

        var blob = builder.CreateBlobAssetReference<MeshBlob>(allocator);
        builder.Dispose();

        return blob;
    }

    public void Dispose()
    {
        if (vertices.IsCreated) vertices.Dispose();
        if (triangles.IsCreated) triangles.Dispose();
        if (normals.IsCreated) normals.Dispose();
    }
}