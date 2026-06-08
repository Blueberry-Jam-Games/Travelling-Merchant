using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using Unity.Collections;
using Unity.Physics;
using System.Runtime.CompilerServices;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;
using System.Linq;

[UpdateInGroup(typeof(WorldGenerationSystemGroup))]
[UpdateBefore(typeof(ChunkPlacementSystem))]
partial struct CharacterChunkTrackingSystem : ISystem
{
    public void OnCreate(ref SystemState state)
    {
        
    }

    public void OnUpdate(ref SystemState state)
    {
        if (!SystemAPI.HasSingleton<ChunkWorldSingleton>()) return;

        ref ChunkWorldSingleton chunkWorld = ref SystemAPI.GetSingletonRW<ChunkWorldSingleton>().ValueRW;
        Entity player = SystemAPI.GetSingletonEntity<Player>();
        LocalTransform playerTransform = SystemAPI.GetComponent<LocalTransform>(player);

        int2 playerMajorChunk = new int2(
            (int)math.floor(playerTransform.Position.x / chunkWorld.MajorChunkSize),
            (int)math.floor(playerTransform.Position.z / chunkWorld.MajorChunkSize)
        );
        
        if(math.any(playerMajorChunk != chunkWorld.activeMajorChunk))
        {
            chunkWorld.newMajorChunk = true;
            chunkWorld.activeMajorChunk = playerMajorChunk;
        }
        else
        {
            chunkWorld.newMajorChunk = false;
        }

        // Minor Chunk Placement
        int3 playerMinorChunk = new int3(
            (int)math.floor(playerTransform.Position.x / chunkWorld.chunkSize),
            (int)math.floor(playerTransform.Position.y / chunkWorld.chunkSize),
            (int)math.floor(playerTransform.Position.z / chunkWorld.chunkSize)
        );

        if (math.any(playerMinorChunk != chunkWorld.activeMinorChunk))
        {
            chunkWorld.newMinorChunk = true;
            chunkWorld.activeMinorChunk = playerMinorChunk;
        }
        else
        {
            chunkWorld.newMinorChunk = false;
        }
    }

    public void OnDestroy(ref SystemState state)
    {

    }
}
