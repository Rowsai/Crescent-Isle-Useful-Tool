using System.Collections.Generic;
using Dalamud.Plugin.Services;
using ECommons.DalamudServices;

namespace Ocelot.Chain;

public static class ChainManager
{
    private readonly static Dictionary<string, ChainQueue> queues = [];

    public static IReadOnlyDictionary<string, ChainQueue> Queues
    {
        get => queues;
    }

    private static bool Initialized;

    public static ChainQueue Get(string id)
    {
        lock (queues)
        {
            if (!queues.TryGetValue(id, out var queue))
            {
                queue = new ChainQueue();
                queues[id] = queue;
            }

            return queue;
        }
    }

    internal static void Initialize()
    {
        if (Initialized)
        {
            return;
        }

        Initialized = true;

        Svc.Framework.Update += Tick;
    }

    private static void Tick(IFramework framework)
    {
        lock (queues)
        {
            var toRemove = new List<string>();

            foreach (var pair in queues)
            {
                var id = pair.Key;
                var queue = pair.Value;

                queue.Tick(framework);

                if (queue is { IsRunning: false, QueueCount: 0, TimeAlive.Seconds: >= 1 })
                {
                    if (queue.HasRun)
                    {
                        Logger.Debug($"Disposing ChainQueue '{id}' (inactive and empty)");
                    }

                    queue.Dispose();
                    toRemove.Add(id);
                }
            }

            foreach (var id in toRemove)
            {
                queues.Remove(id);
            }
        }
    }

    public static void AbortAll()
    {
        lock (queues)
        {
            foreach (var queue in queues.Values)
            {
                queue.Abort();
            }
        }

        Logger.Debug("Aborted all active ChainQueues.");
    }

    public static void Close()
    {
        Svc.Framework.Update -= Tick;

        lock (queues)
        {
            foreach (var queue in queues.Values)
            {
                queue.Dispose();
            }

            queues.Clear();
            Initialized = false;
        }
    }
}
