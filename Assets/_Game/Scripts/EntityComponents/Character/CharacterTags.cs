using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
class PlayerAuthoring : MonoBehaviour
{

}

class PlayerAuthoringBaker : Baker<PlayerAuthoring>
{
    public override void Bake(PlayerAuthoring authoring)
    {
        var entity = GetEntity(TransformUsageFlags.None);
        AddComponent<Player>(entity, new Player
        {
            
        });
    }
}

public struct Player : IComponentData
{

}
