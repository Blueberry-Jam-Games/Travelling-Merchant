using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using UnityEngine;

class HoldableDataAuthoring : MonoBehaviour
{
    
}

class HoldableDataAuthoringBaker : Baker<HoldableDataAuthoring>
{
    public override void Bake(HoldableDataAuthoring authoring)
    {
        var entity = GetEntity(TransformUsageFlags.Dynamic);
        AddComponent<HoldableData>(entity, new HoldableData
        {

        });
        AddComponent<Interactable>(entity, new Interactable
        {

        });
        AddComponent<PhysicsGravityFactor>(entity, new PhysicsGravityFactor
        {
            Value = 1
        });
    }
}

public struct HoldableData : IComponentData, IEnableableComponent
{
    public Entity ControlingEntity;
    public bool held;
    public float3 zeroedRotation;
}
