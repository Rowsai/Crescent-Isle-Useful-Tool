using System.Numerics;
using Dalamud.Game.ClientState.Objects.Types;
using ECommons.GameHelpers;
using Ocelot.Extensions;

namespace Ocelot.Prowler;

public static class IGameObjectEx
{
    public static Vector3 GetPointOnHitboxFromPlayer(this IGameObject obj, float offset = 0f)
    {
        return obj.GetPointOnHitboxFrom(Player.Position, offset);
    }

    public static Vector3 GetPointOnHitboxFrom(this IGameObject obj, Vector3 from, float offset = 0f)
    {
        return obj.Position.GetPointFrom(from, obj.HitboxRadius + offset, obj.HitboxRadius);
    }

    public static float DistanceTo(this IGameObject obj, IGameObject other)
    {
        return obj.DistanceTo(other.Position);
    }

    public static float DistanceTo(this IGameObject obj, Vector3 other)
    {
        return obj.Position.DistanceTo(other);
    }

    public static float DistanceTo2D(this IGameObject obj, IGameObject other)
    {
        return obj.DistanceTo2D(other.Position);
    }

    public static float DistanceTo2D(this IGameObject obj, Vector3 other)
    {
        return obj.Position.DistanceTo2D(other);
    }
}
