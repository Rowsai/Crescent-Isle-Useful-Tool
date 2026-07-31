using System;
using ECommons;
using ECommons.Automation;
using ECommons.Automation.NeoTaskManager;
using ECommons.DalamudServices;
using ECommons.Throttlers;
using FFXIVClientStructs.FFXIV.Component.GUI;

namespace Ocelot.Chain.ChainEx;

public static class ChainAddon
{
    private static unsafe TaskManagerTask AddonCallback(string addonName, bool updateState = true, params object[] callbackValues)
    {
        return new TaskManagerTask(() =>
        {
            if (EzThrottler.Throttle($"ChainAddon.AddonCallback({addonName}, {updateState}, {string.Join(", ", callbackValues)})"))
            {
                var addonPtr = Svc.GameGui.GetAddonByName(addonName);
                if (addonPtr != IntPtr.Zero)
                {
                    var addon = (AtkUnitBase*)addonPtr.Address;
                    if (addon->IsReady)
                    {
                        Callback.Fire(addon, updateState, callbackValues);
                        return true;
                    }
                }
            }

            return false;
        }, new TaskManagerConfiguration { TimeLimitMS = 3000 });
    }

    public static Chain AddonCallback(this Chain chain, string addonName, bool updateState = true, params object[] callbackValues)
    {
        return chain
            .Debug($"Waiting for addon callback to fire {addonName} {updateState} {string.Join(", ", callbackValues)}")
            .Then(AddonCallback(addonName, updateState, callbackValues));
    }

    private static unsafe TaskManagerTask WaitForAddonReady(string addonName, int timeout = 3000)
    {
        return new TaskManagerTask(() =>
        {
            if (EzThrottler.Throttle($"ChainAddon.WaitForAddon({addonName})"))
            {
                var addonPtr = Svc.GameGui.GetAddonByName(addonName);
                if (addonPtr != IntPtr.Zero)
                {
                    var addon = (AtkUnitBase*)addonPtr.Address;
                    return GenericHelpers.IsAddonReady(addon);
                }
            }

            return false;
        }, new TaskManagerConfiguration { TimeLimitMS = timeout, TimeoutSilently = true });
    }

    public static Chain WaitForAddonReady(this Chain chain, string addonName, int timeout = 3000)
    {
        return chain.Debug($"Waiting for addon to be ready '{addonName}'").Then(WaitForAddonReady(addonName, timeout));
    }

    private static unsafe TaskManagerTask WaitForAddonNotReady(string addonName, int timeout = 3000)
    {
        return new TaskManagerTask(() =>
        {
            if (EzThrottler.Throttle($"ChainAddon.WaitForAddon({addonName})"))
            {
                var addonPtr = Svc.GameGui.GetAddonByName(addonName);
                if (addonPtr != IntPtr.Zero)
                {
                    var addon = (AtkUnitBase*)addonPtr.Address;
                    return !GenericHelpers.IsAddonReady(addon);
                }
            }

            return false;
        }, new TaskManagerConfiguration { TimeLimitMS = timeout, TimeoutSilently = true });
    }

    public static Chain WaitForAddonNotReady(this Chain chain, string addonName, int timeout = 3000)
    {
        return chain.Debug($"Waiting for addon to be not ready '{addonName}'").Then(WaitForAddonNotReady(addonName, timeout));
    }
}
