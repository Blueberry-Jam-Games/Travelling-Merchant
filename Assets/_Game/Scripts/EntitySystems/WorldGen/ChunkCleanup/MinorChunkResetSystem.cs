using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Physics;

[UpdateInGroup(typeof(WorldGenerationSystemGroup))]
[UpdateAfter(typeof(ChunkPlacementSystem))]
partial struct MinorChunkResetSystem : ISystem
{
    private EntityQuery delayedMediumChunkQuery;
    private EntityQuery minorChunkQuery;

    [BurstCompile]
    public void OnCreate(ref SystemState state)
    {
        delayedMediumChunkQuery = SystemAPI.QueryBuilder()
            .WithAll<MediumChunk, ChunkDelayedSleep>()
            .WithDisabled<ChunkActive>()
            .Build();

        // Ignore enabled state so we can reset minors even if some tags
        // are already disabled from another system.
        minorChunkQuery = SystemAPI.QueryBuilder()
            .WithAll<
                MinorChunk,
                Chunk,
                ChunkActive,
                ChunkGeneratedMesh,
                ChunkGeneratedPhysicsCollider,
                ChunkGeneratingMesh,
                PhysicsCollider>()
            .WithOptions(EntityQueryOptions.IgnoreComponentEnabledState)
            .Build();
    }

    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        int delayedMediumCount = delayedMediumChunkQuery.CalculateEntityCount();

        if (delayedMediumCount == 0) return;

        NativeParallelHashSet<Entity> delayedMediumChunks = new NativeParallelHashSet<Entity>(delayedMediumCount, Allocator.TempJob);

        JobHandle collectHandle = new CollectDelayedMediumChunksJob{DelayedMediumChunks = delayedMediumChunks.AsParallelWriter()}.ScheduleParallel(delayedMediumChunkQuery, state.Dependency);

        JobHandle resetMinorHandle = new ResetMinorChunksJob{DelayedMediumChunks = delayedMediumChunks}.ScheduleParallel(minorChunkQuery, collectHandle);

        JobHandle clearMediumDelayHandle = new ClearMediumDelayedSleepJob{}.ScheduleParallel(delayedMediumChunkQuery, resetMinorHandle);

        state.Dependency = delayedMediumChunks.Dispose(clearMediumDelayHandle);
    }

    [BurstCompile]
    partial struct CollectDelayedMediumChunksJob : IJobEntity
    {
        public NativeParallelHashSet<Entity>.ParallelWriter DelayedMediumChunks;

        private void Execute(Entity entity)
        {
            DelayedMediumChunks.Add(entity);
        }
    }

    [BurstCompile]
    partial struct ResetMinorChunksJob : IJobEntity
    {
        [ReadOnly]
        public NativeParallelHashSet<Entity> DelayedMediumChunks;

        private void Execute(
            ref MinorChunk minorChunk,
            ref PhysicsCollider physicsCollider,
            EnabledRefRW<ChunkActive> chunkActive,
            EnabledRefRW<ChunkGeneratedMesh> chunkGeneratedMesh,
            EnabledRefRW<ChunkGeneratedPhysicsCollider> chunkGeneratedPhysicsCollider,
            EnabledRefRW<ChunkGeneratingMesh> chunkGeneratingMesh)
        {
            if (!DelayedMediumChunks.Contains(minorChunk.mediumChunk)) return;

            chunkActive.ValueRW = false;
            chunkGeneratedMesh.ValueRW = false;
            chunkGeneratedPhysicsCollider.ValueRW = false;
            chunkGeneratingMesh.ValueRW = false;

            minorChunk.mediumChunk = Entity.Null;
            minorChunk.majorChunk = Entity.Null;

            minorChunk.meshHighDetailValid = false;
            minorChunk.meshLowDetailValid = false;

            if(physicsCollider.Value.IsCreated) physicsCollider.Value.Dispose();
            physicsCollider.Value = default;
        }
    }

    [BurstCompile]
    partial struct ClearMediumDelayedSleepJob : IJobEntity
    {
        private void Execute(EnabledRefRW<ChunkDelayedSleep> chunkDelayedSleep)
        {
            chunkDelayedSleep.ValueRW = false;
        }
    }

    [BurstCompile]
    public void OnDestroy(ref SystemState state)
    {

    }
}