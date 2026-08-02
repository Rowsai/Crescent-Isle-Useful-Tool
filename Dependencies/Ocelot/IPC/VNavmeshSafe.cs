using System;
using System.Collections.Generic;
using System.Numerics;
using System.Threading.Tasks;

namespace Ocelot.IPC;

/// <summary>
/// Failure-safe access to optional vnavmesh IPC endpoints. IPC delegates may
/// become unavailable between IsReady and invocation while plugins reload.
/// </summary>
public static class VNavmeshSafe
{
    public static bool TryIsRunning(VNavmesh? vnav, out bool running)
    {
        running = false;
        if (vnav?.IsReady() != true)
        {
            return false;
        }

        try
        {
            running = vnav.IsRunning();
            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }

    public static bool TryIsPathfinding(VNavmesh? vnav, out bool pathfinding)
    {
        pathfinding = false;
        if (vnav?.IsReady() != true)
        {
            return false;
        }

        try
        {
            pathfinding = vnav.IsPathfinding();
            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }

    public static bool TryIsSimpleMovePathfinding(VNavmesh? vnav, out bool pathfinding)
    {
        pathfinding = false;
        if (vnav?.IsReady() != true)
        {
            return false;
        }

        try
        {
            pathfinding = vnav.IsSimpleMoveInProgress();
            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }

    public static bool TryStop(VNavmesh? vnav)
    {
        if (vnav?.IsReady() != true)
        {
            return false;
        }

        try
        {
            vnav.Stop();
            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }

    public static bool TryPathfind(
        VNavmesh? vnav,
        Vector3 start,
        Vector3 destination,
        bool fly,
        out Task<List<Vector3>>? task)
    {
        task = null;
        if (vnav?.IsReady() != true)
        {
            return false;
        }

        try
        {
            task = vnav.Pathfind(start, destination, fly);
            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }

    public static bool TryMoveTo(VNavmesh? vnav, List<Vector3> nodes, bool fly)
    {
        if (vnav?.IsReady() != true)
        {
            return false;
        }

        try
        {
            vnav.MoveTo(nodes, fly);
            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }

    public static bool TryPathfindAndMoveTo(VNavmesh? vnav, Vector3 destination, bool fly)
    {
        if (vnav?.IsReady() != true)
        {
            return false;
        }

        try
        {
            return vnav.PathfindAndMoveTo(destination, fly);
        }
        catch (Exception)
        {
            return false;
        }
    }
}
