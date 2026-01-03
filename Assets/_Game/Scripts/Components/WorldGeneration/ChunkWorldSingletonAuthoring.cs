using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

class ChunkWorldSingletonAuthoring : MonoBehaviour
{
    public int renderDistance;
    public int chunkCubeSize = 32;
    public int MinorPerMajorChunk = 24;
    public float isoLevel;
    public int numPointsPerAxis = 30;
    public int boundsSize = 1;
    public string terrainMaterial;
}

class ChunkWorldSingletonAuthoringBaker : Baker<ChunkWorldSingletonAuthoring>
{
    public override void Bake(ChunkWorldSingletonAuthoring authoring)
    {
        int calculatedChunkCount = (int)math.pow(authoring.renderDistance * 2 + 1, 3) + (int)math.pow(authoring.renderDistance * 2 + 1, 2) * 4; // Revise Later

        const int majorChunkPadding = 3; // Passing multiple for chunks. Keep at 3
        int majorChunkRequiredCount = math.square((int)math.ceil((float) authoring.renderDistance / authoring.MinorPerMajorChunk) * majorChunkPadding + 1);

        var entity = GetEntity(TransformUsageFlags.None);
        AddComponent(entity, new ChunkWorldSingleton
        {
            renderDistance = authoring.renderDistance,
            pooledChunkCount = calculatedChunkCount,
            currentChunkCount = 0,
            pooledMajorChunkCount = majorChunkRequiredCount,
            MajorChunkCurrentCount = 0,
            MajorChunkSize = authoring.chunkCubeSize * authoring.MinorPerMajorChunk,
            MinorPerMajorChunk = authoring.MinorPerMajorChunk,
            majorChunkRenderDistance = (int)math.ceil((float) authoring.renderDistance / authoring.MinorPerMajorChunk) + 1,

            chunkSize = authoring.chunkCubeSize,
            activeChunk = float3.zero,
            numPointsPerAxis = authoring.numPointsPerAxis,
            isoLevel = authoring.isoLevel,
            boundsSize = authoring.boundsSize,
            terrainMaterial = authoring.terrainMaterial,
        });
    }
}

public struct ChunkWorldSingleton : IComponentData
{
    public int renderDistance;
    public int pooledChunkCount; // Requested number of minor chunk entities. Contains buffer.
    public int currentChunkCount; // Actual number of minor chunk entities
    public int pooledMajorChunkCount; // Required amount of major chunk entities
    public int MajorChunkCurrentCount; // Actual number of major chunk entities
    public int MajorChunkSize; // Size of major chunk in Unity Units
    public int MinorPerMajorChunk; // Size of major chunk in units of Minor Chunks. Current default is 16. Change with caution. Major chunk lists may need to be increased in capacity and heightmap struct must be changed manually.
    public int majorChunkRenderDistance; // Amount of major chunks to be generated away from the player. Stored in units of Major chunk. TODO Revise to account for LoD

    public float3 activeChunk;
    public int2 activeMajorChunk;
    public float isoLevel;
    public int chunkSize;
    public int numPointsPerAxis;
    public int boundsSize;

    // Terrain
    public FixedString128Bytes terrainMaterial;
}