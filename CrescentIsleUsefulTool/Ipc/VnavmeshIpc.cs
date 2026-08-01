using System;
using System.Collections.Generic;
using System.Numerics;
using System.Threading.Tasks;
using Dalamud.Plugin.Ipc.Exceptions;
using ECommons.DalamudServices;
using ECommons.GameHelpers;
using Ocelot.IPC;

namespace CrescentIsleUsefulTool.Ipc;

/// <summary>
/// Guards optional vnavmesh IPC calls. Dalamud can keep the subscriber object
/// alive briefly while vnavmesh is loading or unloading, during which invoking
/// a delegate directly throws <see cref="IpcNotReadyError"/>.
/// </summary>
public static class VnavmeshIpc
{
    public static bool IsOperational(VNavmesh? vnav, out string waitingReason)
    {
        waitingReason = "";
        if (vnav == null || !vnav.IsReady())
        {
            waitingReason = "vnavmeshプラグインの起動を待っています。";
            return false;
        }

        try
        {
            if (!vnav.IsNavmeshReady())
            {
                waitingReason = "現在エリアのナビメッシュ読み込みを待っています。";
                return false;
            }

            return true;
        }
        catch (IpcNotReadyError)
        {
            waitingReason = "vnavmesh IPCの登録完了を待っています。";
            return false;
        }
        catch (Exception ex)
        {
            waitingReason = "vnavmeshの準備状態を取得できません。";
            Svc.Log.Warning(ex, "Failed to query vnavmesh readiness.");
            return false;
        }
    }

    public static bool TryStop(VNavmesh? vnav)
    {
        if (vnav == null || !vnav.IsReady())
        {
            return false;
        }

        try
        {
            vnav.Stop();
            return true;
        }
        catch (IpcNotReadyError)
        {
            return false;
        }
        catch (Exception ex)
        {
            Svc.Log.Warning(ex, "Failed to stop vnavmesh safely.");
            return false;
        }
    }

    public static bool TryIsRunning(VNavmesh? vnav, out bool isRunning)
    {
        isRunning = false;
        if (vnav == null || !vnav.IsReady())
        {
            return false;
        }

        try
        {
            isRunning = vnav.IsRunning();
            return true;
        }
        catch (IpcNotReadyError)
        {
            return false;
        }
        catch (Exception ex)
        {
            Svc.Log.Warning(ex, "Failed to query vnavmesh movement state.");
            return false;
        }
    }

    public static bool TryIsPathfinding(VNavmesh? vnav, out bool isPathfinding)
    {
        isPathfinding = false;
        if (vnav == null || !vnav.IsReady())
        {
            return false;
        }

        try
        {
            isPathfinding = vnav.IsPathfinding();
            return true;
        }
        catch (IpcNotReadyError)
        {
            return false;
        }
        catch (Exception ex)
        {
            Svc.Log.Warning(ex, "Failed to query vnavmesh pathfinding state.");
            return false;
        }
    }

    public static bool IsMovementActive(VNavmesh? vnav)
    {
        if (Player.IsMoving)
        {
            return true;
        }

        // If IPC state cannot be read, fail closed and do not begin a cast.
        if (!TryIsRunning(vnav, out var isRunning) || isRunning)
        {
            return true;
        }

        return !TryIsPathfinding(vnav, out var isPathfinding) || isPathfinding;
    }

    public static bool TryPathfindAndMoveTo(VNavmesh? vnav, Vector3 destination, bool fly)
    {
        if (!IsOperational(vnav, out _))
        {
            return false;
        }

        try
        {
            return vnav!.PathfindAndMoveTo(destination, fly);
        }
        catch (IpcNotReadyError)
        {
            return false;
        }
        catch (Exception ex)
        {
            Svc.Log.Warning(ex, "Failed to start vnavmesh pathfinding.");
            return false;
        }
    }

    public static bool TryPathfind(VNavmesh? vnav, Vector3 start, Vector3 destination, bool fly, out Task<List<Vector3>>? task)
    {
        task = null;
        if (!IsOperational(vnav, out _))
        {
            return false;
        }

        try
        {
            task = vnav!.Pathfind(start, destination, fly);
            return true;
        }
        catch (IpcNotReadyError)
        {
            return false;
        }
        catch (Exception ex)
        {
            Svc.Log.Warning(ex, "Failed to request a vnavmesh path.");
            return false;
        }
    }

    public static bool TryFollowPath(VNavmesh? vnav, List<Vector3> path, bool fly)
    {
        if (!IsOperational(vnav, out _))
        {
            return false;
        }

        try
        {
            vnav!.FollowPath(path, fly);
            return true;
        }
        catch (IpcNotReadyError)
        {
            return false;
        }
        catch (Exception ex)
        {
            Svc.Log.Warning(ex, "Failed to ask vnavmesh to follow a path.");
            return false;
        }
    }

    public static bool TryFindPointOnFloor(VNavmesh? vnav, Vector3 position, bool allowUnlandable, float halfExtent, out Vector3? point)
    {
        point = null;
        if (!IsOperational(vnav, out _))
        {
            return false;
        }

        try
        {
            point = vnav!.FindPointOnFloor(position, allowUnlandable, halfExtent);
            return true;
        }
        catch (IpcNotReadyError)
        {
            return false;
        }
        catch (Exception ex)
        {
            Svc.Log.Warning(ex, "Failed to query a vnavmesh floor point.");
            return false;
        }
    }
}
