using System.Data.Common;
using System.Drawing;
using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;
using Unity.Collections;
using UnityEditor;

[UpdateInGroup(typeof(WorldGenerationSystemGroup))]
[UpdateAfter(typeof(ChunkPlacementSystem))]
partial class ChunkMajorPopulationSystem : SystemBase
{
    public ComputeShader heightmapper;
    private ComputeBuffer heightmap;
    //private ComputeBuffer normalmap;
    private int heightmapSize;

    protected override void OnCreate()
    {
        heightmapper = Resources.Load<ComputeShader>("ComputeShaders/Heightmapper");
    }

    protected override void OnUpdate()
    {
        ChunkWorldSingleton chunkWorld = SystemAPI.GetSingleton<ChunkWorldSingleton>();
        
        if(!chunkWorld.newMajorChunkOccupied) return;
        // Enable - Disable
        GameState gameState = SystemAPI.GetSingleton<GameState>();

        if(heightmap == null)
        {
            heightmapSize = (chunkWorld.minorPerMajorChunk + 1) * (chunkWorld.minorPerMajorChunk + 1);
            heightmap = new ComputeBuffer(heightmapSize, sizeof(float));
            //normalmap = new ComputeBuffer(chunkWorld.mediumPerMajorChunk * chunkWorld.mediumPerMajorChunk + 1, sizeof(float) * 2);
        }

        if(!gameState.chunkGenerationEnable) return;

        foreach ((
            RefRW<MajorChunk> majorChunk,
            RefRO<Chunk> chunk,
            RefRO<ChunkRequestUpdate> update,
            Entity entity
            ) in SystemAPI.Query<
                RefRW<MajorChunk>,
                RefRO<Chunk>,
                RefRO<ChunkRequestUpdate>
                >().
                WithDisabled<ChunkGenerationComplete>().
                WithEntityAccess())
        {
            int kernel = heightmapper.FindKernel("Heightmapper");
            heightmapper.SetBuffer(kernel, "HeightmapBuffer", heightmap);

            heightmapper.SetInts("chunkPosition", chunk.ValueRO.position.x, chunk.ValueRO.position.z);

            heightmapper.SetInt("minorPerMajorChunks", chunkWorld.minorPerMajorChunk);
            heightmapper.SetInt("minorChunkSize", chunkWorld.minorChunkSize);
            heightmapper.SetInt("majorChunkSize", chunkWorld.majorChunkSize);

            heightmapper.SetInt("seed", 24325);

            int size = chunkWorld.minorPerMajorChunk + 1;
            int threads = 8;

            int groupsX = Mathf.CeilToInt(size / (float)threads);
            int groupsY = Mathf.CeilToInt(size / (float)threads);

            heightmapper.Dispatch(kernel, groupsX, groupsY, 1);

            float[] heights = new float[heightmapSize];
            heightmap.GetData(heights);

            if(majorChunk.ValueRO.heightmapData.IsCreated)
            {
                majorChunk.ValueRW.heightmapData.Dispose();
            }

            majorChunk.ValueRW.heightmapData = HeightmapDataBlobBuilder.Create(heights, chunkWorld.minorPerMajorChunk + 1);
            SystemAPI.SetComponentEnabled<ChunkGenerationComplete>(entity, true);
            SystemAPI.SetComponentEnabled<ChunkActive>(entity, true);
        }
    }

    protected override void OnDestroy()
    {
        if(heightmap.IsValid())
        {
            heightmap.Dispose();
            heightmap = null;
        }
    }
}
