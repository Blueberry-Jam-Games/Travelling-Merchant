using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;

[BurstCompile]
[UpdateInGroup(typeof(FixedStepSimulationSystemGroup))]
[UpdateBefore(typeof(Unity.Physics.Systems.BuildPhysicsWorld))]
public partial struct ChunkPhysicsBuildSystem : ISystem
{
    [BurstCompile]
    public void OnCreate(ref SystemState state)
    {
        var query = SystemAPI.QueryBuilder()
            .WithAll<PhysicsCollider, MinorChunk>()
            .WithDisabled<ChunkGeneratedPhysicsCollider>()
            .Build();

        state.RequireForUpdate(query);
    }

    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        var ecbSingleton =
            SystemAPI.GetSingleton<
                EndFixedStepSimulationEntityCommandBufferSystem.Singleton>();

        var job = new BuildPhysicsJob
        {
            ECB = ecbSingleton
                .CreateCommandBuffer(state.WorldUnmanaged)
                .AsParallelWriter()
        };

        state.Dependency = job.ScheduleParallel(state.Dependency);
    }

    [BurstCompile]
    [WithDisabled(typeof(ChunkGeneratedPhysicsCollider))]
    public partial struct BuildPhysicsJob : IJobEntity
    {
        public EntityCommandBuffer.ParallelWriter ECB;

        private void Execute(
            ref PhysicsCollider physicsCollider,
            ref MinorChunk chunk,
            EnabledRefRW<ChunkGeneratedPhysicsCollider> requestPhysics,
            EnabledRefRW<ChunkGeneratedMesh> generatedMesh)
        {
            // Dispose old collider
            if (physicsCollider.Value.IsCreated)
            {
                physicsCollider.Value.Dispose();
            }

            if(chunk.meshHighDetailValid == false)
            {
                requestPhysics.ValueRW = true;
                return;
            }

            ref MeshBlob meshBlob = ref chunk.meshBlobHighDetail.Value;

            NativeArray<float3> vertices = default;
            NativeArray<int3> triangles = default;

            try
            {
                // Convert blob arrays into temporary native arrays
                vertices = new NativeArray<float3>(meshBlob.Vertices.Length, Allocator.Temp);

                triangles = new NativeArray<int3>(meshBlob.Triangles.Length / 3, Allocator.Temp);

                // Copy vertices
                for (int i = 0; i < meshBlob.Vertices.Length; i++)
                {
                    vertices[i] = meshBlob.Vertices[i];
                }

                // Convert triangle index list -> int3 list
                for (int i = 0; i < triangles.Length; i++)
                {
                    triangles[i] = new int3(
                        meshBlob.Triangles[i * 3 + 0],
                        meshBlob.Triangles[i * 3 + 1],
                        meshBlob.Triangles[i * 3 + 2]);
                }

                // Build collider
                physicsCollider.Value = MeshCollider.Create(
                    vertices,
                    triangles);
            }
            finally
            {
                if (vertices.IsCreated) vertices.Dispose();
                if (triangles.IsCreated) triangles.Dispose();
            }
            requestPhysics.ValueRW = true;
        }
    }
}