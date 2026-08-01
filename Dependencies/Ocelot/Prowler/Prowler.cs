using System;
using Ocelot.Chain;
using Ocelot.IPC;

namespace Ocelot.Prowler;

public class Prowler
{
    private static Prowler? CachedInstance = null;

    public static Prowl? Current;

    private static Prowler Instance
    {
        get
        {
            if (CachedInstance == null)
            {
                throw new InvalidOperationException("Prowler has not been initialized. Call Initialize(plugin) first.");
            }

            return CachedInstance;
        }
    }

    private readonly OcelotPlugin plugin;

    private VNavmesh Vnavmesh
    {
        get => Instance.plugin.IPC.GetSubscriber<VNavmesh>();
    }

    private ChainQueue Chain
    {
        get => ChainManager.Get("Ocelot.Prowler.Prowler.ChainQueue");
    }

    public static bool IsRunning
    {
        get
        {
            VNavmeshSafe.TryIsRunning(Instance.Vnavmesh, out var running);
            VNavmeshSafe.TryIsPathfinding(Instance.Vnavmesh, out var pathfinding);
            return Instance.Chain.IsRunning || running || pathfinding;
        }
    }

    private Prowler(OcelotPlugin plugin)
    {
        this.plugin = plugin;
    }

    public static void Initialize(OcelotPlugin plugin)
    {
        CachedInstance ??= new Prowler(plugin);
    }

    public static void Prowl(Prowl prowl)
    {
        Instance.Chain.Submit(chain => chain
            .Then(_ => Current = prowl)
            .Then(prowl.GetChain(Instance.Vnavmesh))
            .OnFinally(() => Current = null)
        );
    }

    public static void Abort()
    {
        VNavmeshSafe.TryStop(Instance.Vnavmesh);
        Instance.Chain.Abort();
    }
}
