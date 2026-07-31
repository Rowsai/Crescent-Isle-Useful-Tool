using System;
using System.Numerics;
using ECommons.GameHelpers;

namespace Ocelot.Extensions;

public static class Vector3Ex
{
    public static float DistanceTo2D(this Vector3 a, Vector3 b)
    {
        var dx = a.X - b.X;
        var dz = a.Z - b.Z;
        return MathF.Sqrt(dx * dx + dz * dz);
    }

    public static float DistanceTo(this Vector3 a, Vector3 b)
    {
        return Vector3.Distance(a, b);
    }

    public static Vector3 GetPointFromPlayer(this Vector3 origin, float max, float min = 0f)
    {
        return origin.GetPointFrom(Player.Position, max, min);
    }

    public static Vector3 GetPointFrom(this Vector3 origin, Vector3 from, float max, float min = 0f)
    {
        if (min < 0f || min > max)
        {
            throw new ArgumentOutOfRangeException(nameof(min), "min must be between 0 and max");
        }

        var direction = Vector3.Normalize(origin - from);

        var angle = (float)(Random.Shared.NextDouble() * MathF.PI / 3 - MathF.PI / 6);
        var sin = MathF.Sin(angle);
        var cos = MathF.Cos(angle);

        var rotatedDirection = new Vector3(direction.X * cos - direction.Z * sin, 0, direction.X * sin + direction.Z * cos);
        var distance = (float)(min + Random.Shared.NextDouble() * (max - min));

        return origin - rotatedDirection * distance;
    }

    public static bool IsNaN(this Vector3 v)
    {
        return float.IsNaN(v.X) || float.IsNaN(v.Y) || float.IsNaN(v.Z);
    }
}
