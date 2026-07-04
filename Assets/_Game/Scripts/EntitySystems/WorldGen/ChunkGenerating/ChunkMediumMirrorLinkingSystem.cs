using Unity.Burst;
using Unity.Entities;
using UnityEditor;
using UnityEngine;

[UpdateInGroup(typeof(SimulationSystemGroup))]
[UpdateAfter(typeof(BeginSimulationEntityCommandBufferSystem))]
partial class ChunkMediumMirrorLinkingSystem : SystemBase
{
    protected override void OnUpdate()
    {
        if(MediumChunkMirrorRegistry.pendingMirrors.Count <= 0)
        {
            return;
        }

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

                SystemAPI.SetComponentEnabled<EntityHasMirror>(e, true);
            }
            else
            {
                break;
            }
        }
    }
}
