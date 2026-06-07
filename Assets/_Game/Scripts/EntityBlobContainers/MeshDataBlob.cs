using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

public struct MeshBlob
{
    public BlobArray<float3> Vertices;
    public BlobArray<int> Triangles;
}

public static class MeshBlobUtility
{
    public static BlobAssetReference<MeshBlob> CreateMeshBlob(
        NativeArray<float3> vertices,
        NativeArray<int> triangles)
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