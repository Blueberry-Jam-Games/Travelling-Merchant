using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

class ChunkWorldSingletonAuthoring : MonoBehaviour
{
    public int renderDistance;
    public int renderDistanceMediumChunks;

    [Header("Chunk Sizes")]
    public int minorChunkSize = 32;
    public int minorPerMediumChunk = 8;
    public int mediumPerMajorChunk = 3;

    [Header("Generation Information")]
    public float isoLevel;
    public int numPointsPerAxis = 32;
    public int boundsSize = 1; // TODO Depricate
    public string terrainMaterial; // TODO Depricate

    public int seed;
}
 
class ChunkWorldSingletonAuthoringBaker : Baker<ChunkWorldSingletonAuthoring>
{
    public override void Bake(ChunkWorldSingletonAuthoring authoring)
    {
        // Temp Storage
        // int calculatedChunkCount = (int)math.pow(authoring.renderDistance * 2 + 1, 3) + (int)math.pow(authoring.renderDistance * 2 + 1, 2) * 4; // Revise Later

        // const int majorChunkPadding = 3; // Passing multiple for chunks. Keep at 3
        // int majorChunkRequiredCount = math.square((int)math.ceil((float) authoring.renderDistance / authoring.MinorPerMajorChunk) * majorChunkPadding + 1) * 2;

        var entity = GetEntity(TransformUsageFlags.None);
        AddComponent(entity, new ChunkWorldSingleton
        {
            //renderDistance = authoring.renderDistance, // TODO Depricate
            minorChunkSize = authoring.minorChunkSize,
            minorPerMediumChunk = authoring.minorPerMediumChunk,
            mediumPerMajorChunk = authoring.mediumPerMajorChunk,
            minorPerMajorChunk = authoring.minorPerMediumChunk * authoring.mediumPerMajorChunk,
            mediumChunkSize = authoring.minorChunkSize * authoring.minorPerMediumChunk,
            majorChunkSize = authoring.minorChunkSize * authoring.minorPerMediumChunk * authoring.mediumPerMajorChunk,
            //majorChunkRenderDistance = (int)math.ceil((float) authoring.renderDistance / authoring.minorPerMediumChunk) + 1, // TODO Depricate

            // Render distance calculations
            renderDistanceMeters = authoring.renderDistanceMediumChunks * authoring.minorPerMediumChunk * authoring.minorChunkSize,
            renderDistanceMinor = (authoring.renderDistanceMediumChunks / 2) * authoring.minorPerMediumChunk,
            renderDistanceMedium = authoring.renderDistanceMediumChunks,
            renderDistanceMajor = authoring.renderDistanceMediumChunks / authoring.mediumPerMajorChunk + 1, // Control Major Chunk Buffer

            activeMinorChunk = new int3(5, 5, 5),
            activeMediumChunk = new int2(5, 5),
            activeMajorChunk = new int2(5, 5),
            newMajorChunkOccupied = true,
            newMediumChunkOccupied = true,
            numPointsPerAxis = authoring.numPointsPerAxis,
            isoLevel = authoring.isoLevel,

            DispatchableShaderCount = 8,

            seed = authoring.seed
        });
    }
}
public struct ChunkWorldSingleton : IComponentData
{
    //public int renderDistance; // TODO Depricate

    public int renderDistanceMeters;
    public int renderDistanceMinor;
    public int renderDistanceMedium;
    public int renderDistanceMajor;

    public int DispatchableShaderCount; // Count of dispatchable compute shaders can exist for chunks. Higher numbers allow more chunks to run at a time.

    public int majorChunkSize; // Size of major chunk in Unity Units
    public int mediumChunkSize;
    public int minorChunkSize;

    public int mediumPerMajorChunk;
    public int minorPerMediumChunk;
    public int minorPerMajorChunk; // Size of major chunk in units of Minor Chunks. Current default is 16. Change with caution. Major chunk lists may need to be increased in capacity and heightmap struct must be changed manually.
    //public int majorChunkRenderDistance; // Amount of major chunks to be generated away from the player. Stored in units of Major chunk. TODO Revise to account for LoD

    public int3 activeMinorChunk;
    public int2 activeMediumChunk;
    public int2 activeMajorChunk;
    public bool newMajorChunkOccupied;
    public bool newMediumChunkOccupied;

    public float isoLevel;
    public int numPointsPerAxis;

    public int seed;
}