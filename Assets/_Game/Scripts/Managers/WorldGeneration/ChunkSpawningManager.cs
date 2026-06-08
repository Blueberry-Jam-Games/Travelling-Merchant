using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;
using Unity.Physics;

public class ChunkSpawningManager : MonoBehaviour
{
    public GameObject MediumChunkPrefab;
    [Header("Targets")]
    public int targetMajorChunkCount;
    [SerializeField]
    private int currentMajorChunkCount;
    [Space]

    public int targetMediumChunkCount;
    [SerializeField]
    private int currentMediumChunkCount;
    [Space]

    public int targetMinorChunkCount;
    [SerializeField]
    private int currentMinorChunkCount;

    // Prototypes (created once)
    private Entity majorPrototype;
    private Entity mediumPrototype;
    private Entity minorPrototype;

    private EntityCommandBuffer.ParallelWriter ecbMinorChunk;
    private EntityCommandBuffer.ParallelWriter ecbMediumChunk;
    private EntityCommandBuffer.ParallelWriter ecbMajorChunk;

    private EntityManager entityManager;
    private BeginSimulationEntityCommandBufferSystem ecbSystem;

    void Start()
    {
        World world = World.DefaultGameObjectInjectionWorld;
        entityManager = world.EntityManager;

        ecbSystem = world.GetOrCreateSystemManaged<BeginSimulationEntityCommandBufferSystem>();

        // Build archetypes
        var majorArchtype = entityManager.CreateArchetype(
            typeof(LocalTransform),
            typeof(ChunkSleeping),
            typeof(ChunkRequestUpdate),
            typeof(MajorChunkReady),
            typeof(ChunkT), // TODO Change to CHUNK
            typeof(MajorChunk),
            typeof(ChunkGenerationComplete)
        );

        var mediumArchtype = entityManager.CreateArchetype(
            typeof(LocalTransform),
            typeof(ChunkSleeping),
            typeof(ChunkRequestUpdate),
            typeof(ChunkT), // TODO Change to CHUNK
            typeof(MediumChunk),
            typeof(ChunkGenerationComplete)
        );

        var minorArchtype = entityManager.CreateArchetype(
            typeof(LocalTransform),
            typeof(Chunk),
            typeof(ChunkSleeping),
            typeof(ChunkRequestUpdate),
            typeof(ChunkRequestPhysics),
            typeof(PhysicsCollider),
            typeof(PhysicsWorldIndex),
            typeof(ChunkT), // TODO Change to CHUNK
            typeof(MinorChunk),
            typeof(ChunkGenerationComplete)
        );

        // Create prototypes
        majorPrototype = entityManager.CreateEntity(majorArchtype);
        mediumPrototype = entityManager.CreateEntity(mediumArchtype);
        minorPrototype = entityManager.CreateEntity(minorArchtype);

        entityManager.SetComponentEnabled<ChunkSleeping>(majorPrototype, true);
        entityManager.SetComponentEnabled<ChunkRequestUpdate>(majorPrototype, false);
        entityManager.SetComponentEnabled<MajorChunkReady>(majorPrototype, false);
        entityManager.SetComponentData(majorPrototype, LocalTransform.Identity);
        entityManager.SetComponentData(majorPrototype, new MajorChunk
        {
            generationStep = -1,
            chunkPosition = new int2(5, 5)
        });

        entityManager.SetComponentData(minorPrototype, LocalTransform.Identity);
        entityManager.SetComponentEnabled<ChunkSleeping>(minorPrototype, true);
        entityManager.SetComponentEnabled<ChunkRequestUpdate>(minorPrototype, false);
        entityManager.SetComponentEnabled<ChunkRequestPhysics>(minorPrototype, false);

        entityManager.SetName(majorPrototype, "MajorPrototype");
        entityManager.SetName(mediumPrototype, "MediumPrototype");
        entityManager.SetName(minorPrototype, "MinorPrototype");
    }

    void Update()
    {
        JobHandle combinedHandle = default;

        if (targetMinorChunkCount != currentMinorChunkCount)
        {
            ecbMinorChunk =
                ecbSystem.CreateCommandBuffer().AsParallelWriter();

            combinedHandle =
                JobHandle.CombineDependencies(
                    combinedHandle,
                    HandleMinor());
        }

        if (targetMediumChunkCount != currentMediumChunkCount)
        {
            ecbMediumChunk =
                ecbSystem.CreateCommandBuffer().AsParallelWriter();

            combinedHandle =
                JobHandle.CombineDependencies(
                    combinedHandle,
                    HandleMedium());
        }

        if (targetMajorChunkCount != currentMajorChunkCount)
        {
            ecbMajorChunk =
                ecbSystem.CreateCommandBuffer().AsParallelWriter();

            combinedHandle =
                JobHandle.CombineDependencies(
                    combinedHandle,
                    HandleMajor());
        }

        ecbSystem.AddJobHandleForProducer(combinedHandle);
    }

    // ----------------------------
    // GENERIC JOBS
    // ----------------------------

    [BurstCompile]
    public struct SpawnJob : IJobParallelFor
    {
        public Entity Prototype;
        public enum ChunkArchtype
        {
            MINOR,
            MEDIUM,
            MAJOR
        }
        public NativeArray<int> meshIndexes;
        public ChunkArchtype chunkType;
        public NativeArray<UnityObjectRef<MediumChunkMirror>> mediumChunkMirrorArray;
        
        public EntityCommandBuffer.ParallelWriter ECB;

        public void Execute(int index)
        {
            var e = ECB.Instantiate(index, Prototype);
            ECB.SetComponent(index, e, LocalTransform.Identity);

            if(chunkType == ChunkArchtype.MINOR)
            {
                ECB.SetComponent(index, e, new Chunk
                {
                    chunkPosition = new int3(100, 100, 100),
                    meshIndex = meshIndexes[index]
                });
                ECB.SetName(index, e, "Minor Chunk");
            }
            else if(chunkType == ChunkArchtype.MEDIUM)
            {
                ECB.SetComponent(index, e, new MediumChunk
                {
                    mediumChunkMirror = mediumChunkMirrorArray[index]
                });
                ECB.SetName(index, e, "MediumChunk");
                DynamicBuffer<RequestedMinorChunkData> data = ECB.AddBuffer<RequestedMinorChunkData>(index, e);
                data.EnsureCapacity(256);
            }
            else if(chunkType == ChunkArchtype.MAJOR)
            {
                ECB.SetName(index, e, "MajorChunk");
            }
        }
    }

    [BurstCompile]
    public struct DestroyJob : IJobParallelFor
    {
        [ReadOnly] public NativeArray<Entity> Entities;
        public EntityCommandBuffer.ParallelWriter ECB;

        public void Execute(int index)
        {
            ECB.DestroyEntity(index, Entities[index]);
        }
    }

    // ----------------------------
    // MAJOR
    // ----------------------------

    JobHandle HandleMajor()
    {
        int delta = targetMajorChunkCount - currentMajorChunkCount;

        if (delta > 0)
        {
            currentMajorChunkCount = targetMajorChunkCount;
            
            NativeArray<UnityObjectRef<MediumChunkMirror>> mediumChunkMirrors = new NativeArray<UnityObjectRef<MediumChunkMirror>>(0, Allocator.TempJob);
            NativeArray<int> newMeshIndexes = new NativeArray<int>(0, Allocator.TempJob);

            JobHandle handle = new SpawnJob
            {
                Prototype = majorPrototype,
                ECB = ecbMajorChunk,
                chunkType = SpawnJob.ChunkArchtype.MAJOR,
                meshIndexes = newMeshIndexes,
                mediumChunkMirrorArray = mediumChunkMirrors
            }.Schedule(delta, 64);

            newMeshIndexes.Dispose(handle);
            mediumChunkMirrors.Dispose(handle);

            return handle;
        }
        else if (delta < 0)
        {
            
        }

        return default;
    }

    // ----------------------------
    // MEDIUM (placeholder)
    // ----------------------------

    JobHandle HandleMedium()
    {
        int delta = targetMediumChunkCount - currentMediumChunkCount;

        if (delta > 0)
        {
            currentMediumChunkCount = targetMediumChunkCount;

            NativeArray<UnityObjectRef<MediumChunkMirror>> mediumChunkMirrors = new NativeArray<UnityObjectRef<MediumChunkMirror>>(delta, Allocator.TempJob);
            NativeArray<int> newMeshIndexes = new NativeArray<int>(0, Allocator.TempJob);

            for(int i = 0; i < delta; i++)
            {
                mediumChunkMirrors[i] = Instantiate(MediumChunkPrefab).GetComponent<MediumChunkMirror>();
            }

            JobHandle handle = new SpawnJob
            {
                Prototype = mediumPrototype,
                ECB = ecbMediumChunk,
                chunkType = SpawnJob.ChunkArchtype.MEDIUM,
                meshIndexes = newMeshIndexes,
                mediumChunkMirrorArray = mediumChunkMirrors
            }.Schedule(delta, 64);

            newMeshIndexes.Dispose(handle);
            mediumChunkMirrors.Dispose(handle);

            return handle;
        }
        else if (delta < 0)
        {
            
        }

        return default;
    }

    // ----------------------------
    // MINOR
    // ----------------------------
    JobHandle HandleMinor()
    {
        int delta = targetMinorChunkCount - currentMinorChunkCount;

        if (delta > 0)
        {
            currentMinorChunkCount = targetMinorChunkCount;

            NativeArray<UnityObjectRef<MediumChunkMirror>> mediumChunkMirrors = new NativeArray<UnityObjectRef<MediumChunkMirror>>(0, Allocator.TempJob);
            NativeArray<int> newMeshIndexes = new NativeArray<int>(delta, Allocator.TempJob);

            for(int i = 0; i < newMeshIndexes.Length; i++)
            {
                newMeshIndexes[i] = ChunkMeshManagerSystem.Instance.CreateEmptyMesh();
            }
            
            JobHandle handle = new SpawnJob
            {
                Prototype = minorPrototype,
                ECB = ecbMinorChunk,
                chunkType = SpawnJob.ChunkArchtype.MINOR,
                meshIndexes = newMeshIndexes,
                mediumChunkMirrorArray = mediumChunkMirrors
            }.Schedule(delta, 64);

            newMeshIndexes.Dispose(handle);
            mediumChunkMirrors.Dispose(handle);

            return handle;
        }
        else if (delta < 0)
        {

        }

        return default;
    }
}