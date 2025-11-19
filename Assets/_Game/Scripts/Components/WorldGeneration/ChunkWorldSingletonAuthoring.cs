using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

class ChunkWorldSingletonAuthoring : MonoBehaviour
{
    public int renderDistance;
    public float chunkSpawnRadius = 30;
    public int chunkCubeSize = 10;
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
        var entity = GetEntity(TransformUsageFlags.None);
        AddComponent(entity, new ChunkWorldSingleton
        {
            renderDistance = authoring.renderDistance,
            pooledChunkCount = calculatedChunkCount,
            currentChunkCount = 0,
            chunkSize = authoring.chunkCubeSize,
            activeChunk = float3.zero,
            numPointsPerAxis = authoring.numPointsPerAxis,
            isoLevel = authoring.isoLevel,
            boundsSize = authoring.boundsSize,
            terrainMaterial = authoring.terrainMaterial
        });
    }
}

public struct ChunkWorldSingleton : IComponentData
{
    public int renderDistance;
    public int pooledChunkCount;
    public int currentChunkCount;
    public float3 activeChunk;
    public float isoLevel;
    public int chunkSize;
    public int numPointsPerAxis;
    public int boundsSize;

    // Terrain
    public FixedString128Bytes terrainMaterial;
}