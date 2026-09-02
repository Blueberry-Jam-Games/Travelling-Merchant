using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;
using Unity.Physics;
using Unity.VisualScripting;
using System;

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
            typeof(Chunk),
            typeof(MajorChunk),
            typeof(ChunkActive),
            typeof(ChunkPopulated),
            typeof(ChunkDelayedSleep),
            typeof(ChunkPopulating),
            typeof(ChunkConfigured)
        );

        var mediumArchtype = entityManager.CreateArchetype(
            typeof(LocalTransform),
            typeof(Chunk),
            typeof(MediumChunk),
            typeof(ChunkActive),
            typeof(ChunkPopulated),
            typeof(ChunkGeneratedMesh),
            typeof(EntityHasMirror),
            typeof(ChunkContainsParentLink),
            typeof(ChunkDelayedSleep)
        );

        var minorArchtype = entityManager.CreateArchetype(
            typeof(LocalTransform),
            typeof(Chunk),
            typeof(MinorChunk),
            typeof(PhysicsCollider),
            typeof(PhysicsWorldIndex),
            typeof(ChunkActive),
            typeof(ChunkGeneratedMesh),
            typeof(ChunkGeneratedPhysicsCollider),
            typeof(ChunkGeneratingMesh)
        );

        // Create prototypes
        majorPrototype = entityManager.CreateEntity(majorArchtype);
        mediumPrototype = entityManager.CreateEntity(mediumArchtype);
        minorPrototype = entityManager.CreateEntity(minorArchtype);

        #region Major Prototype
        entityManager.SetComponentData(majorPrototype, LocalTransform.Identity);
        entityManager.SetComponentEnabled<Chunk>(majorPrototype, false);
        entityManager.SetComponentData(majorPrototype, new Chunk
        {
            position = new int3(5, 5, 5)
        });
        entityManager.SetComponentEnabled<ChunkActive>(majorPrototype, false);
        entityManager.SetComponentEnabled<ChunkPopulated>(majorPrototype, false);
        entityManager.SetComponentEnabled<ChunkDelayedSleep>(majorPrototype, false);
        entityManager.SetComponentEnabled<ChunkPopulating>(majorPrototype, false);
        entityManager.SetComponentEnabled<ChunkConfigured>(majorPrototype, false);
        #endregion

        #region Medium Prototype
        entityManager.SetComponentData(mediumPrototype, LocalTransform.Identity);
        entityManager.SetComponentEnabled<Chunk>(mediumPrototype, false);
        entityManager.SetComponentData(mediumPrototype, new Chunk
        {
            position = new int3(5, 5, 5)
        });
        entityManager.SetComponentData(mediumPrototype, new MediumChunk
        {
            majorChunk = Entity.Null,
            LoD_0 = MediumChunk.MeshTransferState.NOT_READY,
            LoD_1 = MediumChunk.MeshTransferState.NOT_READY,
            LoD_2 = MediumChunk.MeshTransferState.NOT_READY,
            LoD_3 = MediumChunk.MeshTransferState.NOT_READY 
        });
        entityManager.SetComponentEnabled<ChunkActive>(mediumPrototype, false);
        entityManager.SetComponentEnabled<ChunkPopulated>(mediumPrototype, false);
        entityManager.SetComponentEnabled<ChunkGeneratedMesh>(mediumPrototype, false);
        entityManager.SetComponentEnabled<EntityHasMirror>(mediumPrototype, false);
        entityManager.SetComponentEnabled<ChunkContainsParentLink>(mediumPrototype, false);
        entityManager.SetComponentEnabled<ChunkDelayedSleep>(mediumPrototype, false);
        #endregion

        #region Minor Prototype
        entityManager.SetComponentData(minorPrototype, LocalTransform.Identity);
        entityManager.SetComponentEnabled<Chunk>(minorPrototype, false);
        entityManager.SetComponentData(minorPrototype, new MinorChunk
        {
            mediumChunk = Entity.Null,
            majorChunk = Entity.Null,
            meshHighDetailValid = false,
            meshLowDetailValid = false
        });
        entityManager.SetComponentEnabled<ChunkActive>(minorPrototype, false);
        entityManager.SetComponentEnabled<ChunkGeneratedMesh>(minorPrototype, false);
        entityManager.SetComponentEnabled<ChunkGeneratedPhysicsCollider>(minorPrototype, false);
        entityManager.SetComponentEnabled<ChunkGeneratingMesh>(minorPrototype, false);
        #endregion

        entityManager.SetName(majorPrototype, "MajorPrototype");
        entityManager.SetName(mediumPrototype, "MediumPrototype");
        entityManager.SetName(minorPrototype, "MinorPrototype");
    }

    void FixedUpdate()
    {
        JobHandle combinedHandle = default;

        if (targetMinorChunkCount != currentMinorChunkCount)
        {
            ecbMinorChunk = ecbSystem.CreateCommandBuffer().AsParallelWriter();

            combinedHandle = JobHandle.CombineDependencies(combinedHandle, HandleMinor());
        }

        if (targetMediumChunkCount != currentMediumChunkCount)
        {
            ecbMediumChunk = ecbSystem.CreateCommandBuffer().AsParallelWriter();

            combinedHandle = JobHandle.CombineDependencies(combinedHandle, HandleMedium());
        }

        if (targetMajorChunkCount != currentMajorChunkCount)
        {
            ecbMajorChunk = ecbSystem.CreateCommandBuffer().AsParallelWriter();

            combinedHandle = JobHandle.CombineDependencies(combinedHandle, HandleMajor());
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
        public ChunkArchtype chunkType;
        
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
                ECB.SetName(index, e, "Minor Chunk ");
            }
            else if(chunkType == ChunkArchtype.MEDIUM)
            {
                ECB.SetName(index, e, "MediumChunk");
            }
            else if(chunkType == ChunkArchtype.MAJOR)
            {
                ECB.SetName(index, e, "MajorChunk");
            }
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

            JobHandle handle = new SpawnJob
            {
                Prototype = majorPrototype,
                ECB = ecbMajorChunk,
                chunkType = SpawnJob.ChunkArchtype.MAJOR,
            }.Schedule(delta, 64);

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
                MediumChunkMirrorRegistry.pendingMirrors.Enqueue(Instantiate(MediumChunkPrefab).GetComponent<MediumChunkMirror>());
            }

            JobHandle handle = new SpawnJob
            {
                Prototype = mediumPrototype,
                ECB = ecbMediumChunk,
                chunkType = SpawnJob.ChunkArchtype.MEDIUM,
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
            
            JobHandle handle = new SpawnJob
            {
                Prototype = minorPrototype,
                ECB = ecbMinorChunk,
                chunkType = SpawnJob.ChunkArchtype.MINOR,
            }.Schedule(delta, 64);

            mediumChunkMirrors.Dispose(handle);

            return handle;
        }
        else if (delta < 0)
        {

        }

        return default;
    }
}