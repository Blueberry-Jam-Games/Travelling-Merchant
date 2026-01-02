using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

public class ChunkDisplayDebug : MonoBehaviour
{
    void OnDrawGizmos()
    {
        if (!Application.isPlaying)
            return;

        var world = World.DefaultGameObjectInjectionWorld;
        if (world == null)
            return;

        var em = world.EntityManager;

        // Try to get the singleton holding chunk size info
        if (!em.HasComponent<ChunkWorldSingleton>(em.World.EntityManager.CreateEntityQuery(typeof(ChunkWorldSingleton)).GetSingletonEntity()))
            return;

        var chunkWorld = em.CreateEntityQuery(typeof(ChunkWorldSingleton)).GetSingleton<ChunkWorldSingleton>();
        float3 chunkSize = chunkWorld.chunkSize;
        float3 majorChunkSize = chunkWorld.chunkSize * chunkWorld.MinorPerMajorChunk;
        majorChunkSize.y = chunkWorld.chunkSize;

        var query = em.CreateEntityQuery(new EntityQueryDesc
        {
            All = new ComponentType[]
            {
                ComponentType.ReadOnly<LocalTransform>(),
                ComponentType.ReadOnly<ChunkSleeping>(),
                ComponentType.ReadOnly<Chunk>()
            },
            Options = EntityQueryOptions.IgnoreComponentEnabledState
        });

        using (var entities = query.ToEntityArray(Unity.Collections.Allocator.Temp))
        {
            float3 shrink = new float3(0.04f, 0.04f, 0.04f);
            foreach (var entity in entities)
            {
                // Skip if sleeping is enabled
                if (em.IsComponentEnabled<ChunkSleeping>(entity))
                {
                    if (em.IsComponentEnabled<ChunkRequestUpdate>(entity))
                    {
                        var localTransform = em.GetComponentData<LocalTransform>(entity);

                        Gizmos.color = Color.blue;
                        float3 corner1 = localTransform.Position + chunkSize / 2 + shrink;
                        float3 size = chunkSize - shrink * 2;
                        Gizmos.DrawWireCube(corner1, size);
                    }
                    else
                    {
                        var localTransform = em.GetComponentData<LocalTransform>(entity);

                        Gizmos.color = Color.red;
                        float3 corner1 = localTransform.Position + chunkSize / 2 + shrink;
                        float3 size = chunkSize - shrink * 2;
                        Gizmos.DrawWireCube(corner1, size);
                    }
                }
                else
                {
                    if (em.IsComponentEnabled<ChunkRequestUpdate>(entity))
                    {
                        var localTransform = em.GetComponentData<LocalTransform>(entity);

                        Gizmos.color = Color.orange;
                        float3 corner1 = localTransform.Position + chunkSize / 2 + shrink;
                        float3 size = chunkSize - shrink * 2;
                        Gizmos.DrawWireCube(corner1, size);
                    }
                    else
                    {
                        var localTransform = em.GetComponentData<LocalTransform>(entity);

                        Gizmos.color = Color.green;
                        float3 corner1 = localTransform.Position + chunkSize / 2 + shrink;
                        float3 size = chunkSize - shrink * 2;
                        Gizmos.DrawWireCube(corner1, size);
                    }
                }
            }
        }

        var majorQuery = em.CreateEntityQuery(new EntityQueryDesc
        {
            All = new ComponentType[]
            {
                ComponentType.ReadOnly<LocalTransform>(),
                ComponentType.ReadOnly<MajorChunk>()
            },
            Options = EntityQueryOptions.IgnoreComponentEnabledState
        });

        using (var entities = majorQuery.ToEntityArray(Unity.Collections.Allocator.Temp))
        {
            float3 shrink = new float3(0.06f, 0.06f, 0.06f);
            foreach (var entity in entities)
            {
                var localTransform = em.GetComponentData<LocalTransform>(entity);

                Gizmos.color = Color.gold;
                float3 size = majorChunkSize - shrink * 2;
                float3 corner1 = localTransform.Position + size / 2 + shrink;
                Gizmos.DrawWireCube(corner1, size);
            }
        }
    }
}
