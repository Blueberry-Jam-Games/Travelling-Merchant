using System.Collections.Generic;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;
using System.IO;
using System.Linq;

[UpdateInGroup(typeof(WorldGenerationSystemGroup))]
[UpdateAfter(typeof(ChunkMajorPopulationSystem))]
partial class ChunkMajorTensorSystem : SystemBase
{
    private int heightmapSize;
    private int heightmapResolution;

    private const int TensorJobCount = 2; // TODO Replace with Chunk World version

    private NativeArray<int> marchingSquareArray;

    List<PathGenerationContainer> pathGenerationContainer;

    protected override void OnCreate()
    {
        pathGenerationContainer = new List<PathGenerationContainer>();
        PathResolver.SetPathData(74896123); // TODO Change to chunk world

        marchingSquareArray = new NativeArray<int>(new int[]
        {
            -1, -1, -1, -1, // 0
            3,  0, -1, -1, // 1
            0,  1, -1, -1, // 2
            3,  1, -1, -1, // 3
            1,  2, -1, -1, // 4
            3,  0,  1,  2, // 5
            0,  2, -1, -1, // 6
            3,  2, -1, -1, // 7
            2,  3, -1, -1, // 8
            0,  2, -1, -1, // 9
            0,  1,  2,  3, // 10
            1,  2, -1, -1, // 11
            3,  1, -1, -1, // 12
            0,  1, -1, -1, // 13
            3,  0, -1, -1, // 14
            -1, -1, -1, -1  // 15
        }, Allocator.Persistent);
    }

    protected override void OnUpdate()
    {
        ChunkWorldSingleton chunkWorld = SystemAPI.GetSingleton<ChunkWorldSingleton>();

        int detailMapResolution = chunkWorld.detailMapResolution;
        
        GameState gameState = SystemAPI.GetSingleton<GameState>();

        if(!gameState.chunkGenerationEnable) return;


        foreach ((
            RefRW<MajorChunk> majorChunk,
            RefRO<LocalTransform> Transform,
            RefRO<Chunk> chunk,
            Entity entity
            ) in SystemAPI.Query<
                RefRW<MajorChunk>,
                RefRO<LocalTransform>,
                RefRO<Chunk>
                >()
                .WithDisabled<ChunkPopulated, ChunkPopulating>()
                .WithAll<ChunkActive, ChunkConfigured>()
                .WithEntityAccess())
        {
            if(pathGenerationContainer.Count >= TensorJobCount) break; // TODO Change TensorJobCount to Chunk World

            SystemAPI.SetComponentEnabled<ChunkPopulating>(entity, true);
            Texture2D detailMap = majorChunk.ValueRO.renderInformation.Value;

            if(!PathResolver.TryGetChunkIntersections(Transform.ValueRO.Position.xz, chunkWorld.majorChunkSize, out PathBoundaryPoint entrance, out PathBoundaryPoint exit))
            {
                // No path
                SystemAPI.SetComponentEnabled<ChunkPopulating>(entity, false);
                SystemAPI.SetComponentEnabled<ChunkPopulated>(entity, true);
                continue;
            }

            PathGenerationContainer container = new PathGenerationContainer
            {
                MajorChunk = entity,
                pos = Transform.ValueRO.Position.xz,

                Heightmap = majorChunk.ValueRO.heightmapData,
                worldToHeightmap = new CoordinateSpace2(chunkWorld.majorChunkSize, majorChunk.ValueRO.heightmapData.Value.resolution - 3, 1),

                DetailMap = detailMap.GetRawTextureData<half4>(),
                worldToDetail = new CoordinateSpace2(chunkWorld.majorChunkSize, detailMapResolution - 1),

                Stage = PathGenerationStage.TerrainAnalysis,

                Entrance = entrance,
                Exit = exit,

                Path = new NativeList<float2>(Allocator.Persistent),
                Contours = new NativeList<ContourLine>(Allocator.Persistent)
            };

            TerrainContourJob terrainJob = new TerrainContourJob
            {
                Heightmap = container.Heightmap,
                MarchingSquareArray = marchingSquareArray,
                ChunkOrigin = Transform.ValueRO.Position.xz,
                ChunkSize = chunkWorld.majorChunkSize,
                IsoLevel = 100f,
                Contours = container.Contours
            };

            JobHandle terrainHandle = terrainJob.Schedule();

            PathGenerationJob pathJob = new PathGenerationJob
            {
                Heightmap = container.Heightmap,
                Contours = container.Contours,

                ChunkOrigin = container.pos,
                ChunkSize = chunkWorld.majorChunkSize,

                WorldToHeightmap = container.worldToHeightmap,
                WorldToDetail = container.worldToDetail,

                Start = container.Entrance,
                End = container.Exit,

                Path = container.Path,
                DetailMap = container.DetailMap,

                StepSize = 4f,

                PathHalfWidth = 4f,
                PathFeather = 2f,

                ContourFullStrengthDistance = 10f,
                ContourInfluenceDistance = 80f,
                ContourStrength = 0.85f,

                MinorAdvantage = 0.2f,
                TurnResponsiveness = 0.4f,

                ExitBiasStart = 0.15f,
                ExitBiasEnd = 0.65f,
                EntranceDirectionDistance = 30f,
                ExitDirectionDistance = 60f,

                SurfaceMaxGrade = 0.15f,
                MinorMaxGrade = 0.05f,
                ExitSurfaceDistance = 40f,
                MinimumExitAlignment = 0.15f,
            };

            container.Stage = PathGenerationStage.MainPath;
            container.Handle = pathJob.Schedule(terrainHandle);
            pathGenerationContainer.Add(container);

#region Old
        }

        for(int i = pathGenerationContainer.Count - 1; i >= 0; i--)
        {
            PathGenerationContainer container = pathGenerationContainer[i];

            if(!container.Handle.IsCompleted) continue;

            container.Handle.Complete();

            //SaveDetailMapDebug(container);

            MajorChunk majorChunk = SystemAPI.GetComponent<MajorChunk>(container.MajorChunk);
            Texture2D detailMap = majorChunk.renderInformation.Value;
            detailMap.Apply(false, false);

            if(container.Path.IsCreated) container.Path.Dispose();
            if(container.Contours.IsCreated) container.Contours.Dispose();

            pathGenerationContainer.RemoveAt(i);

            SystemAPI.SetComponentEnabled<ChunkPopulating>(container.MajorChunk, false);
            SystemAPI.SetComponentEnabled<ChunkPopulated>(container.MajorChunk, true);
        }

#endregion
    }

    protected override void OnDestroy()
    {
        if(marchingSquareArray.IsCreated) marchingSquareArray.Dispose();
    }

    public enum PathGenerationStage : byte
    {
        TerrainAnalysis,
        MainPath,
        TownCheck,
        TownPaths,
        Complete
    }


    public struct PathGenerationContainer
    {
        public Entity MajorChunk;
        public float2 pos;
        public BlobAssetReference<HeightmapData> Heightmap;
        public CoordinateSpace2 worldToHeightmap;

        public PathGenerationStage Stage;
        public JobHandle Handle;

        public PathBoundaryPoint Entrance;
        public PathBoundaryPoint Exit;

        public NativeArray<half4> DetailMap;
        public CoordinateSpace2 worldToDetail;

        public NativeList<float2> Path;

        public NativeList<ContourLine> Contours;
    }

    [BurstCompile]
    public struct TerrainContourJob : IJob
    {
        [ReadOnly] public BlobAssetReference<HeightmapData> Heightmap;
        [ReadOnly] public NativeArray<int> MarchingSquareArray;

        public float2 ChunkOrigin;
        public float ChunkSize;
        public float IsoLevel;

        public NativeList<ContourLine> Contours;

        public void Execute()
        {
            int cellResolution = Heightmap.Value.resolution - 3;
            float cellSize = ChunkSize / cellResolution;

            for(int y = -1; y <= cellResolution; y++)
            {
                for(int x = -1; x <= cellResolution; x++)
                {
                    float bottomLeft = Heightmap.Value.GetHeight(x, y);
                    float bottomRight = Heightmap.Value.GetHeight(x + 1, y);
                    float topRight = Heightmap.Value.GetHeight(x + 1, y + 1);
                    float topLeft = Heightmap.Value.GetHeight(x, y + 1);

                    int state = 0;
                    if(bottomLeft >= IsoLevel) state |= 1;
                    if(bottomRight >= IsoLevel) state |= 2;
                    if(topRight >= IsoLevel) state |= 4;
                    if(topLeft >= IsoLevel) state |= 8;

                    if(state == 0 || state == 15) continue;

                    int tableState = state;

                    if(state == 5 || state == 10)
                    {
                        bool centerHigh = (bottomLeft + bottomRight + topRight + topLeft) * 0.25f >= IsoLevel;
                        if(state == 5) tableState = centerHigh ? 10 : 5;
                        else tableState = centerHigh ? 5 : 10;
                    }

                    float2 origin = ChunkOrigin + new float2(x, y) * cellSize;

                    float2 bottomLeftPosition = origin;
                    float2 bottomRightPosition = origin + new float2(cellSize, 0f);
                    float2 topRightPosition = origin + new float2(cellSize, cellSize);
                    float2 topLeftPosition = origin + new float2(0f, cellSize);

                    FixedList64Bytes<float2> edges = default;
                    edges.Add(Interpolate(bottomLeftPosition, bottomRightPosition, bottomLeft, bottomRight));
                    edges.Add(Interpolate(bottomRightPosition, topRightPosition, bottomRight, topRight));
                    edges.Add(Interpolate(topLeftPosition, topRightPosition, topLeft, topRight));
                    edges.Add(Interpolate(bottomLeftPosition, topLeftPosition, bottomLeft, topLeft));

                    int tableIndex = tableState * 4;

                    for(int i = 0; i < 4; i += 2)
                    {
                        int edgeA = MarchingSquareArray[tableIndex + i];
                        if(edgeA < 0) break;

                        int edgeB = MarchingSquareArray[tableIndex + i + 1];
                        AddLine(edges[edgeA], edges[edgeB]);
                    }
                }
            }

            ConnectContours(cellSize * 0.001f);
        }

        private float2 Interpolate(float2 a, float2 b, float valueA, float valueB)
        {
            float difference = valueB - valueA;
            if(math.abs(difference) < 0.000001f) return (a + b) * 0.5f;

            float t = math.saturate((IsoLevel - valueA) / difference);
            return math.lerp(a, b, t);
        }

        private void AddLine(float2 start, float2 end)
        {
            if(math.lengthsq(end - start) < 0.000001f) return;

            Contours.Add(new ContourLine
            {
                line = new Line2(start, end),
                previous = -1,
                next = -1
            });
        }

        private void ConnectContours(float tolerance)
        {
            float toleranceSq = tolerance * tolerance;

            for(int i = 0; i < Contours.Length; i++)
            {
                ContourLine contour = Contours[i];

                contour.previous = FindConnectedLine(contour.line.start, i, toleranceSq);
                contour.next = FindConnectedLine(contour.line.end, i, toleranceSq);

                Contours[i] = contour;
            }
        }

        private int FindConnectedLine(float2 point, int currentIndex, float toleranceSq)
        {
            for(int i = 0; i < Contours.Length; i++)
            {
                if(i == currentIndex) continue;

                Line2 line = Contours[i].line;

                if(math.lengthsq(point - line.start) <= toleranceSq) return i;
                if(math.lengthsq(point - line.end) <= toleranceSq) return i;
            }

            return -1;
        }
    }

#region To Replace
    [BurstCompile]
    public struct PathGenerationJob : IJob
    {
        [ReadOnly] public BlobAssetReference<HeightmapData> Heightmap;
        [ReadOnly] public NativeList<ContourLine> Contours;

        public float2 ChunkOrigin;
        public float ChunkSize;

        public CoordinateSpace2 WorldToHeightmap;
        public CoordinateSpace2 WorldToDetail;

        public PathBoundaryPoint Start;
        public PathBoundaryPoint End;

        public NativeList<float2> Path;
        public NativeArray<half4> DetailMap;

        public float StepSize;

        public float PathHalfWidth;
        public float PathFeather;

        public float ContourFullStrengthDistance;
        public float ContourInfluenceDistance;
        public float ContourStrength;

        public float MinorAdvantage;
        public float TurnResponsiveness;

        public float ExitBiasStart;
        public float ExitBiasEnd;
        public float EntranceDirectionDistance;
        public float ExitDirectionDistance;

        public float SurfaceMaxGrade;
        public float MinorMaxGrade;
        public float ExitSurfaceDistance;
        public float MinimumExitAlignment;

        public void Execute()
        {
            Path.Clear();

            float2 start = Start.position;
            float2 end = End.position;

            float2 startDirection = math.normalizesafe(Start.perpendicular, math.normalizesafe(end - start));
            float2 endDirection = math.normalizesafe(End.perpendicular, startDirection);

            float2 current = start;
            float currentHeight = SampleHeight(current);

            float initialDistance = math.max(math.distance(start, end), StepSize);
            float travelled = 0f;

            bool usingMinor = false;

            int maxSteps = math.max(16, (int)math.ceil(initialDistance / (StepSize * math.max(MinimumExitAlignment, 0.05f))) + 32); // Useless

            Path.Add(current);

            for(int step = 0; step < maxSteps; step++)
            {
                float2 toExit = end - current;
                float distanceToExit = math.length(toExit);

                if(distanceToExit <= StepSize * 2f)
                {
                    FinishPath(current, currentHeight, end, endDirection);
                    return;
                }

                float2 exitDirection = toExit / distanceToExit;
                float2 currentDirection = PathDirection();

                TensorSample tensor = SampleTensor(current);

                float progress = 1f - math.saturate(distanceToExit / initialDistance);
                float exitBias = math.lerp(ExitBiasStart, ExitBiasEnd, progress * progress);

                float2 desired = math.normalizesafe(math.lerp(currentDirection, exitDirection, exitBias), exitDirection);

                float entranceInfluence = 1f - math.saturate(travelled / EntranceDirectionDistance);
                desired = math.normalizesafe(math.lerp(desired, startDirection, entranceInfluence), startDirection);

                float exitInfluence = 1f - math.saturate(distanceToExit / ExitDirectionDistance);
                desired = math.normalizesafe(math.lerp(desired, endDirection, exitInfluence), endDirection);

                float2 major = MatchDirection(tensor.major, desired);
                float2 minor = MatchDirection(tensor.minor, desired);

                float majorScore = math.dot(major, desired);
                float minorScore = math.dot(minor, desired);

                float minorThreshold = usingMinor ? MinorAdvantage * 0.5f : MinorAdvantage;
                usingMinor = minorScore > majorScore + minorThreshold;

                float2 tensorDirection = usingMinor ? minor : major;
                float2 direction = math.normalizesafe(math.lerp(currentDirection, tensorDirection, TurnResponsiveness), tensorDirection);

                if(step == 0) direction = startDirection;
                else direction = EnforceExitProgress(direction, exitDirection);

                float2 next = current + direction * StepSize;

                if(!InsideChunk(next))
                {
                    direction = exitDirection;
                    next = current + direction * StepSize;
                }

                float distanceMoved = math.distance(current, next);
                float terrainHeight = SampleHeight(next);
                float nextHeight = CalculatePathHeight(currentHeight, terrainHeight, distanceMoved, usingMinor, math.distance(next, end));

                PaintSegment(current, next, currentHeight, nextHeight);

                Path.Add(next);

                travelled += distanceMoved;
                current = next;
                currentHeight = nextHeight;
            }

            FinishPath(current, currentHeight, end, endDirection);
        }

        private TensorSample SampleTensor(float2 point)
        {
            float2 gradient = SampleGradient(point);

            float2 minor = math.normalizesafe(gradient, new float2(0f, 1f));
            float2 major = new float2(-minor.y, minor.x);

            int closestContour = -1;
            float closestDistance = ContourInfluenceDistance;

            for(int i = 0; i < Contours.Length; i++)
            {
                LineProjection projection = TensorMath.Project(point, Contours[i].line);

                if(projection.distance >= closestDistance) continue;

                closestDistance = projection.distance;
                closestContour = i;
            }

            if(closestContour < 0)
            {
                return new TensorSample
                {
                    major = major,
                    minor = minor
                };
            }

            LineProjection contour = TensorMath.ProjectSmooth(point, Contours, closestContour);

            float influence = 1f - math.smoothstep(ContourFullStrengthDistance, ContourInfluenceDistance, contour.distance);
            influence = math.saturate(influence * ContourStrength);

            float2 contourMajor = MatchDirection(contour.tangent, major);
            major = math.normalizesafe(math.lerp(major, contourMajor, influence), major);

            float2 contourMinor = new float2(-major.y, major.x);
            minor = MatchDirection(contourMinor, minor);

            return new TensorSample
            {
                major = major,
                minor = minor
            };
        }

        private float2 EnforceExitProgress(float2 direction, float2 exitDirection)
        {
            float alignment = math.dot(direction, exitDirection);

            if(alignment >= MinimumExitAlignment) return direction;

            float2 perpendicular = new float2(-exitDirection.y, exitDirection.x);
            float side = math.dot(direction, perpendicular) >= 0f ? 1f : -1f;
            float lateral = math.sqrt(math.max(0f, 1f - MinimumExitAlignment * MinimumExitAlignment));

            return exitDirection * MinimumExitAlignment + perpendicular * lateral * side;
        }

        private float SampleHeight(float2 worldPosition)
        {
            float2 position = WorldToHeightmap.Convert(worldPosition - ChunkOrigin);

            int resolution = Heightmap.Value.resolution - 3;

            int x = math.clamp((int)math.floor(position.x), 0, resolution - 1);
            int y = math.clamp((int)math.floor(position.y), 0, resolution - 1);

            float tx = position.x - x;
            float ty = position.y - y;

            float row0 = Cubic(Heightmap.Value.GetHeight(x - 1, y - 1), Heightmap.Value.GetHeight(x, y - 1), Heightmap.Value.GetHeight(x + 1, y - 1), Heightmap.Value.GetHeight(x + 2, y - 1), tx);
            float row1 = Cubic(Heightmap.Value.GetHeight(x - 1, y), Heightmap.Value.GetHeight(x, y), Heightmap.Value.GetHeight(x + 1, y), Heightmap.Value.GetHeight(x + 2, y), tx);
            float row2 = Cubic(Heightmap.Value.GetHeight(x - 1, y + 1), Heightmap.Value.GetHeight(x, y + 1), Heightmap.Value.GetHeight(x + 1, y + 1), Heightmap.Value.GetHeight(x + 2, y + 1), tx);
            float row3 = Cubic(Heightmap.Value.GetHeight(x - 1, y + 2), Heightmap.Value.GetHeight(x, y + 2), Heightmap.Value.GetHeight(x + 1, y + 2), Heightmap.Value.GetHeight(x + 2, y + 2), tx);

            return Cubic(row0, row1, row2, row3, ty);
        }

        private float2 SampleGradient(float2 worldPosition)
        {
            float2 position = WorldToHeightmap.Convert(worldPosition - ChunkOrigin);

            int resolution = Heightmap.Value.resolution - 3;

            int x = math.clamp((int)math.floor(position.x), 0, resolution - 1);
            int y = math.clamp((int)math.floor(position.y), 0, resolution - 1);

            float tx = position.x - x;
            float ty = position.y - y;

            float2 row0 = Cubic(Heightmap.Value.GetGradient(x - 1, y - 1), Heightmap.Value.GetGradient(x, y - 1), Heightmap.Value.GetGradient(x + 1, y - 1), Heightmap.Value.GetGradient(x + 2, y - 1), tx);
            float2 row1 = Cubic(Heightmap.Value.GetGradient(x - 1, y), Heightmap.Value.GetGradient(x, y), Heightmap.Value.GetGradient(x + 1, y), Heightmap.Value.GetGradient(x + 2, y), tx);
            float2 row2 = Cubic(Heightmap.Value.GetGradient(x - 1, y + 1), Heightmap.Value.GetGradient(x, y + 1), Heightmap.Value.GetGradient(x + 1, y + 1), Heightmap.Value.GetGradient(x + 2, y + 1), tx);
            float2 row3 = Cubic(Heightmap.Value.GetGradient(x - 1, y + 2), Heightmap.Value.GetGradient(x, y + 2), Heightmap.Value.GetGradient(x + 1, y + 2), Heightmap.Value.GetGradient(x + 2, y + 2), tx);

            return Cubic(row0, row1, row2, row3, ty);
        }

        private float CalculatePathHeight(float previousHeight, float terrainHeight, float distance, bool usingMinor, float distanceToExit)
        {
            float maxGrade = usingMinor ? MinorMaxGrade : SurfaceMaxGrade;
            float maxChange = distance * maxGrade;

            float height = math.clamp(terrainHeight, previousHeight - maxChange, previousHeight + maxChange);

            if(ExitSurfaceDistance > 0f)
            {
                float exitInfluence = 1f - math.saturate(distanceToExit / ExitSurfaceDistance);
                height = math.lerp(height, terrainHeight, exitInfluence);
            }

            return height;
        }

        private void FinishPath(float2 current, float currentHeight, float2 end, float2 endDirection)
        {
            float distance = math.distance(current, end);

            if(distance <= 0.001f) return;

            float guideDistance = math.min(StepSize, distance * 0.5f);
            float2 guide = end - endDirection * guideDistance;

            if(InsideChunk(guide) && math.distance(current, guide) > 0.001f)
            {
                float terrainHeight = SampleHeight(guide);
                float guideHeight = CalculatePathHeight(currentHeight, terrainHeight, math.distance(current, guide), false, guideDistance);

                PaintSegment(current, guide, currentHeight, guideHeight);
                Path.Add(guide);

                current = guide;
                currentHeight = guideHeight;
            }

            float endHeight = SampleHeight(end);

            PaintSegment(current, end, currentHeight, endHeight);

            if(math.distance(Path[Path.Length - 1], end) > 0.001f) Path.Add(end);
        }

        private void PaintSegment(float2 worldStart, float2 worldEnd, float startHeight, float endHeight)
        {
            float2 start = WorldToDetail.Convert(worldStart - ChunkOrigin);
            float2 end = WorldToDetail.Convert(worldEnd - ChunkOrigin);

            float halfWidth = PathHalfWidth * WorldToDetail.Rescale();
            float feather = PathFeather * WorldToDetail.Rescale();
            float outerWidth = halfWidth + feather;

            int resolution = (int)WorldToDetail.targetScale + 1;

            int minX = math.clamp((int)math.floor(math.min(start.x, end.x) - outerWidth), 0, resolution - 1);
            int maxX = math.clamp((int)math.ceil(math.max(start.x, end.x) + outerWidth), 0, resolution - 1);
            int minY = math.clamp((int)math.floor(math.min(start.y, end.y) - outerWidth), 0, resolution - 1);
            int maxY = math.clamp((int)math.ceil(math.max(start.y, end.y) + outerWidth), 0, resolution - 1);

            Line2 segment = new Line2(start, end);

            for(int y = minY; y <= maxY; y++)
            {
                for(int x = minX; x <= maxX; x++)
                {
                    float2 pixel = new float2(x, y);

                    LineProjection projection = TensorMath.Project(pixel, segment);

                    if(projection.distance > outerWidth) continue;

                    float strength = 1f;

                    if(projection.distance > halfWidth && feather > 0f)
                    {
                        float t = math.saturate((projection.distance - halfWidth) / feather);
                        strength = 1f - SmoothStep(t);
                    }

                    float pathHeight = math.lerp(startHeight, endHeight, projection.t);

                    int index = x + y * resolution;

                    half4 pixelData = DetailMap[index];
                    float existingStrength = (float)pixelData.x;

                    if(strength >= existingStrength)
                    {
                        pixelData.x = (half)strength;
                        pixelData.w = (half)pathHeight;

                        DetailMap[index] = pixelData;
                    }
                }
            }
        }

        private bool InsideChunk(float2 point)
        {
            float2 local = point - ChunkOrigin;

            return local.x >= 0f && local.y >= 0f && local.x <= ChunkSize && local.y <= ChunkSize;
        }

        private float2 PathDirection()
        {
            if(Path.Length < 2) return math.normalizesafe(Start.perpendicular);

            return math.normalizesafe(Path[Path.Length - 1] - Path[Path.Length - 2], Start.perpendicular);
        }

        private static float2 MatchDirection(float2 direction, float2 reference)
        {
            return math.dot(direction, reference) < 0f ? -direction : direction;
        }

        private static float Cubic(float p0, float p1, float p2, float p3, float t)
        {
            float t2 = t * t;
            float t3 = t2 * t;

            return 0.5f * ((2f * p1) + (-p0 + p2) * t + (2f * p0 - 5f * p1 + 4f * p2 - p3) * t2 + (-p0 + 3f * p1 - 3f * p2 + p3) * t3);
        }

        private static float2 Cubic(float2 p0, float2 p1, float2 p2, float2 p3, float t)
        {
            float t2 = t * t;
            float t3 = t2 * t;

            return 0.5f * ((2f * p1) + (-p0 + p2) * t + (2f * p0 - 5f * p1 + 4f * p2 - p3) * t2 + (-p0 + 3f * p1 - 3f * p2 + p3) * t3);
        }

        private static float SmoothStep(float t)
        {
            return t * t * (3f - 2f * t);
        }
    }
#endregion

// #region Tensor Population Container Struct
//     public struct TensorPopulationContainer
//     {
//         public Entity MajorChunk;

//         public BlobAssetReference<HeightmapData> heightmapData;

//         public bool ContainsCity;
//         public float3 PathEntrancePoint;
//         public float3 PathExitPoint;

//         public int TensorFieldResolution;
//         public int DetailMapResolution;
//         public NativeArray<float4> TensorField;
//         public NativeList<float2> Path;

//         public NativeArray<Color32> DetailMap;
//         public JobHandle Handle;
//     }
// #endregion

//     struct TensorGradientFillJob : IJob
//     {
//         public int TensorFieldResolution;
//         public NativeArray<float4> TensorField;
//         public BlobAssetReference<HeightmapData> Heightmap;
//         public int GradientResolution;
//         public float TensorToGradientScale;
//         public TensorGradientFillJob(TensorPopulationContainer container)
//         {
//             TensorFieldResolution = container.TensorFieldResolution;
//             TensorField = container.TensorField;
//             Heightmap = container.heightmapData;
//             GradientResolution = container.heightmapData.Value.resolution - 2;
//             TensorToGradientScale = (GradientResolution - 1f) / (TensorFieldResolution - 1f);
//         }

//         public void Execute()
//         {
//             for(int index = 0; index < TensorFieldResolution * TensorFieldResolution; index++)
//             {
//                 int tensorX = index % TensorFieldResolution;
//                 int tensorY = index / TensorFieldResolution;

//                 float gradientX = tensorX * TensorToGradientScale;
//                 float gradientY = tensorY * TensorToGradientScale;

//                 int x = math.min((int)math.floor(gradientX), GradientResolution - 2);
//                 int y = math.min((int)math.floor(gradientY), GradientResolution - 2);

//                 float tx = gradientX - x;
//                 float ty = gradientY - y;

//                 float2 row0 = Cubic(Heightmap.Value.GetGradient(x - 1, y - 1), Heightmap.Value.GetGradient(x, y - 1), Heightmap.Value.GetGradient(x + 1, y - 1), Heightmap.Value.GetGradient(x + 2, y - 1), tx);
//                 float2 row1 = Cubic(Heightmap.Value.GetGradient(x - 1, y), Heightmap.Value.GetGradient(x, y), Heightmap.Value.GetGradient(x + 1, y), Heightmap.Value.GetGradient(x + 2, y), tx);
//                 float2 row2 = Cubic(Heightmap.Value.GetGradient(x - 1, y + 1), Heightmap.Value.GetGradient(x, y + 1), Heightmap.Value.GetGradient(x + 1, y + 1), Heightmap.Value.GetGradient(x + 2, y + 1), tx);
//                 float2 row3 = Cubic(Heightmap.Value.GetGradient(x - 1, y + 2), Heightmap.Value.GetGradient(x, y + 2), Heightmap.Value.GetGradient(x + 1, y + 2), Heightmap.Value.GetGradient(x + 2, y + 2), tx);

//                 float2 gradient = Cubic(row0, row1, row2, row3, ty);

//                 float lengthSq = math.lengthsq(gradient);

//                 float2 minor = lengthSq > 0.000001f ? gradient * math.rsqrt(lengthSq) : new float2(0f, 1f);
//                 float2 major = new float2(-minor.y, minor.x);

//                 TensorField[index] = new float4(major.x, major.y, minor.x, minor.y);
//             }
//         }

//         private static float2 Cubic(float2 p0, float2 p1, float2 p2, float2 p3, float t)
//         {
//             float t2 = t * t;
//             float t3 = t2 * t;
//             return 0.5f * ((2f * p1) + (-p0 + p2) * t + (2f * p0 - 5f * p1 + 4f * p2 - p3) * t2 + (-p0 + 3f * p1 - 3f * p2 + p3) * t3);
//         }
//     }

    #region Debug Only
    private void SaveDetailMapDebug(PathGenerationContainer container)
    {
        int resolution = (int)container.worldToDetail.targetScale + 1;

        float minHeight = float.MaxValue;
        float maxHeight = float.MinValue;

        for(int i = 0; i < container.DetailMap.Length; i++)
        {
            half4 pixel = container.DetailMap[i];

            if((float)pixel.x <= 0f) continue;

            float height = (float)pixel.w;

            minHeight = math.min(minHeight, height);
            maxHeight = math.max(maxHeight, height);
        }

        bool hasPath = minHeight <= maxHeight;
        float heightRange = hasPath ? maxHeight - minHeight : 0f;

        NativeArray<Color32> pixels = new NativeArray<Color32>(container.DetailMap.Length, Allocator.Temp);

        for(int i = 0; i < container.DetailMap.Length; i++)
        {
            half4 source = container.DetailMap[i];

            float red = math.saturate((float)source.x);
            float green = math.saturate((float)source.y);
            float blue = math.saturate((float)source.z);

            float alpha = 0f;

            if(red > 0f && hasPath)
            {
                if(heightRange > 0.0001f) alpha = math.saturate(((float)source.w - minHeight) / heightRange);
                else alpha = 1f;
            }

            pixels[i] = new Color32((byte)math.round(red * 255f), (byte)math.round(green * 255f), (byte)math.round(blue * 255f), (byte)math.round(alpha * 255f));
        }

        Texture2D outputImage = new Texture2D(resolution, resolution, TextureFormat.RGBA32, false, true);
        outputImage.SetPixelData(pixels, 0);
        outputImage.Apply(false, false);

        string directory = Path.Combine(Application.dataPath, "PathDebug");
        Directory.CreateDirectory(directory);

        string filename = $"Path_{container.pos.x:0}_{container.pos.y:0}.png";
        File.WriteAllBytes(Path.Combine(directory, filename), outputImage.EncodeToPNG());

        UnityEngine.Object.Destroy(outputImage);
        pixels.Dispose();
    }
    #endregion
}

