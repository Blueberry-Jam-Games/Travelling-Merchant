using System;
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
        var gameState = em.CreateEntityQuery(typeof(GameState)).GetSingleton<GameState>();
        float3 chunkSize = chunkWorld.chunkSize;
        float3 majorChunkSize = chunkWorld.majorChunkSize;
        majorChunkSize.y = chunkWorld.mediumChunkSize;

        float3 mediumChunkSize = chunkWorld.mediumChunkSize;

        var query = em.CreateEntityQuery(new EntityQueryDesc
        {
            All = new ComponentType[]
            {
                ComponentType.ReadOnly<LocalTransform>(),
                ComponentType.ReadOnly<ChunkActive>(),
                ComponentType.ReadOnly<Chunk>(),
                ComponentType.ReadOnly<MinorChunk>()
            },
            Options = EntityQueryOptions.IgnoreComponentEnabledState
        });

        if(gameState.DEBUG_MinorChunkOutline)
        {
            using (var entities = query.ToEntityArray(Unity.Collections.Allocator.Temp))
            {
                float3 shrink = new float3(0.04f, 0.04f, 0.04f);
                foreach (var entity in entities)
                {
                    // Skip if sleeping is enabled
                    if (!em.IsComponentEnabled<ChunkActive>(entity))
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
        }
        
        var majorQuery = em.CreateEntityQuery(new EntityQueryDesc
        {
            All = new ComponentType[]
            {
                ComponentType.ReadOnly<LocalTransform>(),
                ComponentType.ReadOnly<ChunkGenerationComplete>(),
                ComponentType.ReadOnly<MajorChunk>()
            },
            Options = EntityQueryOptions.IgnoreComponentEnabledState
        });

        if(gameState.DEBUG_MajorChunkOutline || gameState.DEBUG_Heightmap)
        {
            using (var entities = majorQuery.ToEntityArray(Unity.Collections.Allocator.Temp))
            {
                float3 shrink = new float3(0.06f, 0.06f, 0.06f);
                foreach (var entity in entities)
                {
                    var localTransform = em.GetComponentData<LocalTransform>(entity);
                    if(gameState.DEBUG_MajorChunkOutline)
                    {
                        Gizmos.color = Color.gold;
                        float3 size = majorChunkSize - shrink * 2;
                        float3 corner1 = localTransform.Position + size / 2 + shrink;
                        Gizmos.DrawWireCube(corner1, size);
                    }

                    if(gameState.DEBUG_Heightmap)
                    {
                        MajorChunk majorChunk = em.GetComponentData<MajorChunk>(entity);

                        if(em.IsComponentEnabled<ChunkGenerationComplete>(entity))
                        {
                            int heightmapSize = majorChunk.heightmapData.Value.resolution;
                            for(int x = 0; x < heightmapSize; x += chunkWorld.minorPerMediumChunk)
                            {
                                for(int y = 0; y < heightmapSize; y += chunkWorld.minorPerMediumChunk)
                                {
                                    Vector3 position = localTransform.Position;
                                    position += new Vector3(x * chunkWorld.minorChunkSize, majorChunk.heightmapData.Value.GetHeight(x, y), y * chunkWorld.minorChunkSize);

                                    Gizmos.color = Color.crimson;
                                    Gizmos.DrawSphere(position, 5f);
                                }
                            }
                        }
                    }
                }
            }
        }

        var mediumQuery = em.CreateEntityQuery(new EntityQueryDesc
        {
            All = new ComponentType[]
            {
                ComponentType.ReadOnly<LocalTransform>(),
                ComponentType.ReadOnly<ChunkGenerationComplete>(),
                ComponentType.ReadOnly<MediumChunk>()
            },
            Options = EntityQueryOptions.IgnoreComponentEnabledState
        });

        if(gameState.DEBUG_MediumChunkOutline)
        {
            using (var entities = mediumQuery.ToEntityArray(Unity.Collections.Allocator.Temp))
            {
                float3 shrink = new float3(0.06f, 0.06f, 0.06f);
                foreach (var entity in entities)
                {
                    var localTransform = em.GetComponentData<LocalTransform>(entity);
                    if(gameState.DEBUG_MediumChunkOutline)
                    {
                        Gizmos.color = Color.darkOrange;
                        float3 size = mediumChunkSize - shrink * 4;
                        float3 corner1 = localTransform.Position + size / 2 + shrink * 2;
                        Gizmos.DrawWireCube(corner1, size);
                    }
                }
            }
        }
    }
}
