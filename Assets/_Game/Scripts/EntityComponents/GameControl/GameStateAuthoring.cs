using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

class GameStateAuthoring : MonoBehaviour
{
    public bool majorChunkOutline = false;
    public bool mediumChunkOutline = false;
    public bool minorChunkOutline = false;
}

class GameStateManagerBaker : Baker<GameStateAuthoring>
{
    public override void Bake(GameStateAuthoring authoring)
    {
        var entity = GetEntity(TransformUsageFlags.None);
        AddComponent<GameState>(entity, new GameState
        {
            chunkGenerationEnable = true,
            playerControlEnable = true,
            DEBUG_MajorChunkOutline = authoring.majorChunkOutline,
            DEBUG_MediumChunkOutline = authoring.mediumChunkOutline,
            DEBUG_MinorChunkOutline = authoring.minorChunkOutline,
            DEBUG_Heightmap = false
        });
    }
}

public struct GameState : IComponentData
{
    public bool chunkGenerationEnable;
    public bool playerControlEnable;
    public bool DEBUG_MajorChunkOutline;
    public bool DEBUG_MediumChunkOutline;
    public bool DEBUG_MinorChunkOutline;
    public bool DEBUG_Heightmap;
}