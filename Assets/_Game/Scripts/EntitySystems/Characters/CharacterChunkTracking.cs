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
partial struct CharacterChunkTrackingSystem : ISystem
{
    public void OnUpdate(ref SystemState state)
    {
        if (!SystemAPI.HasSingleton<ChunkWorldSingleton>()) return;

        ref ChunkWorldSingleton chunkWorld = ref SystemAPI.GetSingletonRW<ChunkWorldSingleton>().ValueRW;
        Entity player = SystemAPI.GetSingletonEntity<Player>();
        LocalTransform playerTransform = SystemAPI.GetComponent<LocalTransform>(player);

        int2 playerMajorChunk = new int2(
            (int)math.floor(playerTransform.Position.x / chunkWorld.majorChunkSize),
            (int)math.floor(playerTransform.Position.z / chunkWorld.majorChunkSize)
        );
        
        if(math.any(playerMajorChunk != chunkWorld.activeMajorChunk))
        {
            chunkWorld.newMajorChunkOccupied = true;
            chunkWorld.activeMajorChunk = playerMajorChunk;
        }
        else
        {
            chunkWorld.newMajorChunkOccupied = false;
        }

        // Medium Chunk Placement
        int2 playerMediumChunk = new int2(
            (int)math.floor(playerTransform.Position.x / chunkWorld.mediumChunkSize),
            (int)math.floor(playerTransform.Position.z / chunkWorld.mediumChunkSize)
        );

        if (math.any(playerMediumChunk != chunkWorld.activeMediumChunk))
        {
            chunkWorld.newMediumChunkOccupied = true;
            chunkWorld.activeMediumChunk = playerMediumChunk;
        }
        else
        {
            chunkWorld.newMediumChunkOccupied = false;
        }

        // TODO Depricate
        // Minor Chunk Placement
        int3 playerMinorChunk = new int3(
            (int)math.floor(playerTransform.Position.x / chunkWorld.chunkSize),
            (int)math.floor(playerTransform.Position.y / chunkWorld.chunkSize),
            (int)math.floor(playerTransform.Position.z / chunkWorld.chunkSize)
        );

        if (math.any(playerMinorChunk != chunkWorld.activeMinorChunk))
        {
            chunkWorld.newMajorChunkOccupied = true;
            chunkWorld.activeMinorChunk = playerMinorChunk;
        }
        else
        {
            chunkWorld.newMajorChunkOccupied = false;
        }
    }
}
