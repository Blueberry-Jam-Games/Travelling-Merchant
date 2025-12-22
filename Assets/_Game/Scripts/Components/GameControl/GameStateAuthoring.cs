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
            playerControlEnable = false
        });
    }
}

public struct GameState : IComponentData
{
    public bool chunkGenerationEnable;
    public bool playerControlEnable;
}