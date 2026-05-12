using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;
using Unity.Physics;
using Unity.Collections;

partial class EntityRenderSystem : SystemBase
{
    UnityEngine.Material terrainMaterial;

    EntityQuery _renderable;

    protected override void OnCreate()
    {
        _renderable = GetEntityQuery(new EntityQueryDesc
        {
            All = new[]
            {
                ComponentType.ReadOnly<EntityMeshRenderer>(), // managed
                ComponentType.ReadOnly<LocalTransform>()
            },
        });
    }

    protected override void OnStartRunning()
    {
        ChunkWorldSingleton chunkWorld = SystemAPI.GetSingleton<ChunkWorldSingleton>();
        terrainMaterial = Resources.Load<UnityEngine.Material>(chunkWorld.terrainMaterial.ToString());
    }

    protected override void OnUpdate()
    {
        using var renderableEntities = _renderable.ToEntityArray(Allocator.Temp);
        var em = EntityManager;

        foreach (Entity renderable in renderableEntities)
        {
            // if (!em.IsComponentEnabled<EntityMeshRenderer>(renderable))
            //     continue;

            var entityMeshRenderer = em.GetComponentData<EntityMeshRenderer>(renderable);
            var transform = em.GetComponentData<LocalTransform>(renderable);
            Graphics.RenderMesh(new RenderParams(terrainMaterial), entityMeshRenderer.mesh, 0, transform.ToMatrix());
        }
    }
}