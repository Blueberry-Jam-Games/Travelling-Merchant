using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

class GameStateAuthoring : MonoBehaviour
{
    
}

class GameStateManagerBaker : Baker<GameStateAuthoring>
{
    public override void Bake(GameStateAuthoring authoring)
    {
        var entity = GetEntity(TransformUsageFlags.None);
        AddComponent<GameState>(entity, new GameState
        {
            chunkGenerationEnable = false,
            playerControlEnable = false,
            DEBUG_ChunkOutline = true,
            DEBUG_MajorChunkOutline = true,
            DEBUG_Heightmap = false
        });
    }
}

public struct GameState : IComponentData
{
    public bool chunkGenerationEnable;
    public bool playerControlEnable;
    public bool DEBUG_ChunkOutline;
    public bool DEBUG_MajorChunkOutline;
    public bool DEBUG_Heightmap;
}