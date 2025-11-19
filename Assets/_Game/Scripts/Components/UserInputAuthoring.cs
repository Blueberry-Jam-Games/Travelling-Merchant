using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

class UserInputAuthoring : MonoBehaviour
{
    
}

class UserInputAuthoringBaker : Baker<UserInputAuthoring>
{
    public override void Bake(UserInputAuthoring authoring)
    {
        var entity = GetEntity(TransformUsageFlags.None);
        AddComponent<UserInput>(entity, new UserInput
        {
            mouseDelta = float2.zero,
            moveDirection = float2.zero
        });
    }
}

public struct UserInput : IComponentData
{
    public float2 mouseDelta;
    public float2 moveDirection;

    public bool interact;

    public bool rotateLeft;
    public bool rotateRight;
}
