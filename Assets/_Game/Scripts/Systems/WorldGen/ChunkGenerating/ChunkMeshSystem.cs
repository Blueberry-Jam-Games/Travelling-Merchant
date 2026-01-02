using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using Unity.Collections;
using Unity.Rendering;
using UnityEngine;
using Unity.Physics;
using UnityEngine.Assertions.Must;
using System.Linq;

[UpdateInGroup(typeof(PresentationSystemGroup))]
[BurstCompile(DisableDirectCall = true)]
partial class ChunkMeshSystem : SystemBase
{
    UnityEngine.Material terrainMaterial;
    const int threadGroupSize = 8;
    public ComputeShader shader;
    public DensityGenerator densityGenerator;
    ComputeBuffer triangleBuffer;
    ComputeBuffer pointsBuffer;
    ComputeBuffer triCountBuffer;

    ComputeBuffer cubeMarchArray;

    protected override void OnCreate()
    {
        shader = Resources.Load<ComputeShader>("ComputeShaders/MarchingCubes");
        densityGenerator = new NoiseDensity();
        densityGenerator.densityShader = Resources.Load<ComputeShader>("ComputeShaders/NoiseDensity");

        if (shader == null) Debug.LogError("Missing shader");
        if (densityGenerator.densityShader == null) Debug.LogError("Missing Density Shader");

        if(shader != null)
        {
            cubeMarchArray = new ComputeBuffer(4096, sizeof(int), ComputeBufferType.Structured);
            cubeMarchArray.SetData(CubeMarchTriangulation.map);
        }
    }

    protected override void OnStartRunning()
    {
        ChunkWorldSingleton chunkWorld = SystemAPI.GetSingleton<ChunkWorldSingleton>();
        terrainMaterial = Resources.Load<UnityEngine.Material>(chunkWorld.terrainMaterial.ToString());
    }

    protected override void OnUpdate()
    {
        // Enable - Disable
        GameState gameState = SystemAPI.GetSingleton<GameState>();
        if(!gameState.chunkGenerationEnable) return;

        if (terrainMaterial == null) return;

        ChunkWorldSingleton chunkWorld = SystemAPI.GetSingleton<ChunkWorldSingleton>();
        Entity chunkWorldEntity = SystemAPI.GetSingletonEntity<ChunkWorldSingleton>();

        if(pointsBuffer == null || triangleBuffer == null || triCountBuffer == null)
        {
            CreateBuffers(chunkWorld.numPointsPerAxis);
        }

        // 
        // Create the Mesh for a chunk requesting an update and set to Mesh Manager 
        // 
        foreach ((
            RefRW<ChunkRequestUpdate> req,
            RefRW<Chunk> chunk,
            RefRW<PhysicsCollider> physicsCollider,
            Entity entity
            ) in SystemAPI.Query<
                RefRW<ChunkRequestUpdate>,
                RefRW<Chunk>,
                RefRW<PhysicsCollider>
                >().WithEntityAccess())
        {
            // Get local copy of values
            Chunk localChunk = chunk.ValueRO;
            int numPointsPerAxis = chunkWorld.numPointsPerAxis;
            int boundsSize = chunkWorld.chunkSize;
            Vector4 offset = Vector4.zero;

            // Imported
            int numVoxelsPerAxis = numPointsPerAxis - 1;
            int numThreadsPerAxis = Mathf.CeilToInt(numVoxelsPerAxis / (float)threadGroupSize);
            float pointSpacing = (float) boundsSize / (float) (numPointsPerAxis - 1);

            int3 coord = localChunk.chunkPosition;
            Vector3 centre = CentreFromCoord(coord, boundsSize);

            densityGenerator.Generate(pointsBuffer, numPointsPerAxis, boundsSize, centre, offset, pointSpacing);

            int marchKernel = shader.FindKernel("March");

            shader.SetBuffer(marchKernel, "points", pointsBuffer);
            shader.SetBuffer(marchKernel, "triangles", triangleBuffer);
            triangleBuffer.SetCounterValue(0);
            shader.SetInt("numPointsPerAxis", numPointsPerAxis);
            shader.SetFloat("isoLevel", chunkWorld.isoLevel);
            shader.SetBuffer(marchKernel, "cubeMarchArray", cubeMarchArray);

            shader.Dispatch(marchKernel, numThreadsPerAxis, numThreadsPerAxis, numThreadsPerAxis);

            // Get number of triangles in the triangle buffer
            ComputeBuffer.CopyCount(triangleBuffer, triCountBuffer, 0);
            int[] triCountArray = { 0 };
            triCountBuffer.GetData(triCountArray);
            int numTris = triCountArray[0];

            if(numTris == 0)
            {
                ChunkMeshManagerSystem.Instance.SetMesh(new Mesh(), chunk.ValueRO.meshIndex);

                if (physicsCollider.ValueRW.Value.IsCreated)
                    physicsCollider.ValueRW.Value.Dispose();
                
                SystemAPI.SetComponentEnabled<ChunkRequestUpdate>(entity, false);

                continue;
            }

            // Get triangle data from shader
            Triangle[] tris = new Triangle[numTris];
            triangleBuffer.GetData(tris, 0, 0, numTris);

            Mesh mesh = ChunkMeshManagerSystem.Instance.GetMesh(localChunk.meshIndex);
            mesh.Clear();

            // Move data into arrays. * 3 used for 3 verticies per face 
            var vertices = new Vector3[numTris * 3];
            var meshTriangles = new int[numTris * 3];

            for (int i = 0; i < numTris; i++)
            {
                for (int j = 0; j < 3; j++)
                {
                    meshTriangles[i * 3 + j] = i * 3 + j;
                    vertices[i * 3 + j] = tris[i][j];
                }
            }
            mesh.vertices = vertices;
            mesh.triangles = meshTriangles;

            mesh.RecalculateNormals();
            mesh.RecalculateTangents();
            mesh.RecalculateBounds();

            ChunkMeshManagerSystem.Instance.SetMesh(mesh, chunk.ValueRO.meshIndex);

            if (physicsCollider.ValueRW.Value.IsCreated)
                physicsCollider.ValueRW.Value.Dispose();

            physicsCollider.ValueRW.Value = Unity.Physics.MeshCollider.Create(mesh, CollisionFilter.Default, Unity.Physics.Material.Default);

            SystemAPI.SetComponentEnabled<ChunkRequestUpdate>(entity, false);
            break;
        }

        foreach (var (chunk, transform, entity) in SystemAPI.Query<RefRO<Chunk>, RefRO<LocalTransform>>().WithNone<ChunkSleeping>().WithEntityAccess())
        {
            Graphics.RenderMesh(new RenderParams(terrainMaterial), ChunkMeshManagerSystem.Instance.GetMesh(chunk.ValueRO.meshIndex), 0, transform.ValueRO.ToMatrix());
        }
    }

    float3 CentreFromCoord(int3 coord, float boundsSize)
    {

        return new float3(coord.x, coord.y, coord.z) * boundsSize;
    }

    struct Triangle
    {
#pragma warning disable 649 // disable unassigned variable warning
        public float4 a;
        public float4 b;
        public float4 c;

        public float3 this[int i]
        {
            get
            {
                switch (i)
                {
                    case 0:
                        return new float3(a.x, a.y, a.z);
                    case 1:
                        return new float3(b.x, b.y, b.z);
                    default:
                        return new float3(c.x, c.y, c.z);
                }
            }
        }
    }

    void CreateBuffers(int numPointsPerAxis)
    {
        int numPoints = numPointsPerAxis * numPointsPerAxis * numPointsPerAxis;
        int numVoxelsPerAxis = numPointsPerAxis - 1;
        int numVoxels = numVoxelsPerAxis * numVoxelsPerAxis * numVoxelsPerAxis;
        int maxTriangleCount = numVoxels * 5;

        triangleBuffer = new ComputeBuffer(maxTriangleCount, sizeof(float) * 4 * 3, ComputeBufferType.Append);
        pointsBuffer = new ComputeBuffer(numPoints, sizeof(float) * 4);
        triCountBuffer = new ComputeBuffer(1, sizeof(int), ComputeBufferType.Raw);
    }


    protected override void OnDestroy()
    {
        if(triangleBuffer.IsValid())
        {
            triangleBuffer.Dispose();
            pointsBuffer.Dispose();
            triCountBuffer.Dispose();
        }
        base.OnDestroy();
    }
}