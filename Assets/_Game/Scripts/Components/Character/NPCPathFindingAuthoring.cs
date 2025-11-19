using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

class NPCPathFindingAuthoing : MonoBehaviour
{
    
}

class NPCPathFindingAuthoingBaker : Baker<NPCPathFindingAuthoing>
{
    public override void Bake(NPCPathFindingAuthoing authoring)
    {
        var entity = GetEntity(TransformUsageFlags.None);
        AddComponent<NPCPathFinding>(entity, new NPCPathFinding
        {

        });
    }
}

public struct NPCPathFinding : IComponentData
{
    
}
