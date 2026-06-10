using System.Data.Common;
using System.Drawing;
using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;
using Unity.Collections;

partial class ChunkMajorSetupSystem : SystemBase
{
    public ComputeShader heightmapper;
    private ComputeBuffer heightmap;
    private ComputeBuffer gradientMap;

    protected override void OnCreate()
    {
        heightmapper = Resources.Load<ComputeShader>("ComputeShaders/RoughHeightmap");
        heightmap = new ComputeBuffer(25 * 25, sizeof(float));
        gradientMap = new ComputeBuffer(25 * 25, sizeof(float) * 2);
    }

    protected override void OnUpdate()
    {
        ChunkWorldSingleton chunkWorld = SystemAPI.GetSingleton<ChunkWorldSingleton>();
        // Enable - Disable
        GameState gameState = SystemAPI.GetSingleton<GameState>();
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
            int kernel = heightmapper.FindKernel("Heightmap");
            heightmapper.SetBuffer(kernel, "HeightmapBuffer", heightmap);
            heightmapper.SetBuffer(kernel, "GradientBuffer", gradientMap);

            heightmapper.SetInts("chunkPosition", chunk.ValueRO.position.x, chunk.ValueRO.position.y);

            heightmapper.SetInt("minorPerMajorChunks", chunkWorld.minorPerMajorChunk);

            heightmapper.SetFloat("scale", 5f);
            heightmapper.SetFloat("worldHeight", 200);
            heightmapper.SetInt("seed", 2);

            int size = 25;
            int threads = 8;

            int groupsX = Mathf.CeilToInt(size / (float)threads);
            int groupsY = Mathf.CeilToInt(size / (float)threads);

            heightmapper.Dispatch(kernel, groupsX, groupsY, 1);

            float[] heights = new float[25 * 25];
            float2[] gradients = new float2[25 * 25];
            heightmap.GetData(heights);
            gradientMap.GetData(gradients);

            // string debug = "";
            // for(int i = 0; i < gradients.Length; i++)
            // {
            //     debug += "[";
            //     debug += gradients[i].x;
            //     debug += " , ";
            //     debug += gradients[i].y;
            //     debug += "] ";
            //     if(i == 24)
            //     {
            //         debug += "\n";
            //     }
            // }
            // Debug.Log(debug);

            if(majorChunk.ValueRO.heightmapData.IsCreated)
            {
                majorChunk.ValueRW.heightmapData.Dispose();
            }

            majorChunk.ValueRW.heightmapData = HeightmapDataBlobBuilder.Create(heights, gradients, 25);
            SystemAPI.SetComponentEnabled<ChunkGenerationComplete>(entity, true);
            SystemAPI.SetComponentEnabled<ChunkActive>(entity, true);

            break;
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
