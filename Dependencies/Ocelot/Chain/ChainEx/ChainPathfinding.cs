using System.Numerics;
using ECommons.Automation.NeoTaskManager;
using ECommons.DalamudServices;
using ECommons.Throttlers;
using Ocelot.IPC;

namespace Ocelot.Chain.ChainEx;

public static class ChainPathfinding
{
    private static TaskManagerTask WaitToStartPathfinding(VNavmesh vnav)
    {
        return new TaskManagerTask(() =>
        {
            if (EzThrottler.Throttle($"ChainPathfinding.WaitToStartPathfinding", 50))
            {
                return vnav.IsRunning();
            }

            return false;
        }, new TaskManagerConfiguration { TimeLimitMS = 2000 });
    }

    public static Chain WaitToStartPathfinding(this Chain chain, VNavmesh vnav)
    {
        return chain
            .Debug("Waiting to start pathfinding")
            .Then(WaitToStartPathfinding(vnav));
    }

    private static TaskManagerTask WaitToStopPathfinding(VNavmesh vnav)
    {
        return new TaskManagerTask(() =>
        {
            if (EzThrottler.Throttle($"ChainPathfinding.WaitToStopPathfinding", 50))
            {
                return !vnav.IsRunning();
            }

            return false;
        }, new TaskManagerConfiguration { TimeLimitMS = 30000 });
    }

    public static Chain WaitToStopPathfinding(this Chain chain, VNavmesh vnav)
    {
        return chain
            .Debug("Waiting to stop pathfinding")
            .Then(WaitToStopPathfinding(vnav));
    }

    public static Chain WaitForPathfindingCycle(this Chain chain, VNavmesh vnav)
    {
        return chain.WaitToStartPathfinding(vnav).WaitToStopPathfinding(vnav);
    }

    public static Chain PathfindAndMoveTo(this Chain chain, VNavmesh vnav, Vector3 destination)
    {
        return chain
            .Debug($"Pathfinding and moving to {destination}")
            .Then(_ => vnav.PathfindAndMoveTo(destination, false));
    }

    private static TaskManagerTask WaitUntilNear(VNavmesh vnav, Vector3 destination, float distance = 5f)
    {
        return new TaskManagerTask(() =>
        {
            var player = Svc.Objects.LocalPlayer;
            if (player == null)
            {
                return false;
            }

            return !vnav.IsRunning() || Vector3.Distance(player.Position, destination) <= distance;
        }, new TaskManagerConfiguration { TimeLimitMS = 30000 });
    }

    public static Chain WaitUntilNear(this Chain chain, VNavmesh vnav, Vector3 destination, float distance = 5f)
    {
        return chain
            .Debug($"Pathfinding and moving, and waiting to be near {destination}")
            .WaitToStartPathfinding(vnav)
            .Then(WaitUntilNear(vnav, destination, distance));
    }
}
