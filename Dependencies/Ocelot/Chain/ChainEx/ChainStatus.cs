using System.Linq;
using ECommons.Automation.NeoTaskManager;
using ECommons.DalamudServices;
using ECommons.Throttlers;

namespace Ocelot.Chain.ChainEx;

public static class ChainStatus
{
    private static TaskManagerTask WaitUntilStatus(uint status, int timeout = 5000, int interval = 50)
    {
        return new TaskManagerTask(() =>
        {
            if (EzThrottler.Throttle($"ChainStatus.WaitUntilStatus({status})", interval))
            {
                return Svc.Objects.LocalPlayer?.StatusList.Any(s => s.StatusId == status) == true;
            }

            return false;
        }, new TaskManagerConfiguration { TimeLimitMS = timeout });
    }

    public static Chain WaitUntilStatus(this Chain chain, uint status, int timeout = 5000, int interval = 50)
    {
        return chain
            .Debug($"Waiting until player has status {status}")
            .Then(WaitUntilStatus(status, timeout, interval));
    }

    private static TaskManagerTask WaitUntilNotStatus(uint status, int timeout = 5000, int interval = 50)
    {
        return new TaskManagerTask(() =>
        {
            if (EzThrottler.Throttle($"ChainStatus.WaitUntilNotStatus({status})", interval))
            {
                return Svc.Objects.LocalPlayer?.StatusList.Any(s => s.StatusId == status) == false;
            }

            return false;
        }, new TaskManagerConfiguration { TimeLimitMS = timeout });
    }

    public static Chain WaitUntilNotStatus(this Chain chain, uint status, int timeout = 5000, int interval = 50)
    {
        return chain
            .Debug($"Waiting until player does not have status {status}")
            .Then(WaitUntilNotStatus(status, timeout, interval));
    }

    public static Chain WaitToCycleStatus(this Chain chain, uint status, int timeout = 5000, int interval = 50)
    {
        return chain.WaitUntilNotStatus(status, timeout, interval).WaitUntilNotStatus(status, timeout, interval);
    }
}
