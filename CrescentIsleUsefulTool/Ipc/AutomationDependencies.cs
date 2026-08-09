using System;
using System.Collections.Generic;
using System.Linq;
using Dalamud.Plugin.Ipc;
using Dalamud.Plugin.Ipc.Exceptions;
using ECommons.Automation;
using ECommons.DalamudServices;

namespace CrescentIsleUsefulTool.Ipc;

/// <summary>
/// Central readiness gate for every plugin that owns part of CIUT automation.
/// Passive UI remains available while a dependency is missing, but no game
/// actions are produced until all three providers are loaded and callable.
/// </summary>
public static class AutomationDependencies
{
    private const string VnavmeshName = "vnavmesh";
    private const string BossModRebornName = "BossMod Reborn";
    private const string RotationSolverRebornName = "Rotation Solver Reborn";

    private static ICallGateSubscriber<bool>? vnavmeshReady;
    private static ICallGateSubscriber<bool>? rotationSolverActive;
    private static ICallGateSubscriber<RsrSpecialCommand, object>? rotationSolverSpecialState;

    private static bool bossModActionsSuppressed;
    private static bool rotationSolverActionsSuppressed;
    private static bool bossModRestorePending;
    private static bool rotationSolverRestorePending;

    public static bool IsMagicPotAiSuppressed => bossModActionsSuppressed || rotationSolverActionsSuppressed;

    public static AutomationDependencySnapshot GetSnapshot()
    {
        var vnavmeshLoaded = IsLoaded(VnavmeshName);
        var bossModLoaded = IsLoaded(BossModRebornName);
        var rotationSolverLoaded = IsLoaded(RotationSolverRebornName);

        var navmeshReady = false;
        var rotationSolverIpcReady = false;
        var rotationSolverIsActive = false;

        if (vnavmeshLoaded)
        {
            try
            {
                vnavmeshReady ??= Svc.PluginInterface.GetIpcSubscriber<bool>("vnavmesh.Nav.IsReady");
                navmeshReady = vnavmeshReady.InvokeFunc();
            }
            catch (IpcNotReadyError)
            {
                vnavmeshReady = null;
            }
            catch
            {
                vnavmeshReady = null;
            }
        }

        if (rotationSolverLoaded)
        {
            try
            {
                rotationSolverActive ??= Svc.PluginInterface.GetIpcSubscriber<bool>(
                    "RotationSolverReborn.AutorotationActive");
                rotationSolverIsActive = rotationSolverActive.InvokeFunc();
                rotationSolverIpcReady = true;
            }
            catch (IpcNotReadyError)
            {
                rotationSolverActive = null;
            }
            catch
            {
                rotationSolverActive = null;
            }
        }

        TryCompletePendingRestoration(
            bossModLoaded,
            rotationSolverLoaded && rotationSolverIpcReady);

        return new AutomationDependencySnapshot(
            vnavmeshLoaded,
            navmeshReady,
            bossModLoaded,
            rotationSolverLoaded,
            rotationSolverIpcReady,
            rotationSolverIsActive);
    }

    public static bool TrySuppressMagicPotCombatAi(out string failureReason)
    {
        failureReason = "";
        var snapshot = GetSnapshot();
        if (!snapshot.AllReady)
        {
            failureReason = $"必須プラグインの準備待ち：{string.Join("、", snapshot.MissingNames)}";
            return false;
        }

        if (!bossModActionsSuppressed)
        {
            try
            {
                // BMR keeps navigation available but refuses targeting and
                // combat actions while its ForbidActions switch is enabled.
                Chat.ExecuteCommand("/bmrai forbidactions on");
                bossModActionsSuppressed = true;
                bossModRestorePending = false;
            }
            catch (Exception exception)
            {
                Svc.Log.Warning(exception, "Failed to suppress BossMod Reborn actions.");
                failureReason = "BossMod Rebornの攻撃抑止を有効にできませんでした。";
                return false;
            }
        }

        if (!rotationSolverActionsSuppressed)
        {
            try
            {
                rotationSolverSpecialState ??= Svc.PluginInterface
                    .GetIpcSubscriber<RsrSpecialCommand, object>(
                        "RotationSolverReborn.TriggerSpecialState");
                rotationSolverSpecialState.InvokeAction(RsrSpecialCommand.NoCasting);
                rotationSolverActionsSuppressed = true;
                rotationSolverRestorePending = false;
            }
            catch (Exception exception)
            {
                Svc.Log.Warning(exception, "Failed to suppress Rotation Solver Reborn actions.");
                ReleaseMagicPotCombatAi();
                failureReason = "Rotation Solver Rebornの攻撃抑止を有効にできませんでした。";
                return false;
            }
        }

        return true;
    }

    public static void ReleaseMagicPotCombatAi()
    {
        var snapshot = GetSnapshot();

        if (bossModActionsSuppressed)
        {
            bossModRestorePending = true;
            try
            {
                if (snapshot.BossModRebornLoaded)
                {
                    Chat.ExecuteCommand("/bmrai forbidactions off");
                    bossModActionsSuppressed = false;
                    bossModRestorePending = false;
                }
            }
            catch (Exception exception)
            {
                Svc.Log.Warning(exception, "Failed to restore BossMod Reborn actions.");
            }
        }

        if (rotationSolverActionsSuppressed)
        {
            rotationSolverRestorePending = true;
            try
            {
                if (snapshot.RotationSolverIpcReady)
                {
                    rotationSolverSpecialState ??= Svc.PluginInterface
                        .GetIpcSubscriber<RsrSpecialCommand, object>(
                            "RotationSolverReborn.TriggerSpecialState");
                    rotationSolverSpecialState.InvokeAction(RsrSpecialCommand.EndSpecial);
                    rotationSolverActionsSuppressed = false;
                    rotationSolverRestorePending = false;
                }
            }
            catch (Exception exception)
            {
                Svc.Log.Warning(exception, "Failed to restore Rotation Solver Reborn actions.");
            }
        }
    }

    private static void TryCompletePendingRestoration(bool bossModLoaded, bool rotationSolverReady)
    {
        if (bossModRestorePending && bossModLoaded)
        {
            try
            {
                Chat.ExecuteCommand("/bmrai forbidactions off");
                bossModActionsSuppressed = false;
                bossModRestorePending = false;
            }
            catch
            {
                // Keep the pending flag and retry on the next readiness poll.
            }
        }

        if (rotationSolverRestorePending && rotationSolverReady)
        {
            try
            {
                rotationSolverSpecialState ??= Svc.PluginInterface
                    .GetIpcSubscriber<RsrSpecialCommand, object>(
                        "RotationSolverReborn.TriggerSpecialState");
                rotationSolverSpecialState.InvokeAction(RsrSpecialCommand.EndSpecial);
                rotationSolverActionsSuppressed = false;
                rotationSolverRestorePending = false;
            }
            catch
            {
                // Keep the pending flag and retry on the next readiness poll.
            }
        }
    }

    private static bool IsLoaded(params string[] names)
    {
        try
        {
            return Svc.PluginInterface.InstalledPlugins.Any(plugin =>
                plugin.IsLoaded && names.Any(name =>
                    plugin.Name.Equals(name, StringComparison.OrdinalIgnoreCase)));
        }
        catch
        {
            return false;
        }
    }

    // Numeric values follow Rotation Solver Reborn's public SpecialCommandType
    // IPC contract. This temporary state preserves the user's Auto/Manual/Off
    // operating mode, unlike changing the whole RSR operating mode.
    private enum RsrSpecialCommand : byte
    {
        EndSpecial = 0,
        NoCasting = 13,
    }
}

public readonly record struct AutomationDependencySnapshot(
    bool VnavmeshLoaded,
    bool VnavmeshReady,
    bool BossModRebornLoaded,
    bool RotationSolverRebornLoaded,
    bool RotationSolverIpcReady,
    bool RotationSolverActive)
{
    public bool AllReady => VnavmeshReady && BossModRebornLoaded && RotationSolverIpcReady;

    public IReadOnlyList<string> MissingNames
    {
        get
        {
            var missing = new List<string>();
            if (!VnavmeshReady)
            {
                missing.Add(VnavmeshLoaded ? "vnavmesh（ナビメッシュ準備中）" : "vnavmesh");
            }

            if (!BossModRebornLoaded)
            {
                missing.Add("BossMod Reborn（BMR）");
            }

            if (!RotationSolverIpcReady)
            {
                missing.Add(RotationSolverRebornLoaded
                    ? "Rotation Solver Reborn（RSR・IPC準備中）"
                    : "Rotation Solver Reborn（RSR）");
            }

            return missing;
        }
    }
}
