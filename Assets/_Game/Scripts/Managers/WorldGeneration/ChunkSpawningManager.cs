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
            typeof(ChunkActive),
            typeof(ChunkRequestUpdate),
            typeof(Chunk),
            typeof(MajorChunk),
            typeof(ChunkGenerationComplete)
        );

        var mediumArchtype = entityManager.CreateArchetype(
            typeof(LocalTransform),
            typeof(ChunkActive),
            typeof(ChunkRequestUpdate),
            typeof(Chunk),
            typeof(MediumChunk),
            typeof(ChunkGenerationComplete),
            typeof(ChunkParentGenerated)
        );

        var minorArchtype = entityManager.CreateArchetype(
            typeof(LocalTransform),
            typeof(Chunk),
            typeof(ChunkActive),
            typeof(ChunkRequestUpdate),
            typeof(ChunkRequestPhysics),
            typeof(PhysicsCollider),
            typeof(PhysicsWorldIndex),
            typeof(MinorChunk),
            typeof(ChunkGenerationComplete),
            typeof(ChunkParentGenerated)
        );

        // Create prototypes
        majorPrototype = entityManager.CreateEntity(majorArchtype);
        mediumPrototype = entityManager.CreateEntity(mediumArchtype);
        minorPrototype = entityManager.CreateEntity(minorArchtype);

        entityManager.SetComponentEnabled<ChunkActive>(majorPrototype, false);
        entityManager.SetComponentEnabled<ChunkRequestUpdate>(majorPrototype, false);
        entityManager.SetComponentEnabled<ChunkGenerationComplete>(majorPrototype, false);
        entityManager.SetComponentEnabled<Chunk>(majorPrototype, false);
        entityManager.SetComponentData(majorPrototype, LocalTransform.Identity);
        entityManager.SetComponentData(majorPrototype, new Chunk
        {
            position = new int3(5, 5, 5)
        });

        entityManager.SetComponentData(mediumPrototype, LocalTransform.Identity);
        entityManager.SetComponentEnabled<ChunkGenerationComplete>(mediumPrototype, false);
        entityManager.SetComponentEnabled<ChunkActive>(mediumPrototype, false);
        entityManager.SetComponentEnabled<ChunkRequestUpdate>(mediumPrototype, false);
        entityManager.SetComponentEnabled<ChunkParentGenerated>(mediumPrototype, false);
        entityManager.SetComponentEnabled<Chunk>(mediumPrototype, false);
        entityManager.SetComponentData(mediumPrototype, new Chunk
        {
            position = new int3(5, 5, 5)
        });

        entityManager.SetComponentData(minorPrototype, LocalTransform.Identity);
        entityManager.SetComponentEnabled<ChunkActive>(minorPrototype, false);
        entityManager.SetComponentEnabled<ChunkRequestUpdate>(minorPrototype, false);
        entityManager.SetComponentEnabled<ChunkRequestPhysics>(minorPrototype, false);
        entityManager.SetComponentEnabled<ChunkParentGenerated>(minorPrototype, false);
        entityManager.SetComponentEnabled<Chunk>(minorPrototype, false);

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

            ECB.SetComponentEnabled<Chunk>(index, e, true);

            if(chunkType == ChunkArchtype.MINOR)
            {
                ECB.SetComponent(index, e, new Chunk
                {
                    position = new int3(100, 100, 100)
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