using System;
using Dalamud.Plugin.Ipc.Exceptions;
using ECommons.DalamudServices;
using Ocelot.IPC;

namespace BOCCHI.Ipc;

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
}
