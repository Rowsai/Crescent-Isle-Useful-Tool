using System.Collections.Generic;
using Ocelot.Modules;

namespace Ocelot;

public static class Gate
{
    private readonly static Dictionary<string, long> Elapsed = [];

    public static bool Milliseconds<T>(T owner, long interval, UpdateContext context) where T : class
    {
        var key = owner.GetType().FullName!;

        var elapsed = Elapsed.GetValueOrDefault(key, 0);
        elapsed += context.Framework.UpdateDelta.Milliseconds;

        if (elapsed >= interval)
        {
            Elapsed[key] = elapsed - interval;
            return true;
        }

        Elapsed[key] = elapsed;
        return false;
    }

    public static bool UpdatesPerSecond<T>(T owner, int ups, UpdateContext context) where T : class
    {
        return Milliseconds(owner, 1000 / ups, context);
    }

    public static bool Seconds<T>(T owner, int seconds, UpdateContext context) where T : class
    {
        return Milliseconds(owner, seconds * 1000, context);
    }

    public static bool Minutes<T>(T owner, int minutes, UpdateContext context) where T : class
    {
        return Seconds(owner, minutes * 60, context);
    }
}
