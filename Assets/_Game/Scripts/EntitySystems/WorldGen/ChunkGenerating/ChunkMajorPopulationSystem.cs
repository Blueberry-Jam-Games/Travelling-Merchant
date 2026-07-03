using Unity.Entities;
using UnityEngine;

[UpdateInGroup(typeof(WorldGenerationSystemGroup))]
[UpdateAfter(typeof(ChunkPlacementSystem))]
partial class ChunkMajorPopulationSystem : SystemBase
{
    public ComputeShader heightmapper;
    private ComputeBuffer heightmap;
    //private ComputeBuffer normalmap;
    private int heightmapSize;
    private int heightmapResolution;

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
            heightmapResolution = chunkWorld.minorPerMajorChunk + 3;
            heightmapSize = heightmapResolution * heightmapResolution;
            heightmap = new ComputeBuffer(heightmapSize, sizeof(float));
        }

        if(!gameState.chunkGenerationEnable) return;

        foreach ((
            RefRW<MajorChunk> majorChunk,
            RefRO<Chunk> chunk,
            RefRO<ChunkRequestPopulation> update,
            Entity entity
            ) in SystemAPI.Query<
                RefRW<MajorChunk>,
                RefRO<Chunk>,
                RefRO<ChunkRequestPopulation>
                >().
                WithDisabled<ChunkGenerationComplete>().
                WithEntityAccess())
        {
            int kernel = heightmapper.FindKernel("Heightmapper");
            heightmapper.SetBuffer(kernel, "HeightmapBuffer", heightmap);

            heightmapper.SetInts("chunkPosition", chunk.ValueRO.position.x, chunk.ValueRO.position.z);
            heightmapper.SetInt("chunkOffset", -1);

            heightmapper.SetInt("heightmapResolution", heightmapResolution);
            heightmapper.SetInt("minorPerMajorChunks", chunkWorld.minorPerMajorChunk);
            heightmapper.SetInt("minorChunkSize", chunkWorld.minorChunkSize);
            heightmapper.SetInt("majorChunkSize", chunkWorld.majorChunkSize);

            heightmapper.SetInt("seed", chunkWorld.seed);

            int size = chunkWorld.minorPerMajorChunk + 3;
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

            majorChunk.ValueRW.heightmapData = HeightmapDataBlobBuilder.Create(heights, chunkWorld.minorPerMajorChunk + 3);
            SystemAPI.SetComponentEnabled<ChunkGenerationComplete>(entity, true);
            SystemAPI.SetComponentEnabled<ChunkRequestPopulation>(entity, false);
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
