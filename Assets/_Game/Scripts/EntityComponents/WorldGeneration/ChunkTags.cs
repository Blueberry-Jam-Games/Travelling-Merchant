using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

public struct ChunkSleeping : IComponentData, IEnableableComponent
{

}

public struct ChunkRequestUpdate : IComponentData, IEnableableComponent
{
    
}

public struct ChunkRequestPhysics : IComponentData, IEnableableComponent
{
    
}

// Used by major chunks to mark when they are ready for minor chunks to generate off of them.
public struct MajorChunkReady : IComponentData, IEnableableComponent
{
    
}

public struct ChunkGenerationComplete : IComponentData, IEnableableComponent
{
    
}
