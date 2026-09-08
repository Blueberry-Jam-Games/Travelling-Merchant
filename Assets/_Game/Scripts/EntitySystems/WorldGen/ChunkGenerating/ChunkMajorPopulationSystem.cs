using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Transforms;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.Rendering.Universal;

[UpdateInGroup(typeof(WorldGenerationSystemGroup))]
[UpdateAfter(typeof(ChunkPlacementSystem))]
partial class ChunkMajorPopulationSystem : SystemBase
{
    public ComputeShader heightmapper;
    private ComputeBuffer heightmap;
    private ComputeBuffer gradientMap;
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

        if(gradientMap == null)
        {
            gradientMap = new ComputeBuffer(heightmapSize, sizeof(float) * 2);
        }

        if(!gameState.chunkGenerationEnable) return;

        foreach ((
            RefRW<MajorChunk> majorChunk,
            RefRO<Chunk> chunk,
            RefRO<LocalTransform> transform,
            Entity entity
            ) in SystemAPI.Query<
                RefRW<MajorChunk>,
                RefRO<Chunk>,
                RefRO<LocalTransform>
                >()
                .WithDisabled<ChunkPopulated, ChunkPopulating, ChunkConfigured>()
                .WithAll<ChunkActive>()
                .WithEntityAccess())
        {
#region Generate Hightmap
            int kernel = heightmapper.FindKernel("Heightmapper");
            heightmapper.SetBuffer(kernel, "HeightmapBuffer", heightmap);
            heightmapper.SetBuffer(kernel, "GradientBuffer", gradientMap);

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
            float2[] gradients = new float2[heightmapSize];
            heightmap.GetData(heights);
            gradientMap.GetData(gradients);

            if(majorChunk.ValueRO.heightmapData.IsCreated)
            {
                majorChunk.ValueRW.heightmapData.Dispose();
            }

            majorChunk.ValueRW.heightmapData = HeightmapDataBlobBuilder.Create(heights, gradients, chunkWorld.minorPerMajorChunk + 3);
#endregion
#region Texture Overlay
            Texture2D texture = majorChunk.ValueRO.renderInformation.Value;

            if (texture == null)
            {
                texture = new Texture2D(chunkWorld.detailMapResolution, chunkWorld.detailMapResolution, TextureFormat.RGBAHalf, false, true)
                {
                    wrapMode = TextureWrapMode.Clamp,
                    filterMode = FilterMode.Bilinear
                };

                majorChunk.ValueRW.renderInformation = texture;
            }

            

// #region Old path system
            // Clear the texture
            var pixels = texture.GetPixelData<half4>(0);

            half4 clearColor = new half4((half)0, (half)0, (half)0, (half)0);

            for (int i = 0; i < pixels.Length; i++)
            {
                pixels[i] = clearColor;
            }

            // texture.name = "MajorChunkRenderInformation" + textureNumber;
            // textureNumber ++;

//             float pixelsPerUnit = chunkWorld.XZtoImageUV;

//             int halfWidthPixels = math.max(1, (int)math.ceil(chunkWorld.PathWidth * 0.5f * pixelsPerUnit));

//             // V = world X
//             for (int vPixel = 0; vPixel < texture.width; vPixel++)
//             {
//                 // Convert texture X into world-space forward position.
//                 float worldV = transform.ValueRO.Position.x + vPixel / pixelsPerUnit;

//                 // Find world-space perpendicular position Z.
//                 float worldU = PathResolver.GetUFromV(worldV);

//                 // Convert world-space U into chunk-local U.
//                 float localU = worldU - transform.ValueRO.Position.z;

//                 // Convert world units into texture pixels.
//                 int uPixel = (int)math.round(localU * pixelsPerUnit);

//                 // Skip if the path is completely outside this chunk.
//                 if (uPixel < -halfWidthPixels || uPixel >= texture.height + halfWidthPixels) continue;

//                 int minU = math.max(0, uPixel - halfWidthPixels);

//                 int maxU = math.min(texture.height - 1, uPixel + halfWidthPixels);

//                 for (int y = minU; y <= maxU; y++)
//                 {
//                     pixels[y * texture.width + vPixel] = pathColor;
//                 }
//             }

            texture.Apply(false, false);
//             // majorChunk.ValueRO.renderInformation.Value.GetPixelData<Color32>(0);
// #endregion
            SystemAPI.SetComponentEnabled<ChunkConfigured>(entity, true); // TODO Move
#endregion

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

