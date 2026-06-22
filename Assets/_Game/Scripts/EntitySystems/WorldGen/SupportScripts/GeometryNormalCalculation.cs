using Unity.Burst;
using Unity.Collections;
using Unity.Mathematics;

[BurstCompile]
public static class GeometryHelper
{
    [BurstCompile]
    public static void CalculateNormals(ref NativeArray<float3> vertices, ref NativeArray<int> triangles, Allocator allocator, out NativeArray<float3> outNormals)
    {
        NativeArray<float3> normals = new NativeArray<float3>(vertices.Length, allocator);

        // Start at zero
        for (int i = 0; i < normals.Length; i++)
        {
            normals[i] = float3.zero;
        }

        // Accumulate face normals
        for (int i = 0; i < triangles.Length; i += 3)
        {
            int i0 = triangles[i];
            int i1 = triangles[i + 1];
            int i2 = triangles[i + 2];

            float3 v0 = vertices[i0];
            float3 v1 = vertices[i1];
            float3 v2 = vertices[i2];

            float3 edge1 = v1 - v0;
            float3 edge2 = v2 - v0;

            float3 faceNormal = math.cross(edge1, edge2);

            normals[i0] += faceNormal;
            normals[i1] += faceNormal;
            normals[i2] += faceNormal;
        }

        // Normalize
        for (int i = 0; i < normals.Length; i++)
        {
            if (!normals[i].Equals(float3.zero))
            {
                normals[i] = math.normalize(normals[i]);
            }
            else
            {
                normals[i] = math.up();
            }
        }
        outNormals = normals;
    }

    [BurstCompile]
    public static void CalculateNormalsWithCrease(
        ref NativeArray<float3> vertices,
        ref NativeArray<int> triangles,
        float maxAngleDeg,
        Allocator allocator,
        out NativeArray<float3> outNormals
        ){
        int vertexCount = vertices.Length;

        NativeArray<float3> vertexNormals = new NativeArray<float3>(vertexCount, allocator);
        NativeArray<int> faceCounts = new NativeArray<int>(vertexCount, Allocator.Temp);

        // We need a per-face normal buffer
        int faceCount = triangles.Length / 3;
        NativeArray<float3> faceNormals = new NativeArray<float3>(faceCount, Allocator.Temp);

        float cosThreshold = math.cos(math.radians(maxAngleDeg));

        // -------------------------
        // 1. Compute face normals
        // -------------------------
        for (int f = 0; f < faceCount; f++)
        {
            int i = f * 3;

            int i0 = triangles[i];
            int i1 = triangles[i + 1];
            int i2 = triangles[i + 2];

            float3 v0 = vertices[i0];
            float3 v1 = vertices[i1];
            float3 v2 = vertices[i2];

            float3 faceNormal = math.cross(v1 - v0, v2 - v0);

            faceNormals[f] = faceNormal;
        }

        // ---------------------------------------
        // 2. Initial vertex normal accumulation
        // ---------------------------------------
        for (int f = 0; f < faceCount; f++)
        {
            int i = f * 3;

            int i0 = triangles[i];
            int i1 = triangles[i + 1];
            int i2 = triangles[i + 2];

            float3 n = faceNormals[f];

            vertexNormals[i0] += n;
            vertexNormals[i1] += n;
            vertexNormals[i2] += n;

            faceCounts[i0]++;
            faceCounts[i1]++;
            faceCounts[i2]++;
        }

        // ---------------------------------------
        // 3. Normalize initial vertex normals
        // ---------------------------------------
        for (int i = 0; i < vertexCount; i++)
        {
            if (faceCounts[i] > 0)
                vertexNormals[i] = math.normalize(vertexNormals[i]);
            else
                vertexNormals[i] = math.up();
        }

        // ---------------------------------------
        // 4. Crease filtering pass
        // ---------------------------------------
        NativeArray<float3> finalNormals = new NativeArray<float3>(vertexCount, allocator);

        for (int f = 0; f < faceCount; f++)
        {
            int i = f * 3;

            int i0 = triangles[i];
            int i1 = triangles[i + 1];
            int i2 = triangles[i + 2];

            float3 faceNormal = math.normalize(faceNormals[f]);

            Accumulate(i0);
            Accumulate(i1);
            Accumulate(i2);

            void Accumulate(int vi)
            {
                float3 avg = vertexNormals[vi];

                float dot = math.dot(avg, faceNormal);

                if (dot >= cosThreshold)
                {
                    finalNormals[vi] += faceNormal;
                }
            }
        }

        // ---------------------------------------
        // 5. Normalize final output
        // ---------------------------------------
        for (int i = 0; i < vertexCount; i++)
        {
            if (math.lengthsq(finalNormals[i]) > 0)
                finalNormals[i] = math.normalize(finalNormals[i]);
            else
                finalNormals[i] = vertexNormals[i]; // fallback
        }

        faceNormals.Dispose();
        vertexNormals.Dispose();
        faceCounts.Dispose();

        outNormals = finalNormals;
    }
}
