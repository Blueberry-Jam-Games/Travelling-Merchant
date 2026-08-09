using Unity.Burst;
using Unity.Entities;
using UnityEditor;
using UnityEngine;

[UpdateInGroup(typeof(SimulationSystemGroup))]
[UpdateAfter(typeof(BeginSimulationEntityCommandBufferSystem))]
partial class ChunkMediumMirrorLinkingSystem : SystemBase
{
    ComponentLookup<MajorChunk> majorChunkLookup = new ComponentLookup<MajorChunk>();
    protected override void OnCreate()
    {
        base.OnCreate();
        majorChunkLookup = GetComponentLookup<MajorChunk>(true);
    }

    protected override void OnUpdate()
    {
        if(MediumChunkMirrorRegistry.pendingMirrors.Count <= 0)
        {
            return;
        }

        majorChunkLookup.Update(this);

        foreach((
            RefRO<MediumChunk> mediumChunk,
            Entity e
                ) in SystemAPI.Query<
                RefRO<MediumChunk>
                >().WithAll<Chunk>().
                WithDisabled<EntityHasMirror>()
                .WithEntityAccess())
        {
            if(MediumChunkMirrorRegistry.pendingMirrors.Count > 0)
            {
                MediumChunkMirror m = MediumChunkMirrorRegistry.pendingMirrors.Dequeue();
                MediumChunkMirrorRegistry.Mirrors.Add(e, m);

                if(majorChunkLookup.TryGetComponent(mediumChunk.ValueRO.majorChunk, out MajorChunk majorChunk))
                {
                    if(majorChunk.renderInformation != null)
                    {
                        m.renderInformation = majorChunk.renderInformation;
                    }
                    else
                    {
                        Debug.LogError("Texture didn't generate properly");
                    }   
                }

                SystemAPI.SetComponentEnabled<EntityHasMirror>(e, true);
            }
            else
            {
                break;
            }
        }
    }
}
