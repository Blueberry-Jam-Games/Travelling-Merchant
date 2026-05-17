using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;
using Unity.Physics;
using Unity.Collections.LowLevel.Unsafe;
using System.Collections.Generic;
using Unity.Collections;

[UpdateInGroup(typeof(PresentationSystemGroup))]
[BurstCompile(DisableDirectCall = true)]
partial class ChunkMeshSystem : SystemBase
{
    UnityEngine.Material terrainMaterial;
    const int threadGroupSize = 8;
    public ComputeShader meshGenerator; // New mesh system
    public ComputeShader marchingCubeShader;
    public ComputeShader heightmapShader;
    public ComputeShader densityShader;
    public DensityGenerator densityGenerator;

    List<ComputeContainer> computeContainers = new List<ComputeContainer>();

    ComputeBuffer heightmapBuffer;

    ComputeBuffer triangleBuffer;
    ComputeBuffer pointsBuffer;
    ComputeBuffer triCountBuffer;

    ComputeBuffer cubeMarchArray;

    ComponentLookup<MajorChunk> majorChunkLookup;
    
    ComponentLookup<Chunk> chunkLookup; // New Code
    ComponentLookup<PhysicsCollider> physicsColliderLookup; // New Code

    HeightmapGenerator heightmapper;
    DensityFromHeightmapGenerator densityFromHeightmapGenerator;

    protected override void OnCreate()
    {
        marchingCubeShader = Resources.Load<ComputeShader>("ComputeShaders/MarchingCubes");
        heightmapShader = Resources.Load<ComputeShader>("ComputeShaders/MinorChunkHeightmap");
        densityShader = Resources.Load<ComputeShader>("ComputeShaders/DensityFromHeightmap");
        meshGenerator = Resources.Load<ComputeShader>("ComputeShaders/MeshGeneration"); // New Shader

        if (marchingCubeShader == null) Debug.LogError("Missing shader"); // Old
        //if (densityGenerator.densityShader == null) Debug.LogError("Missing Density Shader"); // Old

        if (meshGenerator == null) Debug.LogError("Missing Mesh Generation Shader File");

        if(meshGenerator != null)
        {
            cubeMarchArray = new ComputeBuffer(4096, sizeof(int), ComputeBufferType.Structured);
            cubeMarchArray.SetData(CubeMarchTriangulation.map);
        }

        int kernal = meshGenerator.FindKernel("March"); // New Code
        meshGenerator.SetBuffer(kernal, "cubeMarchArray", cubeMarchArray); // New code

        majorChunkLookup = GetComponentLookup<MajorChunk>(isReadOnly: true);

        chunkLookup = GetComponentLookup<Chunk>(false);
        physicsColliderLookup = GetComponentLookup<PhysicsCollider>(false);

        //heightmapper = new HeightmapGenerator(heightmapShader);
        //densityFromHeightmapGenerator = new DensityFromHeightmapGenerator(densityShader);
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
        Entity chunkWorldEntity = SystemAPI.GetSingletonEntity<ChunkWorldSingleton>();

        if(pointsBuffer == null || triangleBuffer == null || triCountBuffer == null || heightmapBuffer == null)
        {
            CreateBuffers(chunkWorld.numPointsPerAxis);
        }

        // Access list of major chunks
        ref ChunkGeneratorSystem chunkGeneratorSystem = ref World.Unmanaged.GetUnsafeSystemRef<ChunkGeneratorSystem>(World.Unmanaged.GetExistingUnmanagedSystem<ChunkGeneratorSystem>());

        majorChunkLookup.Update(this);

        chunkLookup.Update(this);
        physicsColliderLookup.Update(this);

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
            // float4 heightmapCorners;
            // float4 xgradients;
            // float4 ygradients;
            if(chunkGeneratorSystem.majorChunkExists(chunk.ValueRO.majorChunkPosition))
            {
                Entity majorChunk = chunkGeneratorSystem.getMajorChunkEntity(chunk.ValueRO.majorChunkPosition);
                if(SystemAPI.IsComponentEnabled<MajorChunkReady>(majorChunk))
                {
                    if(!majorChunkLookup.TryGetComponent(majorChunk, out MajorChunk output))
                    {
                        Debug.LogError("No Component");
                        continue;
                    }

                    // int2 chunkPosition = chunk.ValueRO.chunkPosition.xz;
                    // chunkPosition = chunkPosition % chunkWorld.MinorPerMajorChunk;
                    // if(chunkPosition.x < 0) chunkPosition.x += chunkWorld.MinorPerMajorChunk;
                    // if(chunkPosition.y < 0) chunkPosition.y += chunkWorld.MinorPerMajorChunk;

                    // heightmapCorners = output.Quad(chunkPosition);
                    // xgradients = output.xgradients(chunkPosition);
                    // ygradients = output.ygradients(chunkPosition);

                    // heightmapper.Generate(ref heightmapBuffer, heightmapCorners, xgradients, ygradients, chunkWorld.numPointsPerAxis, chunkWorld.chunkSize, chunk.ValueRO.chunkPosition, 1, 1);

                    // //Density
                    // int numPointsPerAxis = chunkWorld.numPointsPerAxis;
                    // int boundsSize = chunkWorld.chunkSize;
                    // Vector4 offset = Vector4.zero;

                    // // Imported
                    // int numVoxelsPerAxis = numPointsPerAxis - 1;
                    // int numThreadsPerAxis = Mathf.CeilToInt(numVoxelsPerAxis / (float)threadGroupSize);
                    // float pointSpacing = (float) boundsSize / (float) (numPointsPerAxis - 1);

                    // int3 coord = chunk.ValueRO.chunkPosition;
                    // Vector3 centre = CornerFromCoords(coord, boundsSize);

                    // densityGenerator.Generate(pointsBuffer, numPointsPerAxis, boundsSize, centre, offset, pointSpacing);

                    // densityFromHeightmapGenerator.Generate(ref heightmapBuffer, pointsBuffer, numPointsPerAxis, boundsSize, centre, pointSpacing, 1);

                    // int marchKernel = marchingCubeShader.FindKernel("March");

                    // marchingCubeShader.SetBuffer(marchKernel, "points", pointsBuffer);
                    // marchingCubeShader.SetBuffer(marchKernel, "triangles", triangleBuffer);
                    // triangleBuffer.SetCounterValue(0);
                    // marchingCubeShader.SetInt("numPointsPerAxis", numPointsPerAxis);
                    // marchingCubeShader.SetFloat("isoLevel", chunkWorld.isoLevel);
                    // marchingCubeShader.SetBuffer(marchKernel, "cubeMarchArray", cubeMarchArray);

                    // marchingCubeShader.Dispatch(marchKernel, numThreadsPerAxis, numThreadsPerAxis, numThreadsPerAxis);

                    bool computeShadersOccupied = true;
                    foreach(ComputeContainer container in computeContainers)
                    {
                        if(container.generationStep == ComputeContainer.MinorChunkProgress.CLEARED)
                        {
                            computeShadersOccupied = false;
                            container.Prepare(entity);

                            container.Generate(meshGenerator, ref chunkWorld, chunk.ValueRO.chunkPosition * chunkWorld.chunkSize);

                            SystemAPI.SetComponentEnabled<ChunkRequestUpdate>(entity, false);

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
            Entity chunkEntity = container.linkedChunk;
            
            if(container.generationStep == ComputeContainer.MinorChunkProgress.CLEARED || container.generationStep == ComputeContainer.MinorChunkProgress.BOUND) continue;

            if (chunkEntity == Entity.Null || !SystemAPI.Exists(chunkEntity))
            {
                container.Reset();
                Debug.LogWarning("No Entity");
                continue;
            }

            if (!chunkLookup.HasComponent(chunkEntity))
            {
                container.Reset();
                Debug.LogWarning("No Chunk Component");
                continue;
            }

            if (!physicsColliderLookup.HasComponent(chunkEntity))
            {
                container.Reset();
                Debug.LogWarning("No Physics Collider");
                continue;
            }
            
            RefRW<Chunk> chunk = chunkLookup.GetRefRW(chunkEntity);
            
            RefRW<PhysicsCollider> physicsCollider = physicsColliderLookup.GetRefRW(chunkEntity);

            if(container.generationStep == ComputeContainer.MinorChunkProgress.EMPTY_MESH)
            {
                Mesh mesh = ChunkMeshManagerSystem.Instance.GetMesh(chunk.ValueRO.meshIndex);
                mesh.Clear();
                ChunkMeshManagerSystem.Instance.SetMesh(mesh, chunk.ValueRO.meshIndex);

                if (physicsCollider.ValueRW.Value.IsCreated)
                    physicsCollider.ValueRW.Value.Dispose();

                container.Reset();
                continue;
            }
            else if(container.generationStep == ComputeContainer.MinorChunkProgress.READBACK_PENDING && container.readbackRequest.done)
            {
                if(container.readbackRequest.hasError)
                {
                    Debug.LogError("GPU Readback Error");
                    container.Reset();
                    return;
                }

                NativeArray<Triangle> tris = container.readbackRequest.GetData<Triangle>();

                BlobAssetReference<MeshBlob> blob = MeshBlobUtility.CreateMeshBlob(tris, tris.Length);

                chunk.ValueRW.meshBlob = blob;

                container.Reset();

                Mesh mesh = ChunkMeshManagerSystem.Instance.GetMesh(chunk.ValueRO.meshIndex);
                mesh.Clear();

                ref MeshBlob meshBlob = ref chunk.ValueRO.meshBlob.Value;

                Vector3[] vertices = new Vector3[meshBlob.Vertices.Length];
                int[] triangles = new int[meshBlob.Triangles.Length];

                for(int i = 0; i < meshBlob.Vertices.Length; i++)
                {
                    float3 v = meshBlob.Vertices[i];
                    vertices[i] = new Vector3(v.x, v.y, v.z);
                }

                for(int i = 0; i < meshBlob.Triangles.Length; i++)
                {
                    triangles[i] = meshBlob.Triangles[i];
                }

                mesh.vertices = vertices;
                mesh.triangles = triangles;
                
                mesh.RecalculateNormals();
                mesh.RecalculateTangents();
                mesh.RecalculateBounds();

                ChunkMeshManagerSystem.Instance.SetMesh(mesh, chunk.ValueRO.meshIndex);

                if (physicsCollider.ValueRW.Value.IsCreated)
                    physicsCollider.ValueRW.Value.Dispose();

                physicsCollider.ValueRW.Value = Unity.Physics.MeshCollider.Create(mesh, CollisionFilter.Default, Unity.Physics.Material.Default);
            }

            // // Get number of triangles in the triangle buffer
            // ComputeBuffer.CopyCount(triangleBuffer, triCountBuffer, 0);
            // int[] triCountArray = { 0 };
            // triCountBuffer.GetData(triCountArray);
            // int numTris = triCountArray[0];

            // if(numTris == 0)
            // {
            //     ChunkMeshManagerSystem.Instance.SetMesh(new Mesh(), chunk.ValueRO.meshIndex);

            //     if (physicsCollider.ValueRW.Value.IsCreated)
            //         physicsCollider.ValueRW.Value.Dispose();

            //     continue;
            // }

            // // Get triangle data from shader
            // Triangle[] tris = new Triangle[numTris];
            // triangleBuffer.GetData(tris, 0, 0, numTris);

            // Mesh mesh = ChunkMeshManagerSystem.Instance.GetMesh(chunk.ValueRO.meshIndex);
            // mesh.Clear();

            // //chunk.ValueRW.mesh = MeshBlobUtility.CreateMeshBlob(tris)

            // // Move data into arrays. * 3 used for 3 verticies per face 
            // var vertices = new Vector3[numTris * 3];
            // var meshTriangles = new int[numTris * 3];

            // for (int i = 0; i < numTris; i++)
            // {
            //     for (int j = 0; j < 3; j++)
            //     {
            //         meshTriangles[i * 3 + j] = i * 3 + j;
            //         vertices[i * 3 + j] = tris[i][j];
            //     }
            // }
        }

        foreach (var (chunk, transform, entity) in SystemAPI.Query<RefRO<Chunk>, RefRO<LocalTransform>>().WithNone<ChunkSleeping>().WithEntityAccess())
        {
            Mesh mesh = ChunkMeshManagerSystem.Instance.GetMesh(chunk.ValueRO.meshIndex);

            if (mesh == null)
                continue;

            if (mesh.vertexCount == 0 || mesh.triangles.Length == 0)
                continue;

            if (!mesh.HasVertexAttribute(UnityEngine.Rendering.VertexAttribute.Position))
                continue;

            Graphics.RenderMesh(new RenderParams(terrainMaterial), ChunkMeshManagerSystem.Instance.GetMesh(chunk.ValueRO.meshIndex), 0, transform.ValueRO.ToMatrix());
        }
    }

    float3 CornerFromCoords(int3 coord, float boundsSize)
    {

        return new float3(coord.x, coord.y, coord.z) * boundsSize;
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
        heightmapBuffer = new ComputeBuffer(numPointsPerAxis * numPointsPerAxis, sizeof(float));
    }


    protected override void OnDestroy()
    {
        if(triangleBuffer.IsValid())
        {
            triangleBuffer.Dispose();
            pointsBuffer.Dispose();
            triCountBuffer.Dispose();
            heightmapBuffer.Dispose();
        }
        base.OnDestroy();
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

        Debug.Log("Mesh Generation Completed");
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
        int octaves = 3;

        float lacunarity = 1.0f;
        float persistence = 0.3f;

        float noiseScale = 0.1f;

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