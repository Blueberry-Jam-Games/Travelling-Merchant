using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;
using Unity.Physics;
using Unity.Collections.LowLevel.Unsafe;
using System.Collections.Generic;
using Unity.Collections;

[UpdateInGroup(typeof(WorldGenerationSystemGroup))]
[UpdateAfter(typeof(ChunkMediumMeshSystem))]
[BurstCompile(DisableDirectCall = true)]
partial class ChunkMeshSystem : SystemBase
{
    UnityEngine.Material terrainMaterial;
    const int threadGroupSize = 8;
    public ComputeShader meshGenerator;

    List<ComputeContainer> computeContainers = new List<ComputeContainer>();

    ComputeBuffer cubeMarchArray;

    ComponentLookup<MajorChunk> majorChunkLookup;
    ComponentLookup<Chunk> chunkLookup;
    ComponentLookup<PhysicsCollider> physicsColliderLookup;

    protected override void OnCreate()
    {
        meshGenerator = Resources.Load<ComputeShader>("ComputeShaders/MeshGeneration"); // New Shader

        if (meshGenerator == null) Debug.LogError("Missing Mesh Generation Shader File");

        if (meshGenerator != null)
        {
            cubeMarchArray = new ComputeBuffer(4096, sizeof(int), ComputeBufferType.Structured);
            cubeMarchArray.SetData(CubeMarchTriangulation.map);
        }

        int kernal = meshGenerator.FindKernel("March"); // New Code
        meshGenerator.SetBuffer(kernal, "cubeMarchArray", cubeMarchArray); // New code

        majorChunkLookup = GetComponentLookup<MajorChunk>(isReadOnly: true);

        chunkLookup = GetComponentLookup<Chunk>(false);
        physicsColliderLookup = GetComponentLookup<PhysicsCollider>(false);
    }

    protected override void OnStartRunning()
    {
        ChunkWorldSingleton chunkWorld = SystemAPI.GetSingleton<ChunkWorldSingleton>();
        terrainMaterial = Resources.Load<UnityEngine.Material>(chunkWorld.terrainMaterial.ToString());

        // Create compute containers. These will hold the compute buffers while they are being read back
        for(int i = 0; i < chunkWorld.DispatchableShaderCount; i++)
        {
            ComputeContainer newContainer = new ComputeContainer(chunkWorld.numPointsPerAxis, threadGroupSize);
            computeContainers.Add(newContainer);
        }
    }

    protected override void OnUpdate()
    {
        // Enable - Disable
        GameState gameState = SystemAPI.GetSingleton<GameState>();
        if(!gameState.chunkGenerationEnable) return;

        if (terrainMaterial == null) return;

        ChunkWorldSingleton chunkWorld = SystemAPI.GetSingleton<ChunkWorldSingleton>();

        // Access list of major chunks
        ref ChunkPlacementSystem chunkGeneratorSystem = ref World.Unmanaged.GetUnsafeSystemRef<ChunkPlacementSystem>(World.Unmanaged.GetExistingUnmanagedSystem<ChunkPlacementSystem>());

        majorChunkLookup.Update(this);

        chunkLookup.Update(this);
        physicsColliderLookup.Update(this);

        // 
        // Create the Mesh for a chunk requesting an update and set to Mesh Manager 
        // 
        foreach ((
            RefRW<ChunkRequestUpdate> req,
            RefRO<Chunk> chunk,
            RefRW<MinorChunk> minorChunk,
            RefRW<PhysicsCollider> physicsCollider,
            Entity entity
            ) in SystemAPI.Query<
                RefRW<ChunkRequestUpdate>,
                RefRO<Chunk>,
                RefRW<MinorChunk>,
                RefRW<PhysicsCollider>
                >().WithEntityAccess())
        {
            // if(chunkGeneratorSystem.majorChunkExists(chunk.ValueRO.majorChunkPosition))
            // {
            //     Entity majorChunk = chunkGeneratorSystem.getMajorChunkEntity(chunk.ValueRO.majorChunkPosition);
            //     if(SystemAPI.IsComponentEnabled<MajorChunkReady>(majorChunk))
            //     {
            //         if(!majorChunkLookup.TryGetComponent(majorChunk, out MajorChunk output))
            //         {
            //             Debug.LogError("No Component");
            //             continue;
            //         }

            //         bool computeShadersOccupied = true;
            //         foreach(ComputeContainer container in computeContainers)
            //         {
            //             if(container.generationStep == ComputeContainer.MinorChunkProgress.CLEARED)
            //             {
            //                 computeShadersOccupied = false;
            //                 container.Prepare(entity);

            //                 container.Generate(meshGenerator, ref chunkWorld, chunk.ValueRO.chunkPosition * chunkWorld.chunkSize);

            //                 SystemAPI.SetComponentEnabled<ChunkRequestUpdate>(entity, false);

            //                 break;
            //             }
            //             else
            //             {
            //                 // Compute shader unavailable
            //             }
            //         }

            //         // If this is true, no compute shaders were found that were clear. This breaks the loop for the frame.
            //         if(computeShadersOccupied)
            //         {
            //             break;
            //         }
            //     }
            //     else
            //     {
            //         continue;
            //     }
            // }
            // else
            // {
            //     continue;
            // }
        }

        foreach(ComputeContainer container in computeContainers)
        {
            // Entity chunkEntity = container.linkedChunk;
            
            // if(container.generationStep == ComputeContainer.MinorChunkProgress.CLEARED || container.generationStep == ComputeContainer.MinorChunkProgress.BOUND) continue;

            // if (chunkEntity == Entity.Null || !SystemAPI.Exists(chunkEntity))
            // {
            //     container.Reset();
            //     Debug.LogWarning("No Entity");
            //     continue;
            // }

            // if (!chunkLookup.HasComponent(chunkEntity))
            // {
            //     container.Reset();
            //     Debug.LogWarning("No Chunk Component");
            //     continue;
            // }

            // if (!physicsColliderLookup.HasComponent(chunkEntity))
            // {
            //     container.Reset();
            //     Debug.LogWarning("No Physics Collider");
            //     continue;
            // }
            
            // RefRW<Chunk> chunk = chunkLookup.GetRefRW(chunkEntity);

            // RefRW<PhysicsCollider> physicsCollider = physicsColliderLookup.GetRefRW(chunkEntity);

            // if(container.generationStep == ComputeContainer.MinorChunkProgress.EMPTY_MESH)
            // {
            //     Mesh mesh = ChunkMeshManagerSystem.Instance.GetMesh(chunk.ValueRO.meshIndex);
            //     mesh.Clear();
            //     ChunkMeshManagerSystem.Instance.SetMesh(mesh, chunk.ValueRO.meshIndex);

            //     if (physicsCollider.ValueRW.Value.IsCreated)
            //         physicsCollider.ValueRW.Value.Dispose();

            //     container.Reset();
            //     continue;
            // }
            // else if(container.generationStep == ComputeContainer.MinorChunkProgress.READBACK_PENDING && container.readbackRequest.done)
            // {
            //     if(container.readbackRequest.hasError)
            //     {
            //         Debug.LogError("GPU Readback Error");
            //         container.Reset();
            //         return;
            //     }

            //     NativeArray<Triangle> tris = container.readbackRequest.GetData<Triangle>();

            //     if(chunk.ValueRO.meshBlob.IsCreated)
            //     {
            //         chunk.ValueRW.meshBlob.Dispose();
            //     }

            //     BlobAssetReference<MeshBlob> blob = MeshBlobUtility.CreateMeshBlob(tris, tris.Length);

            //     chunk.ValueRW.meshBlob = blob;

            //     container.Reset();

            //     Mesh mesh = ChunkMeshManagerSystem.Instance.GetMesh(chunk.ValueRO.meshIndex);
            //     mesh.Clear();

            //     ref MeshBlob meshBlob = ref chunk.ValueRO.meshBlob.Value;

            //     Vector3[] vertices = new Vector3[meshBlob.Vertices.Length];
            //     int[] triangles = new int[meshBlob.Triangles.Length];

            //     for(int i = 0; i < meshBlob.Vertices.Length; i++)
            //     {
            //         float3 v = meshBlob.Vertices[i];
            //         vertices[i] = new Vector3(v.x, v.y, v.z);
            //     }

            //     for(int i = 0; i < meshBlob.Triangles.Length; i++)
            //     {
            //         triangles[i] = meshBlob.Triangles[i];
            //     }

            //     mesh.vertices = vertices;
            //     mesh.triangles = triangles;
                
            //     mesh.RecalculateNormals();
            //     mesh.RecalculateTangents();
            //     mesh.RecalculateBounds();

            //     ChunkMeshManagerSystem.Instance.SetMesh(mesh, chunk.ValueRO.meshIndex);

            //     SystemAPI.SetComponentEnabled<ChunkRequestPhysics>(chunkEntity, true);

            //     if (physicsCollider.ValueRW.Value.IsCreated)
            //         physicsCollider.ValueRW.Value.Dispose();
            // }
        }

        // foreach (var (chunk, transform, chunkActive, entity) in SystemAPI.Query<RefRO<Chunk>, RefRO<LocalTransform>, RefRO<ChunkActive>>().WithEntityAccess())
        // {
        //     Mesh mesh = ChunkMeshManagerSystem.Instance.GetMesh(chunk.ValueRO.meshIndex);

        //     if (mesh == null)
        //         continue;

        //     if (mesh.vertexCount == 0 || mesh.triangles.Length == 0)
        //         continue;

        //     if (!mesh.HasVertexAttribute(UnityEngine.Rendering.VertexAttribute.Position))
        //         continue;

        //     Graphics.RenderMesh(new RenderParams(terrainMaterial), ChunkMeshManagerSystem.Instance.GetMesh(chunk.ValueRO.meshIndex), 0, transform.ValueRO.ToMatrix());
        // }
    }
}

public struct Triangle
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

public class ComputeContainer
{
    //public GraphicsBuffer densityMap;
    public GraphicsBuffer triangleBuffer;
    public GraphicsBuffer pointBuffer;
    ComputeBuffer triCountBuffer;

    private int numDensityThreadsPerAxis;
    private int numMarchThreadsPerAxis;
    private int numPointsPerAxis;
    private int numPointsTotal;

    public Entity linkedChunk;

    public UnityEngine.Rendering.AsyncGPUReadbackRequest readbackRequest;
    private int triangleCount;

    public MinorChunkProgress generationStep;

    public enum MinorChunkProgress
    {
        CLEARED,
        BOUND,
        MESH_GENERATED,
        READBACK_PENDING,
        EMPTY_MESH
    }

    public ComputeContainer(int numPointsPerAxis, int threadGroupSize)
    {
        numPointsTotal = numPointsPerAxis * numPointsPerAxis * numPointsPerAxis;
        int numVoxelsPerAxis = numPointsPerAxis - 1;
        int numVoxels = numVoxelsPerAxis * numVoxelsPerAxis * numVoxelsPerAxis;
        int maxTriangleCount = numVoxels * 5;

        numDensityThreadsPerAxis = Mathf.CeilToInt(numPointsPerAxis / (float)threadGroupSize);
        numMarchThreadsPerAxis = Mathf.CeilToInt(numVoxelsPerAxis / (float)threadGroupSize);
        this.numPointsPerAxis = numPointsPerAxis;

        //densityMap = new GraphicsBuffer(GraphicsBuffer.Target.Append, numPointsTotal, sizeof(float));
        triangleBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Append, maxTriangleCount, UnsafeUtility.SizeOf<Triangle>());
        pointBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, numPointsTotal, UnsafeUtility.SizeOf<float4>());
        triCountBuffer = new ComputeBuffer(1, sizeof(int), ComputeBufferType.Raw);

        //densityMap.SetCounterValue(0);
        triangleBuffer.SetCounterValue(0);
        pointBuffer.SetCounterValue(0);

        linkedChunk = Entity.Null;

        generationStep = MinorChunkProgress.CLEARED;
    }

    public void Generate(ComputeShader meshGenerator, ref ChunkWorldSingleton chunkWorld, float3 position)
    {
        if(generationStep != MinorChunkProgress.BOUND)
        {
            Debug.LogWarning("Chunk was not bound to the mesh generator");
            return;
        }
        
        RunDensityShader(meshGenerator, position, ref chunkWorld);

        RunCubeMarch(meshGenerator, chunkWorld.isoLevel);

        //Debug.Log("Mesh Generation Completed");
        generationStep = MinorChunkProgress.MESH_GENERATED;

        BeginReadback();
    }

    public void Prepare(Entity linkedChunkIn)
    {
        if(generationStep == MinorChunkProgress.CLEARED)
        {
            if(linkedChunkIn != Entity.Null)
            {
                linkedChunk = linkedChunkIn;
                generationStep = MinorChunkProgress.BOUND;
            }
            else
            {
                Debug.LogError("Null Entity Assigned");
                return;
            }
        }
        else
        {
            Debug.LogWarning("Chunk not reset properly");
        }
    }

    public void RunDensityShader(ComputeShader meshGenerator, float3 position, ref ChunkWorldSingleton chunkWorld)
    {
        int kernal = meshGenerator.FindKernel("Density");
        int numPoints = numPointsPerAxis * numPointsPerAxis * numPointsPerAxis;
        // Points buffer is populated inside shader with pos (xyz) + density (w).
        // Set paramaters
        int octaves = 4;

        float lacunarity = 1.0f;
        float persistence = 0.3f;

        float noiseScale = 0.2f;

        float noiseWeight = 7.0f;

        float floorOffset = 0.0f;

        float weightMultiplier = 2.0f;

        float hardFloor = 0.0f;
        float hardFloorWeight = 20.0f;

        meshGenerator.SetInt ("octaves", octaves);
        meshGenerator.SetFloat("lacunarity", lacunarity);
        meshGenerator.SetFloat("persistence", persistence);
        meshGenerator.SetFloat("noiseScale", noiseScale);
        meshGenerator.SetFloat("noiseWeight", noiseWeight);
        meshGenerator.SetFloat("floorOffset", floorOffset);
        meshGenerator.SetFloat("weightMultiplier", weightMultiplier);
        meshGenerator.SetFloat("hardFloor", hardFloor);
        meshGenerator.SetFloat("hardFloorWeight", hardFloorWeight);


        meshGenerator.SetBuffer (kernal, "points", pointBuffer);
        meshGenerator.SetInt ("numPointsPerAxis", numPointsPerAxis);
        meshGenerator.SetFloat ("boundsSize", chunkWorld.chunkSize);
        meshGenerator.SetVector ("centre", new Vector4 (position.x, position.y, position.z));
        Vector4 offset = new Vector4(0, 0, 0, 0);
        meshGenerator.SetVector ("offset", offset); // TODO Consider removing

        float spacing = (float) chunkWorld.chunkSize / (float) (numPointsPerAxis - 1);
        meshGenerator.SetFloat("spacing", spacing);

        // Dispatch shader
        meshGenerator.Dispatch (kernal, numDensityThreadsPerAxis, numDensityThreadsPerAxis, numDensityThreadsPerAxis);
    }

    public void RunCubeMarch(ComputeShader meshGenerator, float isoLevel)
    {
        int kernal = meshGenerator.FindKernel("March");

        meshGenerator.SetBuffer(kernal, "points", pointBuffer);
        meshGenerator.SetBuffer(kernal, "triangles", triangleBuffer);
        triangleBuffer.SetCounterValue(0);
        meshGenerator.SetInt("numPointsPerAxis", numPointsPerAxis);
        meshGenerator.SetFloat("isoLevel", isoLevel);

        meshGenerator.Dispatch(kernal, numMarchThreadsPerAxis, numMarchThreadsPerAxis, numMarchThreadsPerAxis);
    }

    public void BeginReadback()
    {
        if(generationStep != MinorChunkProgress.MESH_GENERATED)
        {
            return;
        }

        GraphicsBuffer.CopyCount(triangleBuffer, triCountBuffer, 0);
        int[] triCountArray = { 0 };
        triCountBuffer.GetData(triCountArray);
        int count = triCountArray[0];

        if (count == 0)
        {
            generationStep = MinorChunkProgress.EMPTY_MESH;
            return;
        }

        readbackRequest = UnityEngine.Rendering.AsyncGPUReadback.Request(triangleBuffer, count * UnsafeUtility.SizeOf<Triangle>(), 0);

        generationStep = MinorChunkProgress.READBACK_PENDING;
    }

    public void Reset()
    {
        //densityMap.SetCounterValue(0);
        triangleBuffer.SetCounterValue(0);
        pointBuffer.SetCounterValue(0);
        generationStep = MinorChunkProgress.CLEARED;
        linkedChunk = Entity.Null;
    }

    public void Destroy()
    {
        //densityMap.Release();
        triangleBuffer.Release();
        pointBuffer.Release();
    }
}