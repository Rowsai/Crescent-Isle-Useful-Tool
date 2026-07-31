using System;

namespace Ocelot.Extensions;

public static class FloatEx
{
    public static TimeSpan GetEstimatedTimeToFlyDistance(this float distance, float speed = 20f)
    {
        return TimeSpan.FromSeconds(distance / speed);
    }
}
