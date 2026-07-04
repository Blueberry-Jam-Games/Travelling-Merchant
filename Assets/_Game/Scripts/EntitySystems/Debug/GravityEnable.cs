using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Physics.Systems;
using Unity.Transforms;
using UnityEditor.ShaderGraph.Internal;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SocialPlatforms;

[UpdateInGroup(typeof(WorldGenerationSystemGroup))]
partial struct GravityEnable : ISystem
{
    bool completed;

    public void OnCreate()
    {
        completed = false;
    }
    
    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        if (completed) return;

        foreach (
            RefRO<Chunk> chunk
            in SystemAPI.Query<
                RefRO<Chunk>
                >()
                .WithAll<ChunkActive, MinorChunk>()
                .WithDisabled<ChunkGeneratedPhysicsCollider>())
        {
            return;
        }

        foreach (
        RefRW<PhysicsGravityFactor> gravity
        in SystemAPI.Query<
            RefRW<PhysicsGravityFactor>
            >())
        {
            gravity.ValueRW.Value = 1;
        }


        completed = true;
    }
}
