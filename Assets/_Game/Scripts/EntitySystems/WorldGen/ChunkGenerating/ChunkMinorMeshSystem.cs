using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using Unity.Collections.LowLevel.Unsafe;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Transforms;
using Unity.VisualScripting;
using UnityEngine.Rendering;

[UpdateInGroup(typeof(WorldGenerationSystemGroup))]
[UpdateAfter(typeof(ChunkMediumMeshSystem))]
[BurstCompile(DisableDirectCall = true)]
partial class ChunkMinorMeshSystem : SystemBase
{
    const int threadGroupSize = 8;
    public ComputeShader meshGenerator;

    List<ComputeContainer> computeContainers = new List<ComputeContainer>();

    CommandBuffer commandBuffer;
    GraphicsFence commandBufferFence;
    bool commandBufferInFlight;
    int densityKernal;
    int marchKernal;

    ComputeBuffer cubeMarchArray;

    ComponentLookup<MajorChunk> majorChunkLookup;
    ComponentLookup<MinorChunk> minorChunkLookup;

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

        commandBuffer = new CommandBuffer
        {
            name = "Minor Chunk Mesh Generation"
        };
        commandBuffer.SetExecutionFlags(CommandBufferExecutionFlags.AsyncCompute);

        densityKernal = meshGenerator.FindKernel("Density");
        marchKernal = meshGenerator.FindKernel("March");
    }

    protected override void OnStartRunning()
    {
        ChunkWorldSingleton chunkWorld = SystemAPI.GetSingleton<ChunkWorldSingleton>();

        // Create compute containers. These will hold the compute buffers while they are being read back
        if (computeContainers.Count > 0) return;
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

        ChunkWorldSingleton chunkWorld = SystemAPI.GetSingleton<ChunkWorldSingleton>();

        // Access list of major chunks
        ref ChunkPlacementSystem chunkGeneratorSystem = ref World.Unmanaged.GetUnsafeSystemRef<ChunkPlacementSystem>(World.Unmanaged.GetExistingUnmanagedSystem<ChunkPlacementSystem>());

        majorChunkLookup.Update(this);
        minorChunkLookup.Update(this);       

        if (commandBufferInFlight && commandBufferFence.passed)
        {
            commandBufferInFlight = false;

            foreach (ComputeContainer container in computeContainers)
            {
                if (container.generationStep == ComputeContainer.MinorChunkProgress.COMMANDS_BUFFERED)
                {
                    container.BeginReadback();
                }
            }
        }

        foreach (ComputeContainer container in computeContainers)
        {
            container.UpdateReadback();

            Entity chunkEntity = container.linkedMinorChunk;

            if (container.generationStep != ComputeContainer.MinorChunkProgress.TRIANGLE_READBACK_PENDING &&
                container.generationStep != ComputeContainer.MinorChunkProgress.EMPTY_MESH &&
                container.generationStep != ComputeContainer.MinorChunkProgress.READBACK_ERROR)
            {
                continue;
            }

            if (chunkEntity == Entity.Null || !SystemAPI.Exists(chunkEntity))
            {
                container.Reset();
                Debug.LogWarning("Invalid Entity");
                continue;
            }

            if (!minorChunkLookup.HasComponent(chunkEntity))
            {
                container.Reset();
                Debug.LogWarning("No MinorChunk Component");
                continue;
            }

            RefRW<MinorChunk> minorChunk = minorChunkLookup.GetRefRW(chunkEntity);

            if (container.generationStep == ComputeContainer.MinorChunkProgress.READBACK_ERROR)
            {
                Debug.LogError("GPU Readback Error");

                SystemAPI.SetComponentEnabled<ChunkGeneratedMesh>(chunkEntity, false);
                SystemAPI.SetComponentEnabled<ChunkGeneratingMesh>(chunkEntity, false);

                container.Reset();
                continue;
            }

            if (container.generationStep == ComputeContainer.MinorChunkProgress.EMPTY_MESH)
            {
                if (minorChunk.ValueRO.meshBlobHighDetail.IsCreated)
                {
                    minorChunk.ValueRW.meshBlobHighDetail.Dispose();
                }

                minorChunk.ValueRW.meshHighDetailValid = false;

                SystemAPI.SetComponentEnabled<ChunkGeneratedMesh>(chunkEntity, true);
                SystemAPI.SetComponentEnabled<ChunkGeneratingMesh>(chunkEntity, false);

                container.Reset();
                continue;
            }

            if (container.generationStep == ComputeContainer.MinorChunkProgress.TRIANGLE_READBACK_PENDING)
            {
                if (!container.readbackRequest.done) continue;

                if (container.readbackRequest.hasError)
                {
                    Debug.LogError("GPU Triangle Readback Error");

                    SystemAPI.SetComponentEnabled<ChunkGeneratedMesh>(chunkEntity, false);
                    SystemAPI.SetComponentEnabled<ChunkGeneratingMesh>(chunkEntity, false);

                    container.Reset();
                    continue;
                }

                NativeArray<Triangle> tris = container.readbackRequest.GetData<Triangle>();

                if (minorChunk.ValueRO.meshBlobHighDetail.IsCreated)
                {
                    minorChunk.ValueRW.meshBlobHighDetail.Dispose();
                }

                minorChunk.ValueRW.meshBlobHighDetail = MeshBlobUtility.CreateMeshBlob(tris, tris.Length);

                minorChunk.ValueRW.meshHighDetailValid = true;

                SystemAPI.SetComponentEnabled<ChunkGeneratedMesh>(chunkEntity, true);
                SystemAPI.SetComponentEnabled<ChunkGeneratingMesh>(chunkEntity, false);

                container.Reset();
            }
        }

        if(!commandBufferInFlight && gameState.chunkGenerationEnable)
        {
            bool recordedAnyCommands = false;

            commandBuffer.Clear();
            commandBuffer.SetExecutionFlags(CommandBufferExecutionFlags.AsyncCompute);

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
                if (minorChunk.ValueRO.mediumChunk == Entity.Null) continue;

                Entity mediumChunk = minorChunk.ValueRO.mediumChunk;

                if (!SystemAPI.IsComponentEnabled<ChunkPopulated>(mediumChunk)) continue;

                ComputeContainer freeContainer = null;

                foreach (ComputeContainer container in computeContainers)
                {
                    if (container.generationStep == ComputeContainer.MinorChunkProgress.CLEARED)
                    {
                        freeContainer = container;
                        break;
                    }
                }

                if (freeContainer == null) break; // No Containers available yet

                freeContainer.Prepare(entity);

                freeContainer.AddCommands(ref commandBuffer, ref meshGenerator, densityKernal, marchKernal, ref chunkWorld, transform.ValueRO.Position);

                SystemAPI.SetComponentEnabled<ChunkGeneratingMesh>(entity, true);

                recordedAnyCommands = true;
            }

            if (recordedAnyCommands)
            {
                commandBufferFence = commandBuffer.CreateGraphicsFence(GraphicsFenceType.AsyncQueueSynchronisation, SynchronisationStageFlags.ComputeProcessing);

                Graphics.ExecuteCommandBufferAsync(commandBuffer, ComputeQueueType.Default);

                commandBufferInFlight = true;
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
        commandBuffer.Dispose();
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

#region Compute Container
public class ComputeContainer
{
    public GraphicsBuffer triangleBuffer;
    public GraphicsBuffer pointBuffer;
    ComputeBuffer triCountBuffer;

    CommandBuffer commandBuffer;

    private int numDensityThreadsPerAxis;
    private int numMarchThreadsPerAxis;
    private int numPointsPerAxis;
    private int numPointsTotal;

    public Entity linkedMinorChunk;

    public AsyncGPUReadbackRequest counterReadbackRequest;
    public AsyncGPUReadbackRequest readbackRequest;
    private int triangleCount;

    public MinorChunkProgress generationStep;

    public enum MinorChunkProgress
    {
        CLEARED,
        BOUND,
        COMMANDS_BUFFERED,
        COUNTER_READBACK_PENDING,
        TRIANGLE_READBACK_PENDING,
        EMPTY_MESH,
        READBACK_ERROR
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

    public void AddCommands(ref CommandBuffer generator, ref ComputeShader computeShader, int densityKernel, int marchKernel, ref ChunkWorldSingleton chunkWorld, float3 position)
    {
        float spacing = (float)chunkWorld.minorChunkSize / (numPointsPerAxis - 1);

        //
        // Density
        // 
        generator.SetComputeBufferParam(computeShader, densityKernel, "points", pointBuffer);
        generator.SetComputeIntParam(computeShader, "numPointsPerAxis", numPointsPerAxis);
        generator.SetComputeVectorParam(computeShader, "position", new Vector4(position.x, position.y, position.z));
        generator.SetComputeIntParam(computeShader, "seed", chunkWorld.seed);
        generator.SetComputeFloatParam(computeShader, "spacing", spacing);

        generator.DispatchCompute(computeShader, densityKernel, numDensityThreadsPerAxis, numDensityThreadsPerAxis, numDensityThreadsPerAxis);

        //
        // March
        // 
        generator.SetBufferCounterValue(triangleBuffer, 0);
        generator.SetComputeBufferParam(computeShader, marchKernel, "points", pointBuffer);
        generator.SetComputeBufferParam(computeShader, marchKernel, "triangles", triangleBuffer);
        generator.SetComputeIntParam(computeShader, "numPointsPerAxis", numPointsPerAxis);
        generator.SetComputeFloatParam(computeShader, "isoLevel", chunkWorld.isoLevel);

        generator.DispatchCompute(computeShader, marchKernel, numMarchThreadsPerAxis, numMarchThreadsPerAxis, numMarchThreadsPerAxis);

        // Size readback
        generator.CopyCounterValue(triangleBuffer, triCountBuffer, 0);

        generationStep = MinorChunkProgress.COMMANDS_BUFFERED;
    }

    public void Generate(ComputeShader meshGenerator, ref ChunkWorldSingleton chunkWorld, float3 position)
    {
        // if(generationStep != MinorChunkProgress.BOUND)
        // {
        //     Debug.LogWarning("Chunk was not bound to the mesh generator");
        //     return;
        // }
        
        // RunDensityShader(meshGenerator, position, ref chunkWorld);

        // RunCubeMarch(meshGenerator, chunkWorld.isoLevel);

        // //Debug.Log("Mesh Generation Completed");
        // generationStep = MinorChunkProgress.MESH_GENERATED;

        // BeginReadback();
        int densityKernal = meshGenerator.FindKernel("Density");
        int marchKernal = meshGenerator.FindKernel("March");
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
        if(generationStep != MinorChunkProgress.COMMANDS_BUFFERED) return;

        counterReadbackRequest = AsyncGPUReadback.Request(triCountBuffer, sizeof(int), 0);

        generationStep = MinorChunkProgress.COUNTER_READBACK_PENDING;
    }

    public void UpdateReadback()
    {
        if (generationStep == MinorChunkProgress.COUNTER_READBACK_PENDING)
        {
            if (!counterReadbackRequest.done) return;

            if (counterReadbackRequest.hasError)
            {
                generationStep = MinorChunkProgress.READBACK_ERROR;
                return;
            }

            NativeArray<int> countData = counterReadbackRequest.GetData<int>();

            triangleCount = countData[0];

            if (triangleCount <= 0)
            {
                generationStep = MinorChunkProgress.EMPTY_MESH;
                return;
            }

            readbackRequest = AsyncGPUReadback.Request(triangleBuffer, triangleCount * UnsafeUtility.SizeOf<Triangle>(), 0);

            generationStep = MinorChunkProgress.TRIANGLE_READBACK_PENDING;
            return;
        }

        if (generationStep == MinorChunkProgress.TRIANGLE_READBACK_PENDING)
        {
            if (!readbackRequest.done)
                return;

            if (readbackRequest.hasError)
            {
                generationStep = MinorChunkProgress.READBACK_ERROR;
                return;
            }
        }
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
#endregion