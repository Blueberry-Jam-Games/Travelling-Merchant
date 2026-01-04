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

    protected override void OnCreate()
    {
        heightmapper = Resources.Load<ComputeShader>("ComputeShaders/RoughHeightmap");
        heightmap = new ComputeBuffer(25 * 25, sizeof(float));
    }

    protected override  void OnUpdate()
    {
        ChunkWorldSingleton chunkWorld = SystemAPI.GetSingleton<ChunkWorldSingleton>();
        // Enable - Disable
        GameState gameState = SystemAPI.GetSingleton<GameState>();
        if(!gameState.chunkGenerationEnable) return;

        foreach ((
            RefRW<MajorChunk> chunk,
            _,
            Entity entity
            ) in SystemAPI.Query<
                RefRW<MajorChunk>,
                RefRO<ChunkRequestUpdate>
                >().WithEntityAccess())
        {
            if(chunk.ValueRO.generationStep == 0)
            {
                int kernel = heightmapper.FindKernel("Heightmap");
                heightmapper.SetBuffer(kernel, "HeightmapBuffer", heightmap);

                heightmapper.SetInts("chunkPosition", chunk.ValueRO.chunkPosition.x, chunk.ValueRO.chunkPosition.y);

                heightmapper.SetInt("minorPerMajorChunks", chunkWorld.MinorPerMajorChunk);

                heightmapper.SetFloat("scale", 5f);
                heightmapper.SetFloat("worldHeight", 200);
                heightmapper.SetInt("seed", 2);

                int size = 25;
                int threads = 8;

                int groupsX = Mathf.CeilToInt(size / (float)threads);
                int groupsY = Mathf.CeilToInt(size / (float)threads);

                heightmapper.Dispatch(kernel, groupsX, groupsY, 1);

                float[] temp = new float[25 * 25];
                heightmap.GetData(temp);

                FixedList4096Bytes<float> heights = default;
                for (int i = 0; i < temp.Length; i++)
                {
                    heights.Add(temp[i]);
                }

                chunk.ValueRW.heightmap = heights;
                chunk.ValueRW.generationStep = 1;
                SystemAPI.SetComponentEnabled<MajorChunkReady>(entity, true);
            }
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
