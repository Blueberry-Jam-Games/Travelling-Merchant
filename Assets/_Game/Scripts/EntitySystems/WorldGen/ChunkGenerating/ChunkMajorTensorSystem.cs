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

                StepSize = 1f,

                PathHalfWidth = 6f,
                PathFeather = 2f,

                // Blue channel: terrain-height influence. This is a continuous falloff
                // from the path centerline so it also provides stable segment ownership
                // through sharp corners without needing another texture channel.
                PathHeightBlendWidth = 24f,
                HeightSeamBlendDistance = 12f,

                TerrainWeight = 6f,

                ContourInfluenceDistance = 80f,
                ContourStrength = 0.8f,

                EdgeAvoidDistance = 50f,

                MaxGrade = 0.5f,

                TerrainInfluenceStart = 0.05f,
                TerrainInfluenceFull = 0.6f,

                GuideWeight = 0.6f,
                ExitGuideWeight = 4f,
                DirectionWeight = 0.5f,
                ExitGuideDistance = 100f,
                SeamBlendDistance = 40f,
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

            // SaveDetailMapDebug(container, -64f, 64f);

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

#region Path Generation
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

    // World-space distance from the path centerline where blue height influence reaches zero.
    public float PathHeightBlendWidth;
    public float HeightSeamBlendDistance;

    public float TerrainWeight;

    public float ContourInfluenceDistance;
    public float ContourStrength;

    public float EdgeAvoidDistance;

    public float MaxGrade;

    public float TerrainInfluenceStart;
    public float TerrainInfluenceFull;
    public float GuideWeight;
    public float ExitGuideWeight;
    public float DirectionWeight;
    public float ExitGuideDistance;
    public float SeamBlendDistance;

    public void Execute()
    {
        Path.Clear();

        float2 currentPosition = Start.position;
        float2 currentDirection = math.normalizesafe(Start.perpendicular, GetGuideDirection(currentPosition));

        float currentHeight = SampleHeight(currentPosition);
        float endHeight = SampleHeight(End.position);

        float minForward = 0.05f;

        int maxSteps = math.max(16, (int)math.ceil(ChunkSize / (StepSize * minForward)) + 32);

        Path.Add(currentPosition);

        for(int i = 0; i < maxSteps; i++)
        {
            float distanceToExit = math.distance(currentPosition, End.position);

            if(distanceToExit <= StepSize * 1.5f)
            {
                PaintSegment(currentPosition, End.position, currentHeight, endHeight);
                Path.Add(End.position);
                return;
            }

            float2 guideDirection = GetGuideDirection(currentPosition);
            TensorSample tensor = SampleTensor(currentPosition, out float terrainInfluence);

            float exitInfluence = 1f - math.saturate(distanceToExit / ExitGuideDistance);
            float guideWeight = math.lerp(GuideWeight, ExitGuideWeight, exitInfluence * exitInfluence);

            float2 reference = math.normalizesafe(guideDirection * guideWeight + currentDirection * DirectionWeight, guideDirection);
            float2 direction = reference;

            float seamDistance = math.min(math.distance(currentPosition, Start.position), distanceToExit);
            float seamInfluence = math.saturate(seamDistance / SeamBlendDistance);

            if(math.lengthsq(tensor.major) > 0.000001f)
            {
                float minorWeight = math.lerp(1f, 0.2f, terrainInfluence);

                float2 terrainDirection = tensor.major * math.dot(reference, tensor.major);
                terrainDirection += tensor.minor * math.dot(reference, tensor.minor) * minorWeight;
                terrainDirection = math.normalizesafe(terrainDirection, reference);

                float terrainStrength = math.saturate(terrainInfluence * TerrainWeight * seamInfluence);
                direction = math.normalizesafe(math.lerp(reference, terrainDirection, terrainStrength), reference);
            }

            direction += GetEdgeAvoidance(currentPosition) * 2f;
            direction = math.normalizesafe(direction, reference);
            direction = EnforceForward(direction, minForward);

            float2 nextPosition = currentPosition + direction * StepSize;

            if(!InsideChunk(nextPosition))
            {
                direction = EnforceForward(guideDirection, minForward);
                nextPosition = currentPosition + direction * StepSize;
            }

            float distanceMoved = math.distance(currentPosition, nextPosition);
            float terrainHeight = SampleHeight(nextPosition);
            float maxHeightChange = distanceMoved * MaxGrade;
            float nextHeight = math.clamp(terrainHeight, currentHeight - maxHeightChange, currentHeight + maxHeightChange);

            PaintSegment(currentPosition, nextPosition, currentHeight, nextHeight);
            Path.Add(nextPosition);

            currentPosition = nextPosition;
            currentDirection = direction;
            currentHeight = nextHeight;
        }
    }

    private float2 GetGuideDirection(float2 point)
    {
        float lookAhead = StepSize * 8f;
        float targetV = math.min(point.x + lookAhead, End.position.x);

        if(targetV <= point.x + 0.0001f) return math.normalizesafe(End.position - point, End.perpendicular);

        float targetU = targetV >= End.position.x - 0.0001f ? End.position.y : PathResolver.GetUFromV(targetV);
        float2 target = new float2(targetV, targetU);

        return math.normalizesafe(target - point, Start.perpendicular);
    }

    private TensorSample SampleTensor(float2 point, out float influence)
    {
        float2 gradient = SampleGradient(point);
        float gradientMagnitude = math.length(gradient);

        influence = math.smoothstep(TerrainInfluenceStart, TerrainInfluenceFull, gradientMagnitude);

        float2 minor = gradientMagnitude > 0.000001f ? gradient / gradientMagnitude : float2.zero;
        float2 major = gradientMagnitude > 0.000001f ? new float2(-minor.y, minor.x) : float2.zero;

        int closestContour = -1;
        float closestDistance = ContourInfluenceDistance;

        for(int i = 0; i < Contours.Length; i++)
        {
            float distance = TensorMath.Distance(point, Contours[i].line);

            if(distance >= closestDistance) continue;

            closestDistance = distance;
            closestContour = i;
        }

        if(closestContour >= 0)
        {
            LineProjection contour = TensorMath.ProjectSmooth(point, Contours, closestContour);

            float contourInfluence = 1f - math.smoothstep(0f, ContourInfluenceDistance, contour.distance);
            contourInfluence = math.saturate(contourInfluence * ContourStrength);

            if(math.lengthsq(major) > 0.000001f)
            {
                float2 contourMajor = MatchDirection(contour.tangent, major);
                major = math.normalizesafe(math.lerp(major, contourMajor, contourInfluence), contourMajor);
            }
            else
            {
                major = contour.tangent;
            }

            minor = new float2(-major.y, major.x);

            influence = math.max(influence, contourInfluence);
        }

        return new TensorSample { major = major, minor = minor };
    }

    private float2 GetEdgeAvoidance(float2 point)
    {
        float2 local = point - ChunkOrigin;
        float2 force = float2.zero;

        if(local.x < EdgeAvoidDistance) force.x += 1f - local.x / EdgeAvoidDistance;
        if(local.y < EdgeAvoidDistance) force.y += 1f - local.y / EdgeAvoidDistance;
        if(ChunkSize - local.x < EdgeAvoidDistance) force.x -= 1f - (ChunkSize - local.x) / EdgeAvoidDistance;
        if(ChunkSize - local.y < EdgeAvoidDistance) force.y -= 1f - (ChunkSize - local.y) / EdgeAvoidDistance;

        float startMask = math.saturate(math.distance(point, Start.position) / EdgeAvoidDistance);
        float endMask = math.saturate(math.distance(point, End.position) / EdgeAvoidDistance);

        return force * startMask * endMask;
    }

    private float2 EnforceForward(float2 direction, float minimum)
    {
        if(direction.x >= minimum) return direction;

        float maxY = math.sqrt(math.max(0f, 1f - minimum * minimum));
        return new float2(minimum, math.clamp(direction.y, -maxY, maxY));
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

    private void PaintSegment(float2 worldStart, float2 worldEnd, float startHeight, float endHeight)
    {
        float2 start = WorldToDetail.Convert(worldStart - ChunkOrigin);
        float2 end = WorldToDetail.Convert(worldEnd - ChunkOrigin);

        float scale = WorldToDetail.targetScale / WorldToDetail.sourceScale;

        // Red channel controls only the visible path.
        float visualHalfWidth = PathHalfWidth * scale;
        float visualFeather = PathFeather * scale;
        float visualOuterWidth = visualHalfWidth + visualFeather;

        // Blue channel controls terrain-height blending. Unlike red, this deliberately
        // has no flat-strength core: strength falls continuously with distance from
        // the centerline. That means the closest segment wins alpha at sharp corners.
        float heightOuterWidth = PathHeightBlendWidth * scale;

        float outerWidth = math.max(visualOuterWidth, heightOuterWidth);

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
                LineProjection projection = TensorMath.Project(new float2(x, y), segment);

                if(projection.distance > outerWidth) continue;

                float visualStrength = GetVisualStrength(projection.distance, visualHalfWidth, visualFeather);
                float heightStrength = GetHeightStrength(projection.distance, heightOuterWidth);

                float seamStrength = GetHeightSeamStrength(x, y, resolution, scale);
                heightStrength *= seamStrength;

                if(visualStrength <= 0f && heightStrength <= 0f) continue;

                float pathHeight = math.lerp(startHeight, endHeight, projection.t);

                int index = x + y * resolution;
                half4 pixel = DetailMap[index];
                bool changed = false;

                // Red is independent visual coverage.
                if(visualStrength > (float)pixel.x)
                {
                    pixel.x = (half)visualStrength;
                    changed = true;
                }

                // Blue is both the terrain blend strength and the ownership metric for alpha.
                // Because it continuously decreases from the centerline, the geometrically
                // closest path segment normally has the strongest blue value.
                float existingHeightStrength = (float)pixel.z;
                const float tieEpsilon = 0.001f;

                if(heightStrength > existingHeightStrength + tieEpsilon)
                {
                    pixel.z = (half)heightStrength;
                    pixel.w = (half)pathHeight;
                    changed = true;
                }
                else if(heightStrength > 0f && math.abs(heightStrength - existingHeightStrength) <= tieEpsilon)
                {
                    // On the exact bisector of a sharp corner, blend the two candidate
                    // heights instead of allowing paint order to create a hard seam.
                    pixel.z = (half)math.max(existingHeightStrength, heightStrength);
                    pixel.w = (half)math.lerp((float)pixel.w, pathHeight, 0.5f);
                    changed = true;
                }

                if(changed) DetailMap[index] = pixel;
            }
        }
    }

    private float GetHeightSeamStrength(int x, int y, int resolution, float scale)
    {
        float edgeDistance = math.min(x, y);
        edgeDistance = math.min(edgeDistance, resolution - 1 - x);
        edgeDistance = math.min(edgeDistance, resolution - 1 - y);

        float edgeDistanceWorld = edgeDistance / scale;
        float t = math.saturate(edgeDistanceWorld / HeightSeamBlendDistance);

        return SmoothStep(t);
    }

    private static float GetVisualStrength(float distance, float halfWidth, float feather)
    {
        if(distance <= halfWidth) return 1f;
        if(feather <= 0f) return 0f;

        float t = math.saturate((distance - halfWidth) / feather);
        return 1f - SmoothStep(t);
    }

    private static float GetHeightStrength(float distance, float outerWidth)
    {
        if(outerWidth <= 0f || distance >= outerWidth) return 0f;

        float t = math.saturate(distance / outerWidth);
        return 1f - SmoothStep(t);
    }

    private bool InsideChunk(float2 point)
    {
        float2 local = point - ChunkOrigin;
        return local.x >= 0f && local.y >= 0f && local.x <= ChunkSize && local.y <= ChunkSize;
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

    #region Debug Only
    private void SaveDetailMapDebug(PathGenerationContainer container, float heightStart, float heightEnd)
    {
        int resolution = (int)container.worldToDetail.targetScale + 1;

        float heightRange = heightEnd - heightStart;
        if(math.abs(heightRange) < 0.0001f) heightRange = 1f;

        NativeArray<Color32> pixels = new NativeArray<Color32>(container.DetailMap.Length, Allocator.Temp);

        for(int i = 0; i < container.DetailMap.Length; i++)
        {
            half4 source = container.DetailMap[i];

            float red = math.saturate((float)source.x);
            float blue = math.saturate((float)source.z);

            float height = (float)source.w;
            float heightVisual = math.saturate((height - heightStart) / heightRange);

            if(red <= 0f) heightVisual = 0f;

            pixels[i] = new Color32(
                (byte)math.round(red * 255f),
                (byte)math.round(heightVisual * 255f),
                (byte)math.round(blue * 255f),
                255
            );
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

