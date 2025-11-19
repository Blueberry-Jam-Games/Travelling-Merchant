using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

class LocalInputAuthoring : MonoBehaviour
{
    
}

class LocalInputAuthoringBaker : Baker<LocalInputAuthoring>
{
    public override void Bake(LocalInputAuthoring authoring)
    {
        var entity = GetEntity(TransformUsageFlags.None);
        AddComponent<LocalInput>(entity, new LocalInput
        {
            mouseDelta = float2.zero,
            moveDirection = float2.zero
        });
    }
}

public struct LocalInput : IComponentData
{
    public float2 mouseDelta;
    public float2 moveDirection;
    public bool interact;
    public bool rotateLeft;
    public bool rotateRight;
}
