using Unity.Collections;
using Unity.Mathematics;

public struct Line2
{
    public float2 start;
    public float2 end;

    public Line2(float2 start, float2 end)
    {
        this.start = start;
        this.end = end;
    }

    public float2 Direction => end - start;
    public float Length => math.length(end - start);
    public float2 Tangent => math.normalizesafe(end - start);
}

public struct ContourLine
{
    public Line2 line;

    public int previous;
    public int next;

    public ContourLine(float2 start, float2 end)
    {
        line = new Line2(start, end);
        previous = -1;
        next = -1;
    }
}

public struct TensorSample
{
    public float2 major;
    public float2 minor;
}

public struct CoordinateSpace2
{
    public float sourceScale;
    public float targetScale;
    public int rim;

    public CoordinateSpace2(float sourceScale, float targetScale, int rim = 0)
    {
        this.sourceScale = sourceScale;
        this.targetScale = targetScale;
        this.rim = rim;
    }

    public float2 Convert(float2 position)
    {
        return position * (targetScale / sourceScale);
    }

    public Line2 Convert(Line2 line)
    {
        return new Line2(Convert(line.start), Convert(line.end));
    }

    public float2 ConvertInverse(float2 position)
    {
        return position * (sourceScale / targetScale);
    }

    public Line2 ConvertInverse(Line2 line)
    {
        return new Line2(ConvertInverse(line.start), ConvertInverse(line.end));
    }

    public int2 ConvertIndex(float2 position)
    {
        return (int2)math.round(Convert(position)) + rim;
    }

    public float2 ConvertIndexInverse(int2 index)
    {
        return ConvertInverse((float2)(index - rim));
    }
}

public struct LineProjection
{
    public float2 point;
    public float2 tangent;
    public float distance;
    public float t;

    public LineProjection(float2 point, float2 tangent, float distance, float t)
    {
        this.point = point;
        this.tangent = tangent;
        this.distance = distance;
        this.t = t;
    }
}

public static class TensorMath
{
    public static LineProjection Project(float2 point, Line2 line)
    {
        float2 direction = line.end - line.start;
        float lengthSq = math.lengthsq(direction);

        if(lengthSq <= 0f) return new LineProjection(line.start, float2.zero, math.distance(point, line.start), 0f);

        float t = math.saturate(math.dot(point - line.start, direction) / lengthSq);
        float2 projected = line.start + direction * t;
        float2 tangent = math.normalize(direction);

        return new LineProjection(projected, tangent, math.distance(point, projected), t);
    }

    // Convert the line to match the point.
    public static LineProjection Project(float2 point, Line2 line, CoordinateSpace2 space)
    {
        return Project(point, space.Convert(line));
    }

    // Convert the point to match the line.
    public static LineProjection Project(float2 point, CoordinateSpace2 space, Line2 line)
    {
        return Project(space.Convert(point), line);
    }

    public static float Distance(float2 point, Line2 line)
    {
        return Project(point, line).distance;
    }

    // Convert the line to match the point.
    public static float Distance(float2 point, Line2 line, CoordinateSpace2 space)
    {
        return Project(point, space.Convert(line)).distance;
    }

    // Convert the point to match the line.
    public static float Distance(float2 point, CoordinateSpace2 space, Line2 line)
    {
        return Project(space.Convert(point), line).distance;
    }

    public static LineProjection ProjectSmooth(float2 point, Line2 previous, Line2 current, Line2 next)
    {
        LineProjection projection = Project(point, current);

        float2 currentDirection = math.normalizesafe(current.end - current.start);
        float2 previousDirection = math.normalizesafe(previous.end - previous.start, currentDirection);
        float2 nextDirection = math.normalizesafe(next.end - next.start, currentDirection);

        previousDirection = MatchDirection(previousDirection, currentDirection);
        nextDirection = MatchDirection(nextDirection, currentDirection);

        float2 startTangent = math.normalizesafe(previousDirection + currentDirection, currentDirection);
        float2 endTangent = math.normalizesafe(currentDirection + nextDirection, currentDirection);

        projection.tangent = math.normalizesafe(math.lerp(startTangent, endTangent, SmoothStep(projection.t)), currentDirection);

        return projection;
    }

    // Convert the lines to match the point.
    public static LineProjection ProjectSmooth(float2 point, Line2 previous, Line2 current, Line2 next, CoordinateSpace2 space)
    {
        return ProjectSmooth(point, space.Convert(previous), space.Convert(current), space.Convert(next));
    }

    // Convert the point to match the lines.
    public static LineProjection ProjectSmooth(float2 point, CoordinateSpace2 space, Line2 previous, Line2 current, Line2 next)
    {
        return ProjectSmooth(space.Convert(point), previous, current, next);
    }

    public static LineProjection ProjectSmooth(float2 point, NativeList<ContourLine> contours, int index)
    {
        ContourLine current = contours[index];

        Line2 previous = current.previous >= 0 ? contours[current.previous].line : current.line;
        Line2 next = current.next >= 0 ? contours[current.next].line : current.line;

        return ProjectSmooth(point, previous, current.line, next);
    }

    private static float2 MatchDirection(float2 direction, float2 reference)
    {
        return math.dot(direction, reference) < 0f ? -direction : direction;
    }

    private static float SmoothStep(float t)
    {
        return t * t * (3f - 2f * t);
    }
}