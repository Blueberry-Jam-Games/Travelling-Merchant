using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using Unity.Physics;
using Unity.Collections.LowLevel.Unsafe;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Transforms;
using Unity.VisualScripting;

[UpdateInGroup(typeof(WorldGenerationSystemGroup))]
[UpdateAfter(typeof(ChunkMediumMeshSystem))]
[BurstCompile(DisableDirectCall = true)]
partial class ChunkMinorMeshSystem : SystemBase
{
    const int threadGroupSize = 8;
    public ComputeShader meshGenerator;

    List<ComputeContainer> computeContainers = new List<ComputeContainer>();

    ComputeBuffer cubeMarchArray;

    ComponentLookup<MajorChunk> majorChunkLookup;
    ComponentLookup<MinorChunk> minorChunkLookup;
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

        minorChunkLookup = GetComponentLookup<MinorChunk>(false);
        physicsColliderLookup = GetComponentLookup<PhysicsCollider>(false);
    }

    protected override void OnStartRunning()
    {
        ChunkWorldSingleton chunkWorld = SystemAPI.GetSingleton<ChunkWorldSingleton>();

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

        ChunkWorldSingleton chunkWorld = SystemAPI.GetSingleton<ChunkWorldSingleton>();

        // Access list of major chunks
        ref ChunkPlacementSystem chunkGeneratorSystem = ref World.Unmanaged.GetUnsafeSystemRef<ChunkPlacementSystem>(World.Unmanaged.GetExistingUnmanagedSystem<ChunkPlacementSystem>());

        majorChunkLookup.Update(this);
        minorChunkLookup.Update(this);
        physicsColliderLookup.Update(this);

        // 
        // Create the Mesh for a chunk requesting an update and set to Mesh Manager 
        // 
        foreach ((
            RefRO<LocalTransform> transform,
            RefRW<MinorChunk> minorChunk,
            Entity entity
            ) in SystemAPI.Query<
                RefRO<LocalTransform>,
                RefRW<MinorChunk>
                >()
                .WithAll<Chunk, ChunkActive>()
                .WithDisabled<ChunkGeneratedMesh, ChunkGeneratingMesh>()
                .WithEntityAccess())
        {
            if(minorChunk.ValueRO.mediumChunk != null)
            {
                Entity mediumChunk = minorChunk.ValueRO.mediumChunk;
                if(SystemAPI.IsComponentEnabled<ChunkPopulated>(mediumChunk))
                {
                    bool computeShadersOccupied = true;
                    foreach(ComputeContainer container in computeContainers)
                    {
                        if(container.generationStep == ComputeContainer.MinorChunkProgress.CLEARED)
                        {
                            computeShadersOccupied = false;
                            container.Prepare(entity);

                            container.Generate(meshGenerator, ref chunkWorld, transform.ValueRO.Position);

                            SystemAPI.SetComponentEnabled<ChunkGeneratingMesh>(entity, true);

                            break;
                        }
                        else
                        {
                            // Compute shader unavailable
                        }
                    }

                    // If this is true, no compute shaders were found that were clear. This breaks the loop for the frame.
                    if(computeShadersOccupied)
                    {
                        break;
                    }
                }
                else
                {
                    continue;
                }
            }
            else
            {
                continue;
            }
        }

        foreach(ComputeContainer container in computeContainers)
        {
            Entity chunkEntity = container.linkedMinorChunk;
            
            if(container.generationStep == ComputeContainer.MinorChunkProgress.CLEARED || container.generationStep == ComputeContainer.MinorChunkProgress.BOUND) continue;

            if (chunkEntity == Entity.Null || !SystemAPI.Exists(chunkEntity))
            {
                container.Reset();
                Debug.LogWarning("Invalid Entity");
                continue;
            }

            if (!minorChunkLookup.HasComponent(chunkEntity))
            {
                container.Reset();
                Debug.LogWarning("No Chunk Component");
                continue;
            }
            
            RefRW<MinorChunk> minorChunk = minorChunkLookup.GetRefRW(chunkEntity);

            if(container.generationStep == ComputeContainer.MinorChunkProgress.EMPTY_MESH)
            {
                SystemAPI.SetComponentEnabled<ChunkGeneratedMesh>(chunkEntity, true);
                SystemAPI.SetComponentEnabled<ChunkGeneratingMesh>(chunkEntity, false);
                minorChunk.ValueRW.meshHighDetailValid = false;
                container.Reset();
                continue;
            }
            else if(container.generationStep == ComputeContainer.MinorChunkProgress.READBACK_PENDING && container.readbackRequest.done)
            {
                if(container.readbackRequest.hasError)
                {
                    Debug.LogError("GPU Readback Error");
                    SystemAPI.SetComponentEnabled<ChunkGeneratedMesh>(chunkEntity, false);
                    SystemAPI.SetComponentEnabled<ChunkGeneratingMesh>(chunkEntity, false);
                    container.Reset();
                    return;
                }

                NativeArray<Triangle> tris = container.readbackRequest.GetData<Triangle>();

                if(minorChunk.ValueRO.meshBlobHighDetail.IsCreated)
                {
                    minorChunk.ValueRW.meshBlobHighDetail.Dispose();
                }

                minorChunk.ValueRW.meshBlobHighDetail = MeshBlobUtility.CreateMeshBlob(tris, tris.Length);

                container.Reset();

                minorChunk.ValueRW.meshHighDetailValid = true;
                SystemAPI.SetComponentEnabled<ChunkGeneratedMesh>(chunkEntity, true);
                SystemAPI.SetComponentEnabled<ChunkGeneratingMesh>(chunkEntity, false);
            }
        }
    }

    protected override void OnDestroy()
    {
        foreach(ComputeContainer c in computeContainers)
        {
            c.Dispose();
        }
        cubeMarchArray.Dispose();
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
                case 2:
                    return new float3(c.x, c.y, c.z);
                default:
                    return new float3(a.x, a.y, a.z);
            }
        }
    }
}

public class ComputeContainer
{
    public GraphicsBuffer triangleBuffer;
    public GraphicsBuffer pointBuffer;
    ComputeBuffer triCountBuffer;

    private int numDensityThreadsPerAxis;
    private int numMarchThreadsPerAxis;
    private int numPointsPerAxis;
    private int numPointsTotal;

    public Entity linkedMinorChunk;

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

        linkedMinorChunk = Entity.Null;

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

    public void Prepare(Entity linkedMinorChunkIn)
    {
        if(generationStep == MinorChunkProgress.CLEARED)
        {
            if(linkedMinorChunkIn != Entity.Null)
            {
                linkedMinorChunk = linkedMinorChunkIn;
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
        // Points buffer is populated inside shader with pos (xyz) + density (w).

        meshGenerator.SetBuffer (kernal, "points", pointBuffer);
        meshGenerator.SetInt ("numPointsPerAxis", numPointsPerAxis);
        meshGenerator.SetVector ("position", new Vector4 (position.x, position.y, position.z));

        meshGenerator.SetInt ("seed", chunkWorld.seed);

        float spacing = (float) chunkWorld.minorChunkSize / (numPointsPerAxis - 1);
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
        triangleBuffer.SetCounterValue(0);
        generationStep = MinorChunkProgress.CLEARED;
        linkedMinorChunk = Entity.Null;
    }

    public void Dispose()
    {
        triCountBuffer.Release();
        triangleBuffer.Release();
        pointBuffer.Release();
    }
}