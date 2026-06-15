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
            RefRO<Chunk> Chunk,
            Entity e
                ) in SystemAPI.Query<
                RefRO<MediumChunk>,
                RefRO<Chunk>
                >().WithPresent<ChunkRequiresMirror>()
                .WithEntityAccess())
        {
            if(MediumChunkMirrorRegistry.pendingMirrors.Count > 0)
            {
                MediumChunkMirror m = MediumChunkMirrorRegistry.pendingMirrors.Dequeue();
                MediumChunkMirrorRegistry.Mirrors.Add(e, m);

                SystemAPI.SetComponentEnabled<ChunkRequiresMirror>(e, false);
            }
            else
            {
                break;
            }
        }
    }
}
