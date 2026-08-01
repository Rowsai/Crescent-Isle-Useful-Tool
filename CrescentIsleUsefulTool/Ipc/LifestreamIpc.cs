using System;
using Dalamud.Plugin.Ipc.Exceptions;
using ECommons.DalamudServices;
using Ocelot.IPC;

namespace CrescentIsleUsefulTool.Ipc;

public static class LifestreamIpc
{
    public static bool IsOperational(Lifestream? lifestream, out string waitingReason)
    {
        waitingReason = "";
        if (lifestream == null || !lifestream.IsReady())
        {
            waitingReason = "Lifestreamプラグインの起動を待っています。";
            return false;
        }

        try
        {
            // IsReady() only checks Dalamud's plugin list. Calling a harmless
            // query also confirms that Lifestream has registered its IPC gates.
            _ = lifestream.IsBusy();
            return true;
        }
        catch (IpcNotReadyError)
        {
            waitingReason = "Lifestream IPCの登録完了を待っています。";
            return false;
        }
        catch (Exception ex)
        {
            waitingReason = "Lifestreamの準備状態を取得できません。";
            Svc.Log.Warning(ex, "Failed to query Lifestream readiness.");
            return false;
        }
    }

    public static bool TryIsBusy(Lifestream? lifestream, out bool isBusy)
    {
        isBusy = true;
        if (lifestream == null || !lifestream.IsReady())
        {
            return false;
        }

        try
        {
            isBusy = lifestream.IsBusy();
            return true;
        }
        catch (IpcNotReadyError)
        {
            return false;
        }
        catch (Exception ex)
        {
            Svc.Log.Warning(ex, "Failed to query Lifestream busy state.");
            return false;
        }
    }

    public static bool TryAbort(Lifestream? lifestream)
    {
        if (lifestream == null || !lifestream.IsReady())
        {
            return false;
        }

        try
        {
            lifestream.Abort();
            return true;
        }
        catch (IpcNotReadyError)
        {
            return false;
        }
        catch (Exception ex)
        {
            Svc.Log.Warning(ex, "Failed to abort Lifestream safely.");
            return false;
        }
    }

    public static bool TryGetActiveCustomAetheryte(Lifestream? lifestream, out uint placeNameId)
    {
        placeNameId = 0;
        if (lifestream == null || !lifestream.IsReady())
        {
            return false;
        }

        try
        {
            placeNameId = lifestream.GetActiveCustomAetheryte();
            return true;
        }
        catch (IpcNotReadyError)
        {
            return false;
        }
        catch (Exception ex)
        {
            Svc.Log.Warning(ex, "Failed to query the active Lifestream aetheryte.");
            return false;
        }
    }

    public static bool TryAethernetTeleport(Lifestream? lifestream, uint placeNameId)
    {
        if (lifestream == null || !lifestream.IsReady())
        {
            return false;
        }

        try
        {
            return lifestream.AethernetTeleportByPlaceNameId(placeNameId);
        }
        catch (IpcNotReadyError)
        {
            return false;
        }
        catch (Exception ex)
        {
            Svc.Log.Warning(ex, "Failed to request a Lifestream aethernet teleport.");
            return false;
        }
    }
}
