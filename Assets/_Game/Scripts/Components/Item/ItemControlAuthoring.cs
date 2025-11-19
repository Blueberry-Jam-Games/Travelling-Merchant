using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

class ItemControlAuthoring : MonoBehaviour
{
    
}

class ItemControlAuthoringBaker : Baker<ItemControlAuthoring>
{
    public override void Bake(ItemControlAuthoring authoring)
    {
        var entity = GetEntity(TransformUsageFlags.Dynamic);
        AddComponent<ItemControl>(entity, new ItemControl
        {
            
        });
    }
}

public struct ItemControl : IComponentData, IEnableableComponent
{
    public Entity heldItem;
    public float3 itemTargetPosition;
    public float3 controllerRotation;
    public float3 itemRotationOffset;
}
