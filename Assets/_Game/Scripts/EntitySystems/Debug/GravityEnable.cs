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

    int cycles;

    public void OnCreate()
    {
        completed = false;
        cycles = 0;
    }
    
    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        if (completed) return;

        if(cycles <= 10)
        {
            cycles ++;
            return;
        }

        int chunkGeneratingCount = 0;
        foreach ((
            RefRO<Chunk> chunk,
            RefRO<PhysicsCollider> physicsCollider,
                RefRO<ChunkRequestMesh> req
            ) in SystemAPI.Query<
                RefRO<Chunk>,
                RefRO<PhysicsCollider>,
                RefRO<ChunkRequestMesh>
                >())
        {
            chunkGeneratingCount++;
        }

        if (chunkGeneratingCount == 0)
        {
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
        else
        {
            
        }
    }
}
