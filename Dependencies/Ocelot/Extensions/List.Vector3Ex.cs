using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

namespace Ocelot.Extensions;

public static class ListVector3Ex
{
    public static float Length(this List<Vector3> nodes)
    {
        if (nodes.Count < 2)
        {
            return 0f;
        }

        var length = 0f;
        for (var i = 1; i < nodes.Count; i++)
        {
            length += Vector3.Distance(nodes[i - 1], nodes[i]);
        }

        return length;
    }

    public static List<Vector3> ContinueFrom(this List<Vector3> points, Vector3 point)
    {
        if (points.Count == 0)
        {
            return [];
        }

        var bestIndex = 0;
        var bestDistance = float.MaxValue;

        for (var i = 0; i < points.Count; i++)
        {
            var totalDistance = Vector3.Distance(point, points[i]);

            for (var j = i; j < points.Count - 1; j++)
            {
                totalDistance += Vector3.Distance(points[j], points[j + 1]);
            }

            if (totalDistance < bestDistance)
            {
                bestDistance = totalDistance;
                bestIndex = i;
            }
        }

        return points.GetRange(bestIndex, points.Count - bestIndex);
    }


    public static List<Vector3> Smooth(this List<Vector3> points, float pointsPerUnit = 0.25f, int minSegments = 2)
    {
        if (points.Count < 2)
        {
            return points;
        }

        points = points.Distinct().ToList();
        var smoothed = new List<Vector3> { points[0] };

        for (var i = 0; i < points.Count - 1; i++)
        {
            var p0 = i > 0 ? points[i - 1] : points[i];
            var p1 = points[i];
            var p2 = points[i + 1];
            var p3 = i + 2 < points.Count ? points[i + 2] : p2;

            const float t0 = 0.0f;
            var t1 = GetT(t0, p0, p1);
            var t2 = GetT(t1, p1, p2);
            var t3 = GetT(t2, p2, p3);

            var segmentLength = Vector3.Distance(p1, p2);
            var segments = Math.Max(minSegments, (int)(segmentLength * pointsPerUnit));

            for (var j = 0; j <= segments; j++)
            {
                var t = t1 + (t2 - t1) * (j / (float)segments);
                var A1 = Lerp(p0, p1, (t1 - t) / (t1 - t0), (t - t0) / (t1 - t0));
                var A2 = Lerp(p1, p2, (t2 - t) / (t2 - t1), (t - t1) / (t2 - t1));
                var A3 = Lerp(p2, p3, (t3 - t) / (t3 - t2), (t - t2) / (t3 - t2));

                var B1 = Lerp(A1, A2, (t2 - t) / (t2 - t0), (t - t0) / (t2 - t0));
                var B2 = Lerp(A2, A3, (t3 - t) / (t3 - t1), (t - t1) / (t3 - t1));

                var C = Lerp(B1, B2, (t2 - t) / (t2 - t1), (t - t1) / (t2 - t1));

                smoothed.Add(C);
            }
        }

        smoothed.Add(points[^1]);

        return smoothed
            .Where(v => !float.IsNaN(v.X) && !float.IsNaN(v.Y) && !float.IsNaN(v.Z))
            .ToList();
    }

    private static float GetT(float t, Vector3 p0, Vector3 p1)
    {
        return MathF.Pow(Vector3.Distance(p0, p1), 0.5f) + t;
    }

    private static Vector3 Lerp(Vector3 a, Vector3 b, float w1, float w2)
    {
        return a * w1 + b * w2;
    }

    public static Vector3 Center(this List<Vector3> points)
    {
        {
            if (points.Count == 0)
            {
                return Vector3.Zero;
            }

            var sum = Vector3.Zero;
            foreach (var point in points)
            {
                sum += point;
            }

            return sum / points.Count;
        }
    }
}
