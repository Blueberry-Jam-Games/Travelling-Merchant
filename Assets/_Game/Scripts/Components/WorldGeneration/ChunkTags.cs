using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

public struct ChunkSleeping : IComponentData, IEnableableComponent
{

}

public struct ChunkRequestUpdate : IComponentData, IEnableableComponent
{
    
}

public struct MajorChunkReady : IComponentData, IEnableableComponent
{
    
}

public struct RenderMinorChunk : IComponentData, IEnableableComponent
{
    
}
