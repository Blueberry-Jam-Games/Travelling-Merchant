using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

class InteractableAuthoring : MonoBehaviour
{
    
}

class InteractableAuthoringBaker : Baker<InteractableAuthoring>
{
    public override void Bake(InteractableAuthoring authoring)
    {
        var entity = GetEntity(TransformUsageFlags.Dynamic);
        AddComponent<Interactable>(entity, new Interactable
        {
            
        });
    }
}

public struct Interactable : IComponentData, IEnableableComponent
{
    
}
