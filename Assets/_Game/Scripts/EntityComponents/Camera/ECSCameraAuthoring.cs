using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

class ECSCameraAuthoring : MonoBehaviour
{
    public int weight;
}

class ECSCameraAuthoringBaker : Baker<ECSCameraAuthoring>
{
    public override void Bake(ECSCameraAuthoring authoring)
    {
        var entity = GetEntity(TransformUsageFlags.None);
        AddComponent<ECSCamera>(entity, new ECSCamera
        {
            position = float3.zero,
            rotation = float2.zero,
            weight = authoring.weight
        });
    }
}

public struct ECSCamera : IComponentData
{
    public float3 position;
    public float2 rotation;
    public int weight;
}
